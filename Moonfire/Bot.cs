using Moonfire.Interfaces;
using Moonfire.TableEntities;
using System.Collections.Concurrent;

namespace Moonfire;

public class Bot(string token, DiscordSocketConfig? config = null, List<Command>? _commands = null) : BotBase(token,config,_commands)
{
    private readonly ConcurrentDictionary<ulong, SCPInterface?> scpInterfaces = [];
    private readonly ConcurrentDictionary<ulong, Working> scpWorkingFlags = [];
    private readonly ConcurrentDictionary<ulong, MCInterface?> mcInterfaces = [];
    private readonly ConcurrentDictionary<ulong, Working> mcWorkingFlags = [];

    
    // Uncomment to do initial population of commands
    /*protected async override Task ClientReadyHandler(){
        await PopulateCommandsAsync(ownerServerId);
        return;
    }*/

    protected async override Task PreStartupTasks(){
        await ReconnectTaskAsync();
    }

    private async Task ReconnectTaskAsync(){
        var runningServers = await TableManager.QueryTableAsync<ServerEntity>($"{nameof(Moonfire)}Servers",e=>e.IsRunning);

        var reconnectionTasks = new List<Task>();

        foreach(var server in runningServers){
            if(server.RowKey==null || server.PartitionKey==null) continue;

            _ = Console.Out.WriteLineAsync($"Recreating {server.PartitionKey}:{server.RowKey}");
            var reconnectTask = Task.Run(async () =>{
                //create and validate interface creation
                var serverInterface = await CreateServerInterfaceAsync(server.PartitionKey,server.RowKey);
                if(serverInterface==null) return;

                //add interface to correct dictionary
                var guid = ulong.TryParse(server.PartitionKey,out var result) ? result : 0;
                switch(server.RowKey){
                    case "scp":
                        _ = Console.Out.WriteLineAsync($"Added scp interface at {guid}");
                        scpInterfaces.TryAdd(guid,(SCPInterface)serverInterface);
                        break;
                    case "mc":
                        _ = Console.Out.WriteLineAsync($"Added mc interface at {guid}");
                        mcInterfaces.TryAdd(guid,(MCInterface)serverInterface);
                        break;
                    default:
                        _ = Console.Out.WriteLineAsync($"{server.PartitionKey}:{server.RowKey}Failed to add to dictionary");
                        break;
                }

                //reconnect to server
                await serverInterface.StartServerAsync((a) => _ = Console.Out.WriteLineAsync($"{a}"));
            });

            reconnectionTasks.Add(reconnectTask);
        }

        try{
            await Task.WhenAll(reconnectionTasks);
        } catch (Exception e) {
            await Console.Out.WriteLineAsync($"{nameof(Bot)}:{ReconnectTaskAsync}:Reconnection Failed\n{e}");
            return;
        }
    }

    private static async Task<IServerBase?> CreateServerInterfaceAsync(string guid, string serverType) =>
        serverType switch{
            "scp" => await SCPInterface.CreateInterfaceAsync(guid),
            "mc" => await MCInterface.CreateInterfaceAsync(guid),
            _ => null
        };

    protected override Task SlashCommandHandler(SocketSlashCommand command){
        //finds first command that matches name of passed command
        //if a command was found gets the rank of that command
        //gets proper task to run as background task
        var handlerTask = commands.FirstOrDefault(p => p.Name == command.Data.Name)?.Rank switch
        {
            Rank.User => UserCommandHandler(command),

            Rank.Admin => AdminCommandHandler(command),

            Rank.Owner => OwnerCommandHandler(command),

            _ => SendSlashReplyAsync("Failed to find command in commands list",command)
        };

        //run returned task as background task
        _ = Task.Run(async () => {
            try{
                await handlerTask;
            }catch(Exception e){
                //log here to prevent silent failure of background task
                await Console.Out.WriteLineAsync($"{e}");
            }
        });

        return Task.CompletedTask;
    }

    private Task UserCommandHandler(SocketSlashCommand command){
        return command.Data.Name switch
        {
            helpCmd => PrintHelpAsync(command),
                
            _ => SendSlashReplyAsync($"Caught {command.Data.Name} by user handler but found no command",command),
        };
    }

    private Task AdminCommandHandler(SocketSlashCommand command){
        if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
            return SendSlashReplyAsync("You are not an admin",command);
        }

        return command.Data.Name switch
        {
            "start" => StartCommandHandler(command),

            "stop" => StopCommandHandler(command),

            _ => SendSlashReplyAsync($"Caught {command.Data.Name} by admin handler but found no command",command),
        };
    }

    private Task OwnerCommandHandler(SocketSlashCommand command){
        //owner commands are only registered in owner server
        //allows admin in owner server to manage bot
        if(!((SocketGuildUser)command.User).GuildPermissions.Administrator){
            return SendSlashReplyAsync("You are not an admin",command);
        }

        return command.Data.Name switch
        {
            "repopulate" => RepopulateTaskAsync(command),

            "console" => SendSlashReplyAsync("WIP",command),

            _ => SendSlashReplyAsync($"Caught {command.Data.Name} by owner handler but found no command",command),
        };
    }

    private Task StartCommandHandler(SocketSlashCommand command){
        return (Game)Convert.ToInt32(command.Data.Options.First().Value) switch
        {

            Game.SCP => StartTaskAsync(scpInterfaces,scpWorkingFlags,command),
            
            Game.MINECRAFT => SendSlashReplyAsync("Minecraft not available",command),
            
            Game.GMOD => SendSlashReplyAsync("GMOD not available",command),

            _ => SendSlashReplyAsync($"Caught {command.Data.Options.First().Value} by start command handler but found no game",command),
        };
    }

    private Task StopCommandHandler(SocketSlashCommand command){
        return (Game)Convert.ToInt32(command.Data.Options.First().Value) switch
        {
            
            Game.SCP => StopTaskAsync(scpInterfaces,scpWorkingFlags,command),
            
            Game.MINECRAFT => SendSlashReplyAsync("Minecraft not available",command),
            
            Game.GMOD => SendSlashReplyAsync("GMOD not available",command),

            _ => SendSlashReplyAsync($"Caught {command.Data.Options.First().Value} by stop command handler but found no game",command),
        };
    }

    private async Task PrintHelpAsync(SocketSlashCommand command){
        //empty string in case no commands
        string help = "";
        //formats and get information for every command
        foreach(var cmd in commands.Where(p => p.Rank == Rank.User || p.Rank == Rank.Admin).ToList())
            help += $"[{cmd.Name} - {cmd.Description}]\n";
        //to reduce code this removes the first/last bracket and last newline to match expected formatting
        await SendSlashReplyAsync(help[(help.IndexOf('[')+1)..help.LastIndexOf(']')],command);
    }

    private static async Task StartTaskAsync<TServer>(ConcurrentDictionary<ulong, TServer?> servers, ConcurrentDictionary<ulong, Working> workingFlags, SocketSlashCommand command)
        where TServer : class, IServer<TServer>
    {
        var cts = new CancellationTokenSource();

        //main start task
        var startTask = Task.Run(async () => {
            //ensure initial reply is sent first
            await SendSlashReplyAsync("Handling Command",command);

            if(await CheckMaintenance(command)) return;
            if(await CheckWorking(command,workingFlags)) return;

            //convert from ulong? to ulong
            ulong guid = command.GuildId ?? 0;

            workingFlags.TryAdd(guid,Working.STARTING);

            //if no value, or value = null
            if(!servers.TryGetValue(guid,out TServer? server) || server == null){
                //set server and dictionary value to interface object
                await ModifySlashReplyAsync("Provisioning Server",command);
                server = await TServer.CreateInterfaceAsync($"{guid}",cts.Token);
                servers.TryAdd(guid,server);
            }
            //check if provisioning failed
            if(server==null){
                await ModifySlashReplyAsync("Provisioning Failure",command);
                workingFlags.TryRemove(guid,out _);
                return;
            }

            //start server
            var success = await server.StartServerAsync((string a)=>ModifySlashReplyAsync(a,command),cts.Token);
            if(success) await ModifySlashReplyAsync($"Started Server at '{server.PublicIp}'",command);

            //remove starting lock
            workingFlags.TryRemove(guid,out _);
            
        },cts.Token);

        //cleanup task run after timeout
        Lazy<Task> cleanupTask = new(() => Task.Run(async () => {
            _ = Console.Out.WriteLineAsync($"{nameof(StartTaskAsync)}:Startup Timed Out");
            //send alert here

            await ModifySlashReplyAsync($"Server Startup Failed]\n   [Try Again Soon", command);
            ulong guid = command.GuildId ?? 0;

            workingFlags.TryRemove(guid,out _);
            workingFlags.TryAdd(guid,Working.RECOVERING);

            if (!servers.TryGetValue(guid, out TServer? server) || server == null)
            {
                workingFlags.Remove(guid,out _);
                return;
            }

            //This needs to run to clean up resources
            var stopcts = new CancellationTokenSource();
            await Ext.TimeoutTask
            (
                server.StopServerAsync((string a) => ModifySlashReplyAsync(a, command), stopcts.Token), 
                new(0, 10, 0), 
                stopcts
            );

            //fully cleans up server object
            servers.TryRemove(guid,out _);

            //remove recovery lock
            workingFlags.TryRemove(guid,out _);

        },cts.Token));

        await Ext.TimeoutTask(startTask,cleanupTask,new TimeSpan(0,5,0),cts);
    }

    private static async Task StopTaskAsync<TServer>(ConcurrentDictionary<ulong, TServer?> servers, ConcurrentDictionary<ulong, Working> workingFlags, SocketSlashCommand command)
        where TServer : class, IServer<TServer>
    {
        var cts = new CancellationTokenSource();

        var stopTask = Task.Run(async () => {
            //ensure initial reply is sent first
            await SendSlashReplyAsync("Stopping SCP Server",command);

            if(await CheckMaintenance(command)) return;

            var guid = command.GuildId ?? 0;

            if(await CheckWorking(command,workingFlags)) return;
            workingFlags.TryAdd(guid,Working.STOPPING);

            if(!servers.TryGetValue(guid,out var server)){
                await ModifySlashReplyAsync("No Server Found",command);
                workingFlags.TryRemove(guid,out _);
                return;
            }
            if(server==null){
                await ModifySlashReplyAsync("Server Was Null",command);
                workingFlags.TryRemove(guid,out _);
                return;
            }

            var result = await server.StopServerAsync((string a)=>ModifySlashReplyAsync(a,command),cts.Token);

            servers.TryRemove(guid,out _);
            workingFlags.TryRemove(guid,out _);

            if(result) await ModifySlashReplyAsync("Stopped Server",command);

        },cts.Token);

        Lazy<Task> cleanupTask = new(() => Task.Run(async () => {
            _ = Console.Out.WriteLineAsync($"{nameof(StopTaskAsync)}:Stopping Timed Out");
            //send alert here

            var guid = command.GuildId ?? 0;

            workingFlags.TryRemove(guid,out _);
            workingFlags.TryAdd(guid,Working.RECOVERING);

            await ModifySlashReplyAsync($"Server Stopping Failure]\n     [Try Again Soon",command);
            servers.TryRemove(guid,out _);
            workingFlags.TryRemove(guid,out _);

        },cts.Token));

        await Ext.TimeoutTask(stopTask,cleanupTask,new TimeSpan(0,10,0),cts);
    }

    private async Task RepopulateTaskAsync(SocketSlashCommand command){
        //ensure initial reply is sent first
        await SendSlashReplyAsync("Repopulating Commands",command);
        await UnregisterCommandsAsync();
        await PopulateCommandsAsync(ownerServerId);
        _ = ModifySlashReplyAsync("Commands Repopulated",command);
    }

    private static async Task<bool> CheckMaintenance(SocketSlashCommand command){
        if(await TableManager.GetBoolDefaultFalse("Updatefire","maintenance","bot","rebuilding")){
            var timeRaw = await TableManager.GetTableEntity("Updatefire","maintenance","bot","time");
            int time = (int?)timeRaw ?? 5;
            await SendSlashReplyAsync($"Bot undergoing maintenance]\n  [Try again in {time} minutes",command);
            return true;
        }
        return false;
    }

    private static async Task<bool> CheckWorking(SocketSlashCommand command, ConcurrentDictionary<ulong, Working> workingFlags){

        var guid = command.GuildId ?? 0;

        workingFlags.TryGetValue(guid,out var working);

        switch (working){
            case Working.STARTING:
                await ModifySlashReplyAsync("Server is Starting",command);
                return true;
            case Working.STOPPING:
                await ModifySlashReplyAsync("Server is Stopping",command);
                return true;
            case Working.RECOVERING:
                await ModifySlashReplyAsync("Server is Recovering From Failure",command);
                return true;
            default:
                return false;
        }
    }

    private static Task<EmbedBuilder> EmbedMessage(string input, SocketSlashCommand command){
        EmbedBuilder embed = new();
        embed.AddField($"**[{command.Data.Name.ToUpper()}]**",$"**```[{input}]```**");
        return Task.FromResult(embed);
    }

    private static async Task SendSlashReplyAsync(string input, SocketSlashCommand command)=>
        await command.RespondAsync(" ", embed: (await EmbedMessage(input,command)).Build(), ephemeral: true);

    private static async Task ModifySlashReplyAsync (string a, SocketSlashCommand command) =>
        await command.ModifyOriginalResponseAsync(async msg => msg.Embed = (await EmbedMessage(a,command)).Build());

    private enum Working{
        NONE,
        STARTING,
        STOPPING,
        RECOVERING
    }
}

public enum Game{
        NONE,
        SCP,
        MINECRAFT,
        GMOD
}
using Sunfire.Interfaces;

namespace Sunfire;

public class Bot(string token, DiscordSocketConfig? config = null, List<Command>? _commands = null) : BotBase(token,config,_commands)
{
    private readonly Dictionary<ulong, SCPInterface?> scpInterfaces = [];

    // Uncomment to do initial population of commands
    /*protected async override Task ClientReadyHandler(){
        await PopulateCommandsAsync(ownerServerId);
        return;
    }*/

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

            Game.SCP => StartTaskAsync(scpInterfaces,command),
            
            Game.MINECRAFT => SendSlashReplyAsync("Minecraft not available",command),
            
            Game.GMOD => SendSlashReplyAsync("GMOD not available",command),

            _ => SendSlashReplyAsync($"Caught {command.Data.Options.First().Value} by start command handler but found no game",command),
        };
    }

    private Task StopCommandHandler(SocketSlashCommand command){
        return (Game)Convert.ToInt32(command.Data.Options.First().Value) switch
        {
            
            Game.SCP => StopTaskAsync(scpInterfaces,command),
            
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

    private static async Task StartTaskAsync<TServer>(Dictionary<ulong, TServer?> servers, SocketSlashCommand command)
        where TServer : class, IServer<TServer>
    {
        if(await CheckMaintenance(command)) return;

        //ensure initial reply is sent first
        await SendSlashReplyAsync("Handling Command",command);
        //convert from ulong? to ulong
        ulong guid = command.GuildId ?? 0;
        //if no value, or value = null
        if(!servers.TryGetValue(guid,out TServer? server) || server == null){
            //set server and dictionary value to interface object
            await ModifySlashReplyAsync("Provisioning Server",command);
            server = await TServer.CreateInterfaceAsync($"{guid}");
            servers.Add(guid,server);
        }
        //check if provisioning failed
        if(server==null){
            await ModifySlashReplyAsync("Provisioning Failure",command);
            return;
        }
        //start server
        var result = await server.StartServerAsync((string a)=>ModifySlashReplyAsync(a,command));
        if(result) await ModifySlashReplyAsync($"Started Server at '{server.PublicIp}'",command);
    }

    private static async Task StopTaskAsync<TServer>(Dictionary<ulong, TServer?> servers, SocketSlashCommand command)
        where TServer : class, IServer<TServer>
    {
        if(await CheckMaintenance(command)) return;

        //ensure initial reply is sent first
        await SendSlashReplyAsync("Stopping SCP Server",command);
        var guid = command.GuildId ?? 0;
        if(!servers.TryGetValue(guid,out var server)){
            await ModifySlashReplyAsync("No Server Found",command);
            return;
        }
        if(server==null){
            await ModifySlashReplyAsync("Server Was Null",command);
            return;
        }
        var result = await server.StopServerAsync((string a)=>ModifySlashReplyAsync(a,command));
        servers.Remove(guid);
        if(result) await ModifySlashReplyAsync("Stopped Server",command);
    }

    private async Task RepopulateTaskAsync(SocketSlashCommand command){
        //ensure initial reply is sent first
        await SendSlashReplyAsync("Repopulating Commands",command);
        await UnregisterCommandsAsync();
        await PopulateCommandsAsync(ownerServerId);
        _ = ModifySlashReplyAsync("Commands Repopulated",command);
    }

    private static async Task<bool> CheckMaintenance(SocketSlashCommand command){
        if(await AzureManager.GetBoolDefaultFalse("Updatefire","maintenance","bot","rebuilding")){
            var timeRaw = await AzureManager.GetTableEntity("Updatefire","maintenance","bot","time");
            int time = (int?)timeRaw ?? 5;
            await SendSlashReplyAsync($"Bot undergoing maintenance]\n[Try again in {time} minutes",command);
            return true;
        }
        return false;
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
}

public enum Game{
        NONE,
        SCP,
        MINECRAFT,
        GMOD
}

public interface IServer<TSelf> 
    where TSelf : IServer<TSelf>
{
    // Static abstract method to “create” an instance of TSelf
    static abstract Task<TSelf?> CreateInterfaceAsync(string name);

    Task<bool> StartServerAsync(Func<string, Task> messageSenderCallback);
    Task<bool> StopServerAsync(Func<string, Task> messageSenderCallback);
    Task<bool> ReconnectAsync(Func<string, Task> SendMessage);
    string PublicIp { get; }
}
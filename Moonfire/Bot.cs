using Moonfire.Interfaces;
using Moonfire.TableEntities;
using Moonfire.Workers;
using System.Collections.Concurrent;

namespace Moonfire;

public class Bot(string token, DiscordSocketConfig? config = null, List<Command>? _commands = null) : BotBase(token,config,_commands)
{
    public readonly ConcurrentDictionary<ulong, IServerWorker.InterfacePair<SCPInterface>> scpIPairs = [];
    public readonly ConcurrentDictionary<ulong, IServerWorker.InterfacePair<MCInterface>> mcIPairs = [];
    
    // Uncomment to do initial population of commands
    /*protected async override Task ClientReadyHandler(){
        await PopulateCommandsAsync(ownerServerId);
        return;
    }*/

    protected override Task SlashCommandHandler(SocketSlashCommand command){
        //ensure initial reply is sent to avoid timeout
        _ = Task.Run(async () => {await DI.SendInitialSlashReplyAsync("Handling Command",command);});

        //gets task associated with command parameters
        var commandTask = CommandSorter.GetTask(command,this);

        //run returned task as background task with crash logging
        _ = Task.Run(async () => {
            try{
                await commandTask;
            }catch(Exception e){
                //log here to prevent silent failure of background task
                await Console.Out.WriteLineAsync($"{e}");
            }
        });

        return Task.CompletedTask;
    }

    //tasks to run before connection to discord
    protected async override Task PreStartupTasks(){
        await ReconnectTaskAsync();
    }

    //refills concurrent dictionaries after restart
    private async Task ReconnectTaskAsync(){
        var runningServers = await TableManager.QueryTableAsync<ServerEntity>($"{nameof(Moonfire)}Servers",e=>e.IsRunning);

        var reconnectionTasks = new List<Task>();

        foreach(var server in runningServers){
            if(server.RowKey==null || server.PartitionKey==null) continue;

            var guidString = server.PartitionKey;
            var guid = ulong.TryParse(guidString,out var result) ? result : 0;
            var serverType = server.RowKey;

            _ = Console.Out.WriteLineAsync($"Recreating {guid}:{serverType}");
            var reconnectTask = Task.Run(async () =>{
                //create and validate interface creation
                IServerBase? serverInterface = serverType switch
                    {
                        "scp" => await SCPInterface.CreateInterfaceAsync(guidString),
                        "mc" => await MCInterface.CreateInterfaceAsync(guidString),
                        _ => null
                    };
                if(serverInterface is null) throw new($"{guid}:{serverType}:Interface Creation Failure");

                //add interface to correct dictionary
                switch (serverType){
                    case "scp":
                        _ = Console.Out.WriteLineAsync($"Added scp interface at {guid}");
                        scpIPairs.TryAdd(guid,new((SCPInterface)serverInterface,IServerWorker.WorkingFlag.NONE));
                        break;
                    case "mc":
                        _ = Console.Out.WriteLineAsync($"Added mc interface at {guid}");
                        mcIPairs.TryAdd(guid,new((MCInterface)serverInterface,IServerWorker.WorkingFlag.NONE));
                        break;
                    default:
                        _ = Console.Out.WriteLineAsync($"{guid}:{serverType}Failed to add to dictionary");
                        break;
                }

                //reconnect to server
                var success = await serverInterface.StartServerAsync((a) => _ = Console.Out.WriteLineAsync($"{a}"));
                if(!success) throw new($"{guid}:{serverType}:Startup Failure");
            });

            reconnectionTasks.Add(reconnectTask);
        }

        try{
            await Task.WhenAll(reconnectionTasks);
        } catch (Exception e) {
            await Console.Out.WriteLineAsync($"{nameof(Bot)}:{ReconnectTaskAsync}:Reconnection Failed\n{e}");

            //attempt to reset server entities to a recoverable state
            foreach(var server in runningServers){
                server.IsRunning = false;
                await TableManager.StoreITableEntity
                (
                    $"{nameof(Moonfire)}Servers",
                    server
                );
            }

            return;
        }
    }

}

public enum Game{
        NONE,
        SCP,
        MINECRAFT
}
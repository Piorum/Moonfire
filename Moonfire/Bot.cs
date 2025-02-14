using Moonfire.Interfaces;
using Moonfire.TableEntities;
using Moonfire.Workers;
using Moonfire.Sorters;
using Moonfire.Types.Json;
using Moonfire.Types.Discord;
using System.Collections.Concurrent;
using AzureAllocator.Managers;

namespace Moonfire;

public class Bot(string token, DiscordSocketConfig? config = null, List<MoonfireCommand>? _commands = null) : BotBase(token,config,_commands)
{
    internal readonly ConcurrentDictionary<ulong, IServerWorker.InterfacePair<SCPInterface>> scpIPairs = [];
    internal readonly ConcurrentDictionary<ulong, IServerWorker.InterfacePair<MCInterface>> mcIPairs = [];
    
    // Uncomment to do initial population of commands
    /*protected async override Task ClientReadyHandler(){
        await PopulateCommandsAsync(ownerServerId);
        return;
    }*/

    protected override Task SlashCommandHandler(SocketSlashCommand command){
        var taskHandler = Task.Run(async () => {
            (var commandTask,var responseType) = await CommandSorter.GetTask(command,this);

            //ensure initial reply is sent for basic response types
            if(responseType is CommandSorter.ResponseType.BASIC)
                await DI.SendResponseAsync("Handling Command",command);

            await commandTask;
        });

        //run task as background task with crash logging
        _ = Ext.RunUnderTry(taskHandler);

        return Task.CompletedTask;
    }

    protected override Task ModalSubmissionHandler(SocketModal modal){
        var taskHandler = Task.Run(async () => {
            var modalTask = await ModalSorter.GetTask(modal);

            await modalTask;
        });

        //run task as background task with crash logging
        _ = Ext.RunUnderTry(taskHandler);

        return Task.CompletedTask;
    }

    protected override Task ComponentHandler(SocketMessageComponent component){
        var taskHandler = Task.Run(async () => {
            var componentTask = await ComponentSorter.GetTask(component);

            await componentTask;
        });

        //run task as background task with crash logging
        _ = Ext.RunUnderTry(taskHandler);

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

        try{
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
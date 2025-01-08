using System.Collections.Concurrent;
using Moonfire.Interfaces;

namespace Moonfire.Workers;

public static class IServerWorker
{
    public record InterfacePair<TServer>(TServer? Interface, WorkingFlag WorkingFlag)
        where TServer : class, IServer<TServer>;

    public static async Task StartTaskAsync<TServer>(ConcurrentDictionary<ulong, InterfacePair<TServer>> serverIPairs, SocketSlashCommand command)
        where TServer : class, IServer<TServer>
    {
        var cts = new CancellationTokenSource();

        //main start task
        var startTask = Task.Run(async () => {
            await DI.SendSlashReplyAsync("Starting Server",command);

            if(await CheckMaintenance(command)) return;
            if(await CheckWorking(command,serverIPairs)) return;

            //convert from ulong? to ulong
            ulong guid = command.GuildId ?? 0;

            //if no value, or value = null
            (var serverPair, var found) = await PullIServerPairAsync(serverIPairs,guid);
            if(!found){

                //set working flag
                serverPair = serverPair is null ? 
                    new(null,WorkingFlag.STARTING) : 
                    serverPair with {WorkingFlag = WorkingFlag.STARTING};
                serverIPairs[guid] = serverPair;

                await DI.SendSlashReplyAsync("Provisioning Server",command);

                //create and add interface object
                serverPair = serverPair with {Interface = await TServer.CreateInterfaceAsync($"{guid}",cts.Token)};
                serverIPairs[guid] = serverPair;
            }
            //check if provisioning failed
            if(serverPair is null || serverPair.Interface is null){
                await DI.SendSlashReplyAsync("Provisioning Failure",command);
                //removes interface and working flag
                serverIPairs.TryRemove(guid,out _);
                return;
            }

            //start server
            var success = await serverPair.Interface.StartServerAsync((string a)=>DI.SendSlashReplyAsync(a,command),cts.Token);
            if(success) await DI.SendSlashReplyAsync($"Started Server at '{serverPair.Interface.PublicIp}'",command);

            //remove starting lock
            serverIPairs[guid] = serverPair with {WorkingFlag = WorkingFlag.NONE};
            
        },cts.Token);

        //cleanup task run after timeout
        Lazy<Task> cleanupTask = new(() => Task.Run(async () => {
            _ = Console.Out.WriteLineAsync($"{nameof(StartTaskAsync)}:Startup Timed Out");
            //send alert here

            await DI.SendSlashReplyAsync($"Server Startup Failed]\n   [Try Again Soon", command);
            ulong guid = command.GuildId ?? 0;

            (var serverPair, var found) = await PullIServerPairAsync(serverIPairs,guid);
            if (!found || serverPair is null || serverPair.Interface is null)
                return;
            else
                serverIPairs[guid] = serverPair with {WorkingFlag = WorkingFlag.RECOVERING};

            //This needs to run to clean up resources
            var stopcts = new CancellationTokenSource();
            await Ext.TimeoutTask
            (
                serverPair.Interface.StopServerAsync((string a) => Console.Out.WriteLineAsync(a), stopcts.Token), 
                new(0, 10, 0), 
                stopcts
            );

            //fully cleans up interface pair
            serverIPairs.TryRemove(guid,out _);

        },cts.Token));

        await Ext.TimeoutTask(startTask,cleanupTask,new TimeSpan(0,5,0),cts);
    }

    public static async Task StopTaskAsync<TServer>(ConcurrentDictionary<ulong, InterfacePair<TServer>> serverIPairs, SocketSlashCommand command)
        where TServer : class, IServer<TServer>
    {
        var cts = new CancellationTokenSource();

        var stopTask = Task.Run(async () => {
            await DI.SendSlashReplyAsync("Stopping SCP Server",command);

            if(await CheckMaintenance(command)) return;
            if(await CheckWorking(command,serverIPairs)) return;

            var guid = command.GuildId ?? 0;

            (var serverPair, var found) = await PullIServerPairAsync(serverIPairs,guid);
            if(!found){
                await DI.SendSlashReplyAsync("No Server Found",command);
                return;
            } else if (serverPair is null || serverPair.Interface is null){
                await DI.SendSlashReplyAsync("Server Was Null",command);
                serverIPairs.TryRemove(guid,out _);
                return;
            }
            serverIPairs[guid] = serverPair with {WorkingFlag = WorkingFlag.STOPPING};
            var result = await serverPair.Interface.StopServerAsync((string a)=>DI.SendSlashReplyAsync(a,command),cts.Token);

            //clean up pair
            serverIPairs.TryRemove(guid,out _);

            if(result) await DI.SendSlashReplyAsync("Stopped Server",command);
            else throw new("Server Failed To Stop");

        },cts.Token);

        Lazy<Task> cleanupTask = new(() => Task.Run(async () => {
            _ = Console.Out.WriteLineAsync($"{nameof(StopTaskAsync)}:Stopping Timed Out");
            //send alert here

            var guid = command.GuildId ?? 0;

            await DI.SendSlashReplyAsync($"Server Stopping Failure]\n     [Try Again Soon",command);
            serverIPairs.TryRemove(guid,out _);

        },cts.Token));

        await Ext.TimeoutTask(stopTask,cleanupTask,new TimeSpan(0,10,0),cts);
    }

    private static async Task<bool> CheckMaintenance(SocketSlashCommand command){
        if(await TableManager.GetBoolDefaultFalse("Updatefire","maintenance","bot","rebuilding")){
            var timeRaw = await TableManager.GetTableEntity("Updatefire","maintenance","bot","time");
            int time = (int?)timeRaw ?? 5;
            await DI.SendSlashReplyAsync($"Bot undergoing maintenance]\n  [Try again in {time} minutes",command);
            return true;
        }
        return false;
    }

    private static async Task<bool> CheckWorking<TServer>(SocketSlashCommand command, ConcurrentDictionary<ulong, InterfacePair<TServer>> serverIPairs)
        where TServer : class, IServer<TServer>
    {
        var guid = command.GuildId ?? 0;

        serverIPairs.TryGetValue(guid,out var serverIPair);

        switch (serverIPair?.WorkingFlag){
            case WorkingFlag.STARTING:
                await DI.SendSlashReplyAsync("Server is Starting",command);
                return true;
            case WorkingFlag.STOPPING:
                await DI.SendSlashReplyAsync("Server is Stopping",command);
                return true;
            case WorkingFlag.RECOVERING:
                await DI.SendSlashReplyAsync("Server is Recovering From Failure",command);
                return true;
            default:
                return false;
        }
    }

    private static Task<(InterfacePair<TServer>?,bool)> PullIServerPairAsync<TServer>(ConcurrentDictionary<ulong, InterfacePair<TServer>> serverPairs, ulong guid)
        where TServer : class, IServer<TServer>
    {
        if(!serverPairs.TryGetValue(guid,out var serverPair) || serverPair is null)
            return Task.FromResult<(InterfacePair<TServer>?,bool)>((null,false));
        return Task.FromResult<(InterfacePair<TServer>?,bool)>((serverPair,true));
    }

    public enum WorkingFlag{
        NONE,
        STARTING,
        STOPPING,
        RECOVERING
    }
}

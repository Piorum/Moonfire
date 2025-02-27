using System.Collections.Concurrent;
using Moonfire.Interfaces;
using Moonfire.Credit;
using AzureAllocator.Managers;

namespace Moonfire.Workers;

public static class IServerWorker
{
    public record InterfacePair<TServer>(TServer? Interface, WorkingFlag WorkingFlag)
        where TServer : class, IServer<TServer>;

    public static async Task StartTaskAsync<TServer>(ConcurrentDictionary<ulong, InterfacePair<TServer>> serverIPairs, SocketSlashCommand command)
        where TServer : class, IServer<TServer>
    {
        var cts = new CancellationTokenSource();

        //check for updating status
        if (await TServer.Updating(cts.Token)){
            await DI.ModifyResponseAsync("Game is updating - Try Again Soon",command);
            return;
        }
        //check for balance
        if (await CreditService.OutOfBalance(command.GuildId ?? 0)){
            await DI.ModifyResponseAsync("Not Enough Credit",command);
            return;
        }

        //main start task
        var startTask = Task.Run(async () => {
            await DI.ModifyResponseAsync("Starting Server",command);

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

                await DI.ModifyResponseAsync("Provisioning Server",command);

                //create and add interface object
                serverPair = serverPair with {Interface = await TServer.CreateInterfaceAsync($"{guid}",cts.Token)};
                serverIPairs[guid] = serverPair;
            }
            //check if provisioning failed
            if(serverPair is null || serverPair.Interface is null){
                await DI.ModifyResponseAsync("Provisioning Failure",command);
                //removes interface and working flag
                serverIPairs.TryRemove(guid,out _);
                return;
            }

            //start server
            var success = await serverPair.Interface.StartServerAsync((string a)=>DI.ModifyResponseAsync(a,command), cts.Token);
            
            if(success) 
                await DI.ModifyResponseAsync($"Started Server at '{serverPair.Interface.PublicIp}']\n[Starting Credit '{Math.Round(await CreditTableManager.GetCredit($"{guid}"), 3)}'",command);
            else
                throw new OperationCanceledException(); //cancels task, runs cleanup task

            //remove starting lock
            serverIPairs[guid] = serverPair with {WorkingFlag = WorkingFlag.NONE};
            
        },cts.Token);

        //cleanup task run after timeout or failure
        Lazy<Task> cleanupTask = new(() => Task.Run(async () => {
            _ = Console.Out.WriteLineAsync($"{nameof(StartTaskAsync)}:Startup Timed Out");
            await DI.ModifyResponseAsync($"Startup Failure", command);
            //send alert here

            //output error on timeout, otherwise interface give more verbose failure message
            if(cts.Token.IsCancellationRequested){
                await DI.ModifyResponseAsync($"Server Startup Failed]\n   [Try Again Soon", command);
            }

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

            await DI.ModifyResponseAsync($"Interface Reset", command);

        },cts.Token));

        await Ext.TimeoutTask(startTask,cleanupTask,new TimeSpan(0,5,0),cts);
    }

    public static async Task StopTaskAsync<TServer>(ConcurrentDictionary<ulong, InterfacePair<TServer>> serverIPairs, SocketSlashCommand command)
        where TServer : class, IServer<TServer>
    {
        var cts = new CancellationTokenSource();

        var stopTask = Task.Run(async () => {
            await DI.ModifyResponseAsync("Stopping SCP Server",command);

            if(await CheckMaintenance(command)) return;
            if(await CheckWorking(command,serverIPairs)) return;

            var guid = command.GuildId ?? 0;

            (var serverPair, var found) = await PullIServerPairAsync(serverIPairs,guid);
            if(!found){
                await DI.ModifyResponseAsync("No Server Found",command);
                return;
            } else if (serverPair is null || serverPair.Interface is null){
                await DI.ModifyResponseAsync("Server Was Null",command);
                serverIPairs.TryRemove(guid,out _);
                return;
            }
            serverIPairs[guid] = serverPair with {WorkingFlag = WorkingFlag.STOPPING};
            var result = await serverPair.Interface.StopServerAsync((string a)=>DI.ModifyResponseAsync(a,command),cts.Token);

            //clean up pair
            serverIPairs.TryRemove(guid,out _);

            if(result) await DI.ModifyResponseAsync($"Stopped Server]\n[Credit Remaining '{Math.Round(await CreditTableManager.GetCredit($"{guid}"), 3)}'",command);
            else throw new("Server Failed To Stop");

        },cts.Token);

        Lazy<Task> cleanupTask = new(() => Task.Run(async () => {
            _ = Console.Out.WriteLineAsync($"{nameof(StopTaskAsync)}:Stopping Timed Out");
            //send alert here

            var guid = command.GuildId ?? 0;

            await DI.ModifyResponseAsync($"Server Stopping Failure]\n     [Try Again Soon",command);
            serverIPairs.TryRemove(guid,out _);

        },cts.Token));

        await Ext.TimeoutTask(stopTask,cleanupTask,new TimeSpan(0,10,0),cts);
    }

    private static async Task<bool> CheckMaintenance(SocketSlashCommand command){
        if(await TableManager.GetBoolDefaultFalse("Updatefire","maintenance","bot","rebuilding")){
            var timeRaw = await TableManager.GetTableEntity("Updatefire","maintenance","bot","time");
            int time = (int?)timeRaw ?? 5;
            await DI.ModifyResponseAsync($"Bot undergoing maintenance]\n  [Try again in {time} minutes",command);
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
                await DI.ModifyResponseAsync("Server is Starting",command);
                return true;
            case WorkingFlag.STOPPING:
                await DI.ModifyResponseAsync("Server is Stopping",command);
                return true;
            case WorkingFlag.RECOVERING:
                await DI.ModifyResponseAsync("Server is Recovering From Failure",command);
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

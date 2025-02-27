using System.Diagnostics;
using Moonfire.TableEntities;
using Moonfire.Types.Json;
using Moonfire.ConfigHandlers;
using AzureAllocator.Managers;
using Moonfire.Credit;

namespace Moonfire.Interfaces;

public class SCPInterface : IServer<SCPInterface>, IServerBase
{   
    public string PublicIp => vm?.ip ?? "Not Found";
    private string Name => $"{vm?.rgName ?? "RG Name Not Found"}:{vm?.vmName ?? "VM Name Not Found"}:";
    private bool started = false;
    private Process? sshClient;
    private AzureVM? vm;
    private SCPSettings? scpSettings;

    private SCPInterface(){}

    public static async Task<SCPInterface?> CreateInterfaceAsync(string guildId, CancellationToken token = default){
        //create empty new interface object
        SCPInterface obj = new();

        //game settings/vm building tasks
        var gameSettingsTask = SCPConfigHandler.GetGameSettings(guildId,token);

        //gets already started flag, defaults to false if no entry, if not alreadyStarted : else
        var alreadyStartedEntity = await TableManager.GetITableEntity<ServerEntity>
            (
                $"{nameof(Moonfire)}Servers",
                guildId,
                "scp",
                token
            );
        alreadyStartedEntity ??= new();

        Task<AzureVM?> buildVMTask;
        if(alreadyStartedEntity.IsRunning && alreadyStartedEntity.AzureRegion is not null && alreadyStartedEntity.AzureVmName is not null && alreadyStartedEntity.RgName is not null){
            buildVMTask = Task.Run(async () => 
            {
                return await AzureManager.Allocate(await SCPConfigHandler.GetHardwareSettings(guildId,token), alreadyStartedEntity.AzureRegion, alreadyStartedEntity.AzureVmName, alreadyStartedEntity.RgName, $"SCPVM", token);
            },token);
        } else {
            buildVMTask = Task.Run(async () => 
            {
                return await AzureManager.Allocate(await SCPConfigHandler.GetHardwareSettings(guildId,token), $"{guildId}", $"SCPVM", token);
            },token);
        }

        //await tasks
        await Task.WhenAll(gameSettingsTask,buildVMTask);

        //assign results
        obj.scpSettings = await gameSettingsTask;
        obj.vm = await buildVMTask;

        //check for failure
        if(obj.vm is null){
            _ = Console.Out.WriteLineAsync($"{nameof(SCPInterface)}: Azure Allocation Failed");
            return null;
        }

        //build ssh client and assign result
        obj.sshClient = await AzureVM.BuildSshClient(obj.vm);

        //return complete interface object
        return obj;
    }

    public async Task<bool> StartServerAsync(Func<string, Task> SendMessage, CancellationToken token = default){
        var fN = nameof(StartServerAsync); //used in logging

        if(vm==null){
            await Log(fN,"VM does not exist");
            await SendMessage("Broken Interface - Resetting - Try Again Soon");
            return false;
        }
        if(sshClient==null){
            await Log(fN,"sshClient does not exist");
            await SendMessage("Broken Interface - Resetting - Try Again Soon");
            return false;
        }
        if(started){
            await Log(fN,"Game Already Started");
            return true;
        }
        if(await Updating(token:token)){
            await Log(fN,"Game is updating");
            await SendMessage("Game is updating - Resetting - Try Again Soon");
            return false;
        }

        //gets already started flag, defaults to false if no entry, if not alreadyStarted : else
        var alreadyStartedEntity = await TableManager.GetITableEntity<ServerEntity>
            (
                $"{nameof(Moonfire)}Servers",
                vm.Guid.ToString()??"",
                "scp",
                token
            );
        alreadyStartedEntity ??= new();

        if(!alreadyStartedEntity.IsRunning){
            //runs setup, if fails returns failure
            if(!await Setup(SendMessage,token)) return false;
        } else {
            return await ReconnectAsync(SendMessage, token);
        }

        TaskCompletionSource<bool> _heartbeatReceived = new();
        var configTransferFailed = false;

        //Read the output asynchronously to console
        await Task.Run(async () => {
            while(!_heartbeatReceived.Task.IsCompleted){
                token.ThrowIfCancellationRequested();
                string? output = await sshClient.StandardOutput.ReadLineAsync();
                if(output!=null){
                    Console.WriteLine(output);
                    if(output.Contains("Received first heartbeat")) _heartbeatReceived.TrySetResult(true);
                    if(output.Contains("accept the EULA")) {_ = vm.ConsoleDirect("yes",sshClient);configTransferFailed=true;}
                    if(output.Contains("edit that configuration?")) _ = vm.ConsoleDirect("keep",sshClient);
                    if(output.Contains("save the configuration")) _ = vm.ConsoleDirect("this",sshClient);
                }
            }
            _ = Log(fN,"sshClient EndOfStream encountered or halted");
        },token);

        //Waits for heartbeat report to continue
        await _heartbeatReceived.Task;
        if(configTransferFailed) _ = Log(fN,"Config Transfer Failed");
        _ = Log(fN,"SCP Server Started");

        //set already started flag true
        await TableManager.StoreITableEntity($"{nameof(Moonfire)}Servers",new ServerEntity(vm.Guid.ToString()??"","scp",true,vm.azureRegion,vm.azureVmName,vm.rgName),token);
        started = true;

        //register to credit service
        await CreditService.RegisterClient(vm.Guid ?? 0, Game.SCP, vm.HourlyCost, "SCP Server", token:token);

        return true;
    }

    public async Task<bool> StopServerAsync(Func<string, Task> SendMessage,CancellationToken token = default){
        var fN = nameof(StopServerAsync); //used in logging

        scpSettings = null;

        if(sshClient!=null && vm!=null && vm.Connected){
            sshClient.Close();
            _ = Log(fN,"SSH Connection Stopped");
            sshClient.Dispose();
        }

        _ = SendMessage($"Deprovisioning Server");
        if(vm!=null){

            //set already started flag false
            await TableManager.StoreITableEntity($"{nameof(Moonfire)}Servers",new ServerEntity(vm.Guid.ToString()??string.Empty,"scp",false),token);

            //unregister from credit service
            await CreditService.UnregisterClient(vm.Guid ?? 0, Game.SCP, token:token);

            await vm.Deallocate(token);
        }

        _ = Log(fN,"SCPInterface Reset");
        vm = null;

        return true;
    }

    public static async Task<bool> Updating(CancellationToken token = default)
    {
        return await TableManager.GetBoolDefaultFalse("Updatefire","updating","game","scp",token);
    }

    public async Task<bool> ReconnectAsync(Func<string, Task> SendMessage, CancellationToken token = default){
        _ = SendMessage("Reconnecting to Server");
        _ = Log(nameof(ReconnectAsync),"Reconnecting to Server");
        
        if(vm==null){
            _ = SendMessage("VM Null - Broken Interface");
            _ = Log(nameof(ReconnectAsync),"VM Null - Broken Interface");
            await StopServerAsync(SendMessage,token); //deleting interface
            return false;
        }
        if(sshClient==null){
            _ = SendMessage("sshClient Null - Broken Interface");
            _ = Log(nameof(ReconnectAsync),"sshClient Null - Broken Interface");
            await StopServerAsync(SendMessage,token); //deleting interface
            return false;
        }
        if(!vm.Started){
            _ = SendMessage("VM Offline - Reset Interface - Try Again");
            _ = Log(nameof(ReconnectAsync),"VM Offline - Reset Interface");

            //set already started flag false
            await TableManager.StoreITableEntity($"{nameof(Moonfire)}Servers",new ServerEntity(vm.Guid.ToString()??string.Empty,"scp",false),token);

            await StartServerAsync(SendMessage, token);
            return true;
        }
        await vm.StartSSH(sshClient);

        await CreditService.RegisterClient(vm.Guid ?? 0, Game.SCP, vm.HourlyCost, "SCP Server", token:token);

        return true;
    }

    private async Task<bool> Setup(Func<string, Task> SendMessage, CancellationToken token){
        var fN = nameof(Setup); //used in logging

        if(vm==null){
            await Log(fN,"Server Hardware Null");
            await Log(fN,"VM does not exist");
            return false;
        }
        if(sshClient==null){
            await Log(fN,"sshClient does not exist");
            await SendMessage("sshClient does not exist");
            return false;
        }

        //partition, format, mount, chmod
        var script = "";
        void f(string a) => script += a + Environment.NewLine;

        f(@"#!/bin/bash");
        f(@"while [ ! -b /dev/sda ]; do");
        f(@"    :");
        f(@"done");

        f(@"export HOME=/home/azureuser");

        f(@"if [ ! -d ~/scpcontainer ]; then");

        f($"    curl -o ~/scpcontainer.tar.gz '{await AzureVM.GetDownloadSas(@"scpcontainer",@"scpcontainer.tar.gz")}'");

        f(@"    tar -xvzf ~/scpcontainer.tar.gz -C ~/");
        f(@"    mkdir -p ~/.config");
        f(@"    ln -s ~/scpcontainer/config ~/.config/SCP\ Secret\ Laboratory");

        f(@"    chmod -R 777 ~/.config");
        f(@"    chmod -R 777 ~/scpcontainer");

        f(@"    echo -e 'Members:\n - SomeId@steam: admin' >> ~/.config/SCP\ Secret\ Laboratory/config/7777/config_remoteadmin.txt");

        if(scpSettings is not null)
            foreach(var adminUser in scpSettings.AdminUsers)
                f($@"    echo -e ' - {adminUser.Id}@steam: {adminUser.Role}' >> ~/.config/SCP\ Secret\ Laboratory/config/7777/config_remoteadmin.txt");
        
        f(@"else");

        f(@"    killall -9 LocalAdmin");

        f(@"fi");

        _ = SendMessage("Setting Up Game Files");
        _ = Log(fN,"Running Setup Script");
        await vm.RunScript(script,token);

        _ = SendMessage("Starting Server");
        _ = Log(fN,"Starting SCP Server");
        await vm.ConsoleDirect(@"cd ~/scpcontainer",sshClient);
        await vm.ConsoleDirect(@"./LocalAdmin 7777",sshClient);

        return true;
    }

    private async Task Log(string funcName, string input) =>
        await Console.Out.WriteLineAsync($"{Name}{funcName}:{input}");
}
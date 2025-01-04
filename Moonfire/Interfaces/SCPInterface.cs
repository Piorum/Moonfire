using System.Diagnostics;
using Newtonsoft.Json;
using Azure.Data.Tables;

namespace Moonfire.Interfaces;

public class SCPInterface : IServer<SCPInterface>
{   
    public string PublicIp => vm?.ip ?? "Not Found";
    private string Name => $"{vm?.rgName ?? "RG Name Not Found"}:{vm?.vmName ?? "VM Name Not Found"}:";
    private bool started = false;
    private Process? sshClient;
    private AzureVM? vm;
    private ScpSettings? scpSettings;

    private SCPInterface(){}

    public static async Task<SCPInterface?> CreateInterfaceAsync(string guildId, CancellationToken token = default){
        SCPInterface obj = new();

        //loading settings
        string tableName = nameof(Moonfire) + "SCP";

        var hardwareSettingsJson = await TableManager.GetTableEntity(tableName,guildId,"config","scphardware",token);

        //if settings are null load and store template settings
        if(hardwareSettingsJson==null){
            _ = Console.Out.WriteLineAsync($"{nameof(SCPInterface)}: No Hardware Settings Found For {guildId} Storing Defaults");

            //use template settings if none are found
            var hardwareTemplatePath = Path.Combine(
                Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "",
                "Config",
                "SCPSettings.json"
            );
            hardwareSettingsJson = await File.ReadAllTextAsync(hardwareTemplatePath,token);

            await TableManager.StoreTableEntity(tableName,guildId,"config","scphardware",hardwareSettingsJson,token);
        }

        //create azure settings object
        var hardwareSettings = await AzureSettings.CreateAsync((string)hardwareSettingsJson);

        //loading game settings
        //attempt to get stored setting
        var gameSettingsJson = await TableManager.GetTableEntity(tableName,guildId,"config","scpgame",token);

        if(gameSettingsJson==null){
            _ = Console.Out.WriteLineAsync($"{nameof(SCPInterface)}: No Game Settings Found For {guildId} Storing Defaults");

            //use template settings if none are found
            gameSettingsJson = JsonConvert.SerializeObject(new ScpSettings());

            await TableManager.StoreTableEntity(tableName,guildId,"config","scpgame",gameSettingsJson,token);
        }

        //log settings
        _ = Console.Out.WriteLineAsync($"{(string)hardwareSettingsJson}");
        _ = Console.Out.WriteLineAsync($"{(string)gameSettingsJson}");

        //deserialize json string into obj
        obj.scpSettings = JsonConvert.DeserializeObject<ScpSettings>((string)gameSettingsJson);

        //allocating vm
        //match RG/VM names in sshClient ProcessStartInfo.Arguments
        obj.vm = await AzureManager.Allocate(hardwareSettings, $"{guildId}RG", $"SCPVM", token);

        if(obj.vm == null){
            _ = Console.Out.WriteLineAsync($"{nameof(SCPInterface)}: Azure Allocation Failed");
            return null;
        }

        obj.sshClient = new()
        {
            StartInfo = await AzureVM.BuildSshClient(guildId,obj.vm)
        };

        return obj;
    }

    public async Task<bool> StartServerAsync(Func<string, Task> SendMessage, CancellationToken token = default){
        var fN = nameof(StartServerAsync); //used in logging

        if(vm==null){
            await Log(fN,"VM does not exist");
            await SendMessage("VM does not exist");
            return false;
        }
        if(sshClient==null){
            await Log(fN,"sshClient does not exist");
            await SendMessage("sshClient does not exist");
            return false;
        }
        if(started){
            await Log(fN,"Game Already Started");
            return true;
        }
        if(await TableManager.GetBoolDefaultFalse("Updatefire","updating","game","scp",token)){
            await Log(fN,"Game is updating");
            await SendMessage("Game is updating - Try again soon");
            return false;
        }

        //gets already started flag, defaults to false if no entry, if not alreadyStarted : else
        if(!await TableManager.GetBoolDefaultFalse(nameof(Moonfire), vm.rgName, "started","scp",token)){
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
        await TableManager.StoreTableEntity(nameof(Moonfire),vm.rgName,"started","scp",true,token);
        started = true;
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
            await TableManager.StoreTableEntity(nameof(Moonfire),vm.rgName,"started","scp",false,token);

            await vm.Deallocate(token);
        }

        _ = Log(fN,"SCPInterface Reset");
        vm = null;

        return true;
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
            await TableManager.StoreTableEntity(nameof(Moonfire),vm.rgName,"started","scp",false,token);

            await StartServerAsync(SendMessage, token);
            return true;
        }
        await vm.StartSSH(sshClient);

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

public class ScpSettings{
    [JsonProperty(nameof(id))]
    public List<ulong> id = [];

    [JsonProperty(nameof(role))]
    public List<string> role = [];
}
using System.Diagnostics;
using Newtonsoft.Json;
using Azure.Data.Tables;
using Azure.ResourceManager.Compute;

namespace Sunfire.Interfaces;

public class SCPInterface : IServer<SCPInterface>
{   
    public string PublicIp => vm?.ip ?? "Not Found";
    private string Name => $"{vm?.rgName ?? "RG Name Not Found"}:{vm?.vmName ?? "VM Name Not Found"}:";
    private bool started = false;
    private Process? sshClient;
    private AzureVM? vm;
    private ScpSettings? scpSettings;

    private SCPInterface(){}

    public static async Task<SCPInterface?> CreateInterfaceAsync(string guildId){
        SCPInterface obj = new();


        //loading settings
        var tableClient = await TableManager.GetTableClient(nameof(Sunfire) + "SCP");

        //loading hardware settings
        //attempt to get stored settings
        var hardwareSettingsJson = (await TableManager.GetTableEntity(tableClient,guildId,"config"))?["scphardware"];

        //if settings are null load and store template settings
        if(hardwareSettingsJson==null){
            _ = Console.Out.WriteLineAsync($"{nameof(SCPInterface)}: No Hardware Settings Found For {guildId} Storing Defaults");
            //use template settings if none are found
            var hardwareTemplatePath = Path.Combine(
                Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "",
                "Config",
                "SCPSettings.json"
            );
            hardwareSettingsJson = await File.ReadAllTextAsync(hardwareTemplatePath);
            await TableManager.StoreTableEntity(tableClient,new TableEntity(guildId,"config"){
                { "scphardware", (string)hardwareSettingsJson }
            });
        }

        //create settings object
        var hardwareSettings = await AzureSettings.CreateAsync((string)hardwareSettingsJson,true);

        //loading game settings
        //attempt to get stored setting
        var gameSettingsJson = (await TableManager.GetTableEntity(tableClient,guildId,"config"))?["scpgame"];

        if(gameSettingsJson==null){
            _ = Console.Out.WriteLineAsync($"{nameof(SCPInterface)}: No Game Settings Found For {guildId} Storing Defaults");
            //use default settings if none is found
            gameSettingsJson = JsonConvert.SerializeObject(new ScpSettings());
            await TableManager.StoreTableEntity(tableClient,new TableEntity(guildId,"config"){
                { "scpgame", (string)gameSettingsJson }
            });
        }

        _ = Console.Out.WriteLineAsync($"{(string)hardwareSettingsJson}");
        _ = Console.Out.WriteLineAsync($"{(string)gameSettingsJson}");
        //deserialize json string into obj
        obj.scpSettings = JsonConvert.DeserializeObject<ScpSettings>((string)gameSettingsJson);

        //allocating vm
        //match RG/VM names in sshClient ProcessStartInfo.Arguments
        obj.vm = await AzureManager.Allocate(hardwareSettings, $"{guildId}RG", $"SCPVM");

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

    public async Task<bool> StartServerAsync(Func<string, Task> SendMessage){
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
        if(await TableManager.GetBoolDefaultFalse("Updatefire","updating","game","scp")){
            await Log(fN,"Game is updating");
            await SendMessage("Game is updating - Try again soon");
            return false;
        }

        if(!await GetAlreadyStarted(vm)){
            if(!await Setup(SendMessage)) return false;
        } else {
            return await ReconnectAsync(SendMessage);
        }

        TaskCompletionSource<bool> _heartbeatReceived = new();
        var configTransferFailed = false;

        //Read the output asynchronously to console
        _ = Task.Run(async () => {
            while(!sshClient.StandardOutput.EndOfStream){
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
        });

        //Waits for heartbeat report to continue
        await _heartbeatReceived.Task;
        if(configTransferFailed) _ = Log(fN,"Config Transfer Failed");
        _ = Log(fN,"SCP Server Started");

        await SetAlreadyStarted(true,vm);

        started = true;
        return true;
    }

    public async Task<bool> StopServerAsync(Func<string, Task> SendMessage){
        var fN = nameof(StopServerAsync); //used in logging

        scpSettings = null;

        if(sshClient!=null && vm!=null && vm.Connected){
            sshClient.Close();
            _ = Log(fN,"SSH Connection Stopped");
            sshClient.Dispose();
        }

        _ = SendMessage($"Deprovisioning Server");
        if(vm!=null){

            await SetAlreadyStarted(false,vm);

            await vm.Deallocate();
        }

        _ = Log(fN,"SCPInterface Reset");
        vm = null;

        return true;
    }

    public async Task<bool> ReconnectAsync(Func<string, Task> SendMessage){
        _ = SendMessage("Reconnecting to Server");
        _ = Log(nameof(ReconnectAsync),"Reconnecting to Server");
        
        if(vm==null){
            _ = SendMessage("VM Null - Broken Interface");
            _ = Log(nameof(ReconnectAsync),"VM Null - Broken Interface");
            await StopServerAsync(SendMessage); //deleting interface
            return false;
        }
        if(sshClient==null){
            _ = SendMessage("sshClient Null - Broken Interface");
            _ = Log(nameof(ReconnectAsync),"sshClient Null - Broken Interface");
            await StopServerAsync(SendMessage); //deleting interface
            return false;
        }
        if(!vm.Started){
            _ = SendMessage("VM Offline - Reset Interface - Try Again");
            _ = Log(nameof(ReconnectAsync),"VM Offline - Reset Interface");

            await SetAlreadyStarted(false,vm);

            await StartServerAsync(SendMessage);
            return true;
        }
        await vm.StartSSH(sshClient);

        //restart log output
        _ = Task.Run(async () => {
            while(!sshClient.StandardOutput.EndOfStream){
                string? output = await sshClient.StandardOutput.ReadLineAsync();
                if(output!=null) _ = Console.Out.WriteLineAsync(output);
            }
            _ = Log(nameof(ReconnectAsync),"sshClient EndOfStream encountered or halted");
        });

        return true;
    }

    private async Task<bool> Setup(Func<string, Task> SendMessage){
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
        await vm.RunScript(script);

        _ = SendMessage("Starting Server");
        _ = Log(fN,"Starting SCP Server");
        await vm.ConsoleDirect(@"cd ~/scpcontainer",sshClient);
        await vm.ConsoleDirect(@"./LocalAdmin 7777",sshClient);

        return true;
    }

    private static async Task SetAlreadyStarted(bool value, AzureVM vm){
        var tableClient = await TableManager.GetTableClient(nameof(Sunfire));
        await TableManager.StoreTableEntity(tableClient,vm.rgName,"started","scp",value);
        tableClient = null;
    }

    private static async Task<bool> GetAlreadyStarted(AzureVM vm){
        var tableClient = await TableManager.GetTableClient(nameof(Sunfire));
        var entity = await TableManager.GetTableEntity(tableClient, vm.rgName, "started","scp");
        tableClient = null;
        //default to false if null
        //if not null alreadyStarted = entity
        bool alreadyStarted = entity != null && (bool)entity;
        return alreadyStarted;
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
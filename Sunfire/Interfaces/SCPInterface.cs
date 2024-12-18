using System.Diagnostics;
using Newtonsoft.Json;
using Azure.Data.Tables;

namespace Sunfire.Interfaces;

public class SCPInterface
{   
    private bool _started = false;
    public string PublicIp => _started ? vm?.ip ?? "Not Found" : "Server Not Started";
    private string Name => $"{vm?.rgname ?? "RG Name Not Found"}:{vm?.name ?? "VM Name Not Found"}:";
    private Process? sshClient;
    private AzureVM? vm;
    private ScpSettings? scpSettings;

    private SCPInterface(){}

    public static async Task<SCPInterface?> CreateInterface(string guildId){
        SCPInterface obj = new();


        //loading settings
        var tableClient = await AzureManager.GetTableClient(nameof(Sunfire));

        //loading hardware settings
        //attempt to get stored settings
        var hardwareSettingsJson = (await AzureManager.GetTableEntity(tableClient,guildId,"config"))?["scphardware"];

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
            await AzureManager.StoreTableEntity(tableClient,new TableEntity(guildId,"config"){
                { "scphardware", (string)hardwareSettingsJson }
            });
        }

        //create settings object
        var hardwareSettings = await AzureSettings.CreateAsync((string)hardwareSettingsJson,true);

        //loading game settings
        //attempt to get stored setting
        var gameSettingsJson = (await AzureManager.GetTableEntity(tableClient,guildId,"config"))?["scpgame"];

        if(gameSettingsJson==null){
            _ = Console.Out.WriteLineAsync($"{nameof(SCPInterface)}: No Game Settings Found For {guildId} Storing Defaults");
            //use default settings if none is found
            gameSettingsJson = JsonConvert.SerializeObject(new ScpSettings());
            await AzureManager.StoreTableEntity(tableClient,new TableEntity(guildId,"config"){
                { "scpgame", (string)gameSettingsJson }
            });
        }

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
            StartInfo = new ProcessStartInfo
            {
                FileName = "ssh",
                //RG/Key names to allocation names, Use vm.ip, publicIp returns 'Server Not Started'
                Arguments = $"-o StrictHostKeyChecking=no -i {Environment.GetEnvironmentVariable("CONFIG_PATH")}/Ssh/{guildId}RG/SCPVM-Key.pem azureuser@{obj.vm.ip}",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        return obj;
    }

    public async Task StartServerAsync(Func<string, Task> SendMessage){
        var fN = nameof(StartServerAsync); //used in logging

        try{
            if(vm==null) throw new("VM does not exist");
            if(sshClient==null) throw new("sshClient does not exist");
            if (_started) throw new("Process should already be started");
        }
        catch (Exception e){
            _ = Log(fN,$"{e}");
            _ = SendMessage($"{e}");
            return;
        }

        _ = SendMessage("Setting Up Game Files");
        _ = Log(fN,"Setting up Disk");
        await SetupDisk();

        _ = Log(fN,"Downloading Blob");
        await vm.DownloadBlob(@"scpcontainer",@"scpcontainer.tar.gz",@"/datadrive/scpcontainer.tar.gz");

        _ = Log(fN,"Extracting Blob");
        await vm.ConsoleDirect(@"tar -xvzf /datadrive/scpcontainer.tar.gz -C /datadrive",sshClient);

        _ = Log(fN,"Setting Up Server");
        await vm.ConsoleDirect(@"mkdir -p /home/azureuser/.config",sshClient);
        await vm.ConsoleDirect(@"ln -s /datadrive/scpcontainer/config /home/azureuser/.config/SCP\ Secret\ Laboratory",sshClient);

        _ = SendMessage("Starting Server");
        _ = Log(fN,"Starting SCP Server");
        await vm.ConsoleDirect(@"cd /datadrive/scpcontainer",sshClient);
        await vm.ConsoleDirect(@"./LocalAdmin 7777",sshClient);

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
        _started = true;
    }

    public async Task StopServerAsync(Func<string, Task> SendMessage){
        var fN = nameof(StopServerAsync); //used in logging

        try{
            if(vm==null) throw new("VM does not exist");
            if(sshClient==null)throw new("sshClient does not exist");
            if(!_started) throw new("Process should already be dead.");
        } catch (Exception e){
            _ = Log(fN,$"{e}");
            _ = SendMessage($"{e}");
            return;
        }
        
        await vm.ConsoleDirect(@"stop",sshClient);
        _ = Log(fN,"SCP Server Stopped");

        await vm.ConsoleDirect(@"exit",sshClient);
        sshClient.Close();
        _ = Log(fN,"SSH Connection Stopped");
        
        _ = SendMessage($"Deprovisioning Server");
        await vm.Deallocate();
        _started = false;
        _ = Log(fN,"SCPInterface Reset");
    }

    private async Task SetupDisk(){
        var fN = nameof(SetupDisk); //used in logging

        try{
            if(vm==null) throw new ArgumentException("VM does not exist");
        } catch (Exception e){
            _ = Log(fN,$"{e}");
            return;
        }

        //partition, format, mount, chmod
        var script = @"
            #!/bin/bash
            while [ ! -b /dev/sdc ]; do
                :
            done

            parted /dev/sdc --script mklabel gpt
            parted /dev/sdc --script mkpart primary ext4 0% 100%

            sudo mkfs.ext4 /dev/sdc1

            sudo mkdir -p /datadrive
            sudo mount /dev/sdc1 /datadrive

            sudo chmod -R 777 /datadrive
            ";

        _ = Log(fN,"Formatting Disk 1");
        await vm.RunScript(script);
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
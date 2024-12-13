using System.Text;
using System.Diagnostics;
using Azure.ResourceManager;

namespace Moonfire.Interfaces;

public class SCPInterface
{   
    private bool _started = false;
    public string PublicIp => _started ? vm?.ip ?? "Not Found" : "Server Not Started";
    private Process? sshClient;
    private AzureVM? vm;

    private SCPInterface(){}

    public static async Task<SCPInterface?> CreateInterface(ArmClient client,string name){
        SCPInterface obj = new();

        //loading settings
        var templatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "Moonfire",
            "Config",
            "SCPSettings.json"
        );
        var settings = await AzureSettings.CreateAsync(templatePath);

        //allocating vm
        //match RG/VM names in sshClient ProcessStartInfo.Arguments
        obj.vm = await AzureManager.Allocate(client, settings, $"{name}RG", $"SCPVM");

        if(obj.vm == null){
            _ = Console.Out.WriteLineAsync("SCPInterace: Azure Allocation Failed");
            return null;
        }

        obj.sshClient = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ssh",
                //RG/Key names to allocation names, Use vm.ip, publicIp returns 'Server Not Started'
                Arguments = $"-o StrictHostKeyChecking=no -i ~/.config/Moonfire/Ssh/{name}RG/SCPVM-Key.pem azureuser@{obj.vm.ip}",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        return obj;
    }

    public async Task StartServerAsync(ArmClient client){
        if(vm==null){
            _ = Console.Out.WriteLineAsync("SCPInterface: StartServerAsync: vm does not exist");
            return;
        }
        if(sshClient==null){
            _ = Console.Out.WriteLineAsync($"{vm.name}: StartServerAsync: sshClient does not exist");
            return;
        }
        if(_started){
            _ = Console.Out.WriteLineAsync($"{vm.name}: Process should already be started");
            return;
        }

        _ = Console.Out.WriteLineAsync($"{vm.name}: Setting up Disk");
        await SetupDisk();

        _ = Console.Out.WriteLineAsync($"{vm.name}: Downloading Blob");
        await vm.DownloadBlob(@"scpcontainer",@"scpcontainer.tar.gz",@"/datadrive/scpcontainer.tar.gz");

        _ = Console.Out.WriteLineAsync($"{vm.name}: Extracting Blob");
        await vm.ConsoleDirect(@"tar -xvzf /datadrive/scpcontainer.tar.gz -C /datadrive",sshClient);

        _ = Console.Out.WriteLineAsync($"{vm.name}: Setting Up Server");
        await vm.ConsoleDirect(@"mkdir -p /home/azureuser/.config",sshClient);
        await vm.ConsoleDirect(@"ln -s /datadrive/scpcontainer/config /home/azureuser/.config/SCP\ Secret\ Laboratory",sshClient);

        _ = Console.Out.WriteLineAsync($"{vm.name}: Starting SCP Server");
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
            Console.WriteLine($"{vm.name}: SCPInterface: sshClient EndOfStream encountered or halted");
        });

        //Waits for heartbeat report to continue
        await _heartbeatReceived.Task;
        if(configTransferFailed) _ = Console.Out.WriteLineAsync($"{vm.name}: Config Transfer Failed");
        _ = Console.Out.WriteLineAsync($"{vm.name}: SCP Server Started");
        _started = true;
    }

    public async Task StopServerAsync(){
        if(vm==null){
            _ = Console.Out.WriteLineAsync("SCPInterface: StopServerAsync: vm does not exist");
            return;
        }
        if(sshClient==null){
            _ = Console.Out.WriteLineAsync($"{vm.name}: StopServerAsync: sshClient does not exist");
            return;
        }
        if(!_started){
            _ = Console.Out.WriteLineAsync($"{vm.name}: Process should already be dead.");
            return;
        }
        
        await vm.ConsoleDirect(@"stop",sshClient);
        Console.WriteLine($"{vm.name}: SCP Server Stopped");

        await vm.ConsoleDirect(@"exit",sshClient);
        sshClient.Close();
        
        await vm.Deallocate();
        _started = false;
        Console.WriteLine($"{vm.name}: SCPInterface Reset");
    }

    private async Task SetupDisk(){
        if(vm==null){
            _ = Console.Out.WriteLineAsync("SCPInterface: SetupDisk: vm does not exist");
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
        _ = Console.Out.WriteLineAsync($"{vm.name}: Formatting Disk 1");
        await vm.RunScript(script);
    }
}
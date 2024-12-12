using System.Text;
using System.Diagnostics;

namespace Moonfire.Interfaces;

public class SCPInterface
{   
    private bool _started = false;
    private readonly string? ip;
    public string PublicIp => _started ? ip ?? "Not Found" : "Server Not Started";
    private readonly Process sshClient;
    private readonly AzureVM vm;

    public SCPInterface(AzureVM _vm){
        vm = _vm;
        ip = vm.ip;

        sshClient = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = $"-o StrictHostKeyChecking=no -i ~/.config/Moonfire/Ssh/{vm.rgname}/{vm.name}-Key.pem azureuser@{vm.ip}",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
    }

    public async Task StartServerAsync(){
        if(_started){
            _ = Console.Out.WriteLineAsync("Process should already be started.");
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
        if(!_started){
            _ = Console.Out.WriteLineAsync("Process should already be dead.");
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
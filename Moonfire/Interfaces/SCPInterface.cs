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

    public async Task StartServerAsync(AzureVM vm){
        if(!_started){
            Console.WriteLine($"{vm.name}: Starting SCP Server");
            await vm.ConsoleDirect(@"sudo chmod -R 777 /datadrive/",sshClient);
            await vm.ConsoleDirect(@"ln -s /datadrive/config/ ~/.config/SCP\ Secret\ Laboratory",sshClient);
            await vm.ConsoleDirect(@"cd /datadrive/",sshClient);
            await vm.ConsoleDirect(@"./LocalAdmin 7777",sshClient);

            TaskCompletionSource<bool> _heartbeatReceived = new();

            //Read the output asynchronously to console
            _ = Task.Run(async () => {
                while(!sshClient.StandardOutput.EndOfStream){
                    string? output = await sshClient.StandardOutput.ReadLineAsync();
                    if(output!=null){
                        Console.WriteLine(output);
                        if(output.Contains("Received first heartbeat")) _heartbeatReceived.TrySetResult(true);
                    }
                }
                Console.WriteLine($"{vm.name}: SCPInterface: sshClient EndOfStream encountered or halted");
            });

            //Waits for heartbeat report to continue
            await _heartbeatReceived.Task;
            Console.WriteLine($"{vm.name}: SCP Server Started");
            _started = true;

        }else{
            Console.WriteLine("Process should already be started.");
        }
    }

    public async Task StopServerAsync(AzureVM vm){
        if(_started){
            await vm.ConsoleDirect(@"stop",sshClient);
            Console.WriteLine($"{vm.name}: SCP Server Stopped");

            await vm.ConsoleDirect(@"exit",sshClient);
            sshClient.Close();
            
            await vm.Stop();
            _started = false;
            Console.WriteLine($"{vm.name}: SCPInterface Reset");
        }else{
            Console.WriteLine("Process should already be dead.");
        }
    }
}
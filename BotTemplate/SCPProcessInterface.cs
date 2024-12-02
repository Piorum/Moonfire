using System.Diagnostics;

namespace SCDisc;

public class SCPProcessInterface
{   
    private bool _started = false;
    private string? ip;
    public string PublicIp => _started ? ip ?? "Not Found" : "Server Not Started";

    public async Task StartServerAsync(AzureVM vm){
        if(!_started){
            Console.WriteLine($"{vm.name}: Starting SCP Server");
            await vm.ConsoleDirect(@"mkdir -p ~/.config/SCP\ Secret\ Laboratory/config/7777/");
            await vm.ConsoleDirect(@"cp /datadrive/config/* ~/.config/SCP\ Secret\ Laboratory/config/7777/");
            await vm.ConsoleDirect(@"cd /datadrive/Steam/steamapps/common/SCP\ Secret\ Laboratory\ Dedicated\ Server/");
            await vm.ConsoleDirect(@"./LocalAdmin 7777");

            TaskCompletionSource<bool> _heartbeatReceived = new();

            //Read the output asynchronously to console
            _ = Task.Run(async () => {
                while(!vm.sshClient.StandardOutput.EndOfStream){
                    string? output = await vm.sshClient.StandardOutput.ReadLineAsync();
                    if(output!=null){
                        if(output.Contains("IP address is")) ip = $"{output.Split(' ')[9]}:7777";
                        if(output.Contains("Received first heartbeat.")) _heartbeatReceived.TrySetResult(true);
                    }
                }
            });

            //Waits for heartbeat report to continue
            await _heartbeatReceived.Task;
            _started = true;

        }else{
            Console.WriteLine("Process should already be started.");
        }
    }
}
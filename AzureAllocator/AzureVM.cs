using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using System.Diagnostics;

namespace AzureAllocator;

public class AzureVM
{
    private readonly VirtualMachineResource vm;
    private readonly ResourceGroupResource? rg;
    private bool started;
    private bool connected = false;
    public readonly string name;
    public readonly string ip;

    public AzureVM(VirtualMachineResource _vm, string _name, string _ip, ResourceGroupResource? _rg){
        name = _name;
        ip = _ip;
        vm = _vm;
        rg = _rg;

        //check initial VM power state
        CheckStarted();
        Console.WriteLine($"{name} is Started: {started}");
    }

    public async Task Start(){
        if(!started) await StartVM();
    }

    public async Task Stop(){
        if(started){
            Console.WriteLine($"{name}: Stopping VM");
            await vm.DeallocateAsync(Azure.WaitUntil.Completed);

            started = false;
            connected = false;
            Console.WriteLine($"{name}: Stopped VM");
        }
    }

    public async Task ConsoleDirect(string input, Process _client){
        await Start();
        if (!connected) await StartSSH(_client);
        Console.WriteLine($"{name}: sent '{input}'");
        await _client.StandardInput.WriteLineAsync(input);
    }

    public async Task StartSSH(Process _client){
        Console.WriteLine($"{name}: StartSSH trying to connect");
        _client.Start();

        TaskCompletionSource<bool> _heartbeatReceived = new();
        var monitorTask = Task.Run(async () => {
            while(!_heartbeatReceived.Task.IsCompleted){
                string? output = await _client.StandardOutput.ReadLineAsync();
                if(output!=null && output.Contains("Welcome")){
                    _heartbeatReceived.TrySetResult(true);
                }
            }
            Console.WriteLine($"{name}: StartSSH welcome received");
        });

        //FAIL-OPEN logic, assumes welcome message was missed
        var timeoutTask = Task.Delay(10000).ContinueWith(_ => {
            if(!_heartbeatReceived.Task.IsCompleted) 
                Console.WriteLine($"{name}: StartSSH connection welcome not received.");
            _heartbeatReceived.TrySetResult(true);
        });

        await Task.WhenAny(monitorTask, timeoutTask);
        await Task.Delay(200);
        connected = true;

        Console.WriteLine($"{name}: StartSSH connection attempted");
    }
    public async Task DeAllocate(){
        //check for null, but should never be null here
        if(rg==null){
            await Console.Out.WriteLineAsync($"{name}: Resource Group Null, Deallocation Failed");
            return;
        }
        await Console.Out.WriteLineAsync($"{name}: Deallocating");
        await AzureManager.DeAllocate(rg);
        await Console.Out.WriteLineAsync($"{name}: Deallocated");
    }
    private async Task StartVM(){
        Console.WriteLine($"{name}: Starting VM");

        await vm.PowerOnAsync(Azure.WaitUntil.Completed);
        started = true;
        
        Console.WriteLine($"{name}: Started VM");
    }

    private void CheckStarted(){
        started = "VM running" == vm.InstanceView().Value.Statuses.FirstOrDefault(status => status.Code.StartsWith("PowerState/"))?.DisplayStatus;
    }
}

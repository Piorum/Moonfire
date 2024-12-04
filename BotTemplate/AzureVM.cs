using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using System.Diagnostics;

namespace SCDisc;

public class AzureVM
{
    private readonly VirtualMachineResource vm;
    private bool started;
    private bool connected = false;
    public readonly string name;
    public readonly string ip;
    public readonly string group;

    public AzureVM(string _name, string _ip, string _group){
        name = _name;
        ip = _ip;
        group = _group;

        //credential setup
        //strongly defined to avoid nullable string
        string clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "";
        string clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? "";
        string tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "";
        string subscription = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID") ?? "";
        ClientSecretCredential credential = new(tenantId, clientId, clientSecret);
        ArmClient client = new(credential, subscription);

        //selecting VM
        Azure.Core.ResourceIdentifier rgName = new($"/subscriptions/{subscription}/resourceGroups/{group}");
        ResourceGroupResource resourceGroup = client.GetResourceGroupResource(rgName);
        VirtualMachineCollection vmCollection = resourceGroup.GetVirtualMachines();
        vm = vmCollection.Get(name);

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

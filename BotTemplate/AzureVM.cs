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
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var subscription = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
        ClientSecretCredential credential = new(tenantId, clientId, clientSecret);
        ArmClient client = new(credential, subscription);

        //selecting VM
        Azure.Core.ResourceIdentifier rgName = new($"/subscriptions/{subscription}/resourceGroups/{group}");
        ResourceGroupResource resourceGroup = client.GetResourceGroupResource(rgName);
        VirtualMachineCollection vmCollection = resourceGroup.GetVirtualMachines();
        vm = vmCollection.Get(name);

        //check initial VM power state
        started = "VM running" == vm.InstanceView().Value.Statuses.FirstOrDefault(status => status.Code.StartsWith("PowerState/"))?.DisplayStatus;
        Console.WriteLine($"VM: {name} is Started: {started}");
    }

    public async Task Start(){
        CheckStarted();
        if(!started){
            await StartVM();
        }
    }

    public async Task Stop(){
        CheckStarted();
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
        if (!connected){
            await StartSSH(_client);
        }
        Console.WriteLine($"{name}: sent '{input}'");
        await _client.StandardInput.WriteLineAsync(input);
    }

    public async Task StartSSH(Process _client){
        Console.WriteLine($"{name}: Attempting SSH connection");
        _client.Start();

        TaskCompletionSource<bool> _heartbeatReceived = new();
        _ = Task.Run(async () => {
            while(!_client.StandardOutput.EndOfStream){
                string? output = await _client.StandardOutput.ReadLineAsync();
                if(output!=null){
                    if(output.Contains("Welcome")) _heartbeatReceived.TrySetResult(true);
                }
            }
        });

        await _heartbeatReceived.Task;
        await Task.Delay(200);
        connected = true;

        Console.WriteLine($"{name}: SSH connection succeeded");
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

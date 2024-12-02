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
    public readonly Process sshClient;
    public readonly string name = "Ubuntu-Server-1";
    public AzureVM(){
        //credential setup
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var subscription = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
        ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        ArmClient client = new ArmClient(credential, subscription);

        //selecting VM
        Azure.Core.ResourceIdentifier rgName = new($"/subscriptions/{subscription}/resourceGroups/Moonfire-VM-1");
        ResourceGroupResource resourceGroup = client.GetResourceGroupResource(rgName);
        VirtualMachineCollection vmCollection = resourceGroup.GetVirtualMachines();
        vm = vmCollection.Get(name);

        //check initial VM power state
        started = "VM running" == vm.InstanceView().Value.Statuses.FirstOrDefault(status => status.Code.StartsWith("PowerState/"))?.DisplayStatus;
        Console.WriteLine($"VM: {name} is Started: {started}");

        sshClient = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = @"-i ~/.ssh/Ubuntu-Server-1-Key.pem azureuser@13.89.185.75",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        if(started) _ = StartSSH();

    }
    public async Task Start(){
        if(!started){
            Console.WriteLine($"{name}: Starting VM");
            await vm.PowerOnAsync(Azure.WaitUntil.Completed);
            started = true;
            Console.WriteLine($"{name}: Started VM");

            await StartSSH();

        }
    }

    public async Task Stop(){
        if(started){
            Console.WriteLine($"{name}: Stopping VM");
            await vm.DeallocateAsync(Azure.WaitUntil.Completed);

            started = false;
            Console.WriteLine($"{name}: Stopped VM");
        }
    }

    public async Task ConsoleDirect(string input){
        await EnsureStarted();
        Console.WriteLine($"{name}: sent '{input}'");
        await sshClient.StandardInput.WriteLineAsync(input);    
    }

    private async Task EnsureStarted(){
        if(!started){
            await Start();
        }
    }

    private async Task StartSSH(){
        sshClient.Start();
        Console.WriteLine($"{name}: Attempting SSH connection");
        TaskCompletionSource<bool> _heartbeatReceived = new();
        _ = Task.Run(async () => {
            while(!sshClient.StandardOutput.EndOfStream){
                string? output = await sshClient.StandardOutput.ReadLineAsync();
                if(output!=null){
                    if(output.Contains("Welcome")) _heartbeatReceived.TrySetResult(true);
                }
            }
        });
        await _heartbeatReceived.Task;
        Console.WriteLine($"{name}: SSH connection succeeded");
    }
}

using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;

namespace SCDisc;

public class AzureVM
{
    private readonly VirtualMachineResource vm;
    public readonly string name = "Ubuntu-Server-1";
    public AzureVM(){
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var subscription = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
        ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        ArmClient client = new ArmClient(credential, subscription);

        Azure.Core.ResourceIdentifier rgName = new($"/subscriptions/{subscription}/resourceGroups/Moonfire-VM-1");
        ResourceGroupResource resourceGroup = client.GetResourceGroupResource(rgName);
        VirtualMachineCollection vmCollection = resourceGroup.GetVirtualMachines();
        vm = vmCollection.Get(name);
    }
    public async Task Start(){
        await vm.PowerOnAsync(Azure.WaitUntil.Completed);
    }

    public async Task Stop(){
        await vm.DeallocateAsync(Azure.WaitUntil.Completed);
    }

}

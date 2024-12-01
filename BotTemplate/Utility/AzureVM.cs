using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using SCDisc.Utility;

namespace SCDisc;

public static class AzureVM
{
    public static async Task StartVM(){
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var subscription = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
        ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        ArmClient client = new ArmClient(credential, subscription);

        Azure.Core.ResourceIdentifier rgName = new($"/subscriptions/{subscription}/resourceGroups/VMs");
        string vmName = "Ubuntu-Server-1";
        ResourceGroupResource resourceGroup = client.GetResourceGroupResource(rgName);
        VirtualMachineCollection vmCollection = resourceGroup.GetVirtualMachines();
        VirtualMachineResource vm = await vmCollection.GetAsync(vmName);

        //Attempt to start the VM
        await FuncExt.Time(async () => await vm.PowerOnAsync(Azure.WaitUntil.Completed), $"Starting '{vmName}'", $"'{vmName}' Started");
    }

}

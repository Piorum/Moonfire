using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Mono.Unix.Native;

namespace AzureAllocator;

public static class AzureManager
{
    public static async Task<AzureVM?> Allocate(ArmClient client, AzureSettings settings, string rgName, string vmName){
        //defaults if settings are null
        var region = settings.Region ?? "centralus";

        ResourceGroupResource? rg = null; //used in finally
        try{
            //creating resource group
            VirtualMachineResource vm;
            string ip;
            try{
                //behavior if resource group exists
                //this will break if internal resources are missing
                ResourceIdentifier rgid = new($"/subscriptions/{Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID")}/resourceGroups/{rgName}");
                rg = client.GetResourceGroupResource(rgid);
                vm = await rg.GetVirtualMachines().GetAsync(vmName);
                ip = (await rg.GetPublicIPAddresses().GetAsync("PublicIPDefaultName")).Value.Data.IPAddress;
            }
            catch {
                //behavior if resource group does not exist
                _ = Console.Out.WriteLineAsync($"{nameof(AzureManager)}: Creating Resource Group");
                rg = (await client
                    .GetDefaultSubscription()
                    .GetResourceGroups()
                    .CreateOrUpdateAsync(Azure.WaitUntil.Completed, rgName, new ResourceGroupData(region))).Value;

                //begin vnet and pip allocation and continue once both are done
                var vnetTask = AllocateVnet(region,rg);
                var pipTask = AllocatePip(region,rg);
                var nsgTask = AllocateNsg(region,rg,settings);
                await Task.WhenAll(vnetTask,pipTask,nsgTask);
                var vnet = await vnetTask;
                var pip = await pipTask;
                var nsg = await nsgTask;

                ip = pip.Data.IPAddress;

                //allocate nic then vm
                var nic = await AllocateNic(region,rg,vnet,pip,nsg);
                var key = await GenerateSshKeyPair(region,vmName,rgName,rg);
                vm = await AllocateVm(region,vmName,rg,nic,key,settings);
            }

            _ = Console.Out.WriteLineAsync($"{nameof(AzureManager)}: Completed Allocation of {vmName}");
            //return allocated vm interface
            return new AzureVM(vm, vmName, ip, rg, rgName);
        } 
        catch (Exception e){
            await Console.Out.WriteLineAsync($"AzureManager, Failed to allocate.\n{e}");
            if(rg!=null){
                await Console.Out.WriteLineAsync($"Cleaning up partial VM");
                await rg.DeleteAsync(Azure.WaitUntil.Completed);
                await Console.Out.WriteLineAsync($"Partial VM deallocated");
            } else{
                await Console.Out.WriteLineAsync("Did not create any resources in Azure. No clean up is necessary");
            }
        }
        return null;
    }

    public static async Task DeAllocate(ResourceGroupResource resourceGroup){
        try{
            await resourceGroup.DeleteAsync(Azure.WaitUntil.Completed);
        }
        catch (Exception e){
            await Console.Out.WriteLineAsync($"Failed deallocation\n{e}");
        }
    }

    private static async Task<VirtualNetworkResource> AllocateVnet(
        string region,
        ResourceGroupResource rg)
    {
        var vnetData = new VirtualNetworkData()
        {
            Location = region,
            AddressPrefixes = { "10.0.0.0/16" },
            Subnets = { new SubnetData() { Name = "SubnetDefaultName", AddressPrefix = "10.0.0.0/24" } }
        };
        _ = Console.Out.WriteLineAsync($"{nameof(AzureManager)}: Creating Virtual Network");
        return (await rg.GetVirtualNetworks().CreateOrUpdateAsync(Azure.WaitUntil.Completed, "VnetDefaultName", vnetData)).Value;
    }

    private static async Task<PublicIPAddressResource> AllocatePip(
        string region, 
        ResourceGroupResource rg
    ){
        var publicIp = new PublicIPAddressData()
        {
            Location = region,
            Sku = new PublicIPAddressSku { Name = PublicIPAddressSkuName.Standard },
            PublicIPAllocationMethod = NetworkIPAllocationMethod.Static
        };
        _ = Console.Out.WriteLineAsync($"{nameof(AzureManager)}: Creating Public Ip");
        return (await rg.GetPublicIPAddresses().CreateOrUpdateAsync(Azure.WaitUntil.Completed, "PublicIPDefaultName", publicIp)).Value;
    }

    private static async Task<NetworkSecurityGroupResource> AllocateNsg(
        string region, 
        ResourceGroupResource rg,
        AzureSettings settings
    ){
        var nsg = new NetworkSecurityGroupData(){
            Location = region,
            SecurityRules =
            {
            }
        };

        //adding security rules
        if(settings.SecurityRules != null)
            foreach(var rule in settings.SecurityRules){
                nsg.SecurityRules.Add(new SecurityRuleData
                { 
                    Name = rule.Name,
                    Priority = rule.Priority,
                    Access = rule.Access,
                    Direction = rule.Direction,
                    Protocol = rule.Protocol,
                    SourcePortRange = rule.SourcePortRange,
                    DestinationPortRange = rule.DestinationPortRange,
                    SourceAddressPrefix = rule.SourceAddressPrefix,
                    DestinationAddressPrefix = rule.DestinationAddressPrefix
                });
            }

        _ = Console.Out.WriteLineAsync($"{nameof(AzureManager)}: Creating Network Security Group");
        return (await rg.GetNetworkSecurityGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, "NetworkSGDefaultName", nsg)).Value;
    }

    private static async Task<NetworkInterfaceResource> AllocateNic(
        string region,
        ResourceGroupResource rg,
        VirtualNetworkResource vnet,
        PublicIPAddressResource pip,
        NetworkSecurityGroupResource nsg
    ){
        var nicData = new NetworkInterfaceData()
        {
            Location = region,
            NetworkSecurityGroup = new NetworkSecurityGroupData(){
                Id = nsg.Id
            },
            IPConfigurations = {
                new NetworkInterfaceIPConfigurationData()
                {
                    Name = "Primary",
                    PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                    Primary = true,
                    Subnet = new SubnetData(){
                        Id = vnet.Data.Subnets.First(s => s.Name == "SubnetDefaultName").Id
                    },
                    PublicIPAddress = new PublicIPAddressData(){
                        Id = pip.Id
                    }
                }
            }
        };
        _ = Console.Out.WriteLineAsync($"{nameof(AzureManager)}: Creating Network Interface");
        return (await rg.GetNetworkInterfaces().CreateOrUpdateAsync(Azure.WaitUntil.Completed, "NetworkInterfaceDefaultName", nicData)).Value;
    }

    private static async Task<VirtualMachineResource> AllocateVm(
        string region,
        string VmName,
        ResourceGroupResource rg,
        NetworkInterfaceResource nic,
        SshPublicKeyGenerateKeyPairResult key,
        AzureSettings settings
    ){
        //vm config
        var VmData = new VirtualMachineData(region)
        {
            NetworkProfile = new VirtualMachineNetworkProfile()
            {
                NetworkInterfaces =
                {
                    new VirtualMachineNetworkInterfaceReference()
                    {
                        Id = nic.Id,
                        Primary = true,
                    }
                }
            },
            OSProfile = new VirtualMachineOSProfile()
            {
                ComputerName = VmName,
                AdminUsername = "azureuser",
                LinuxConfiguration = new LinuxConfiguration
                {
                    DisablePasswordAuthentication = true,
                    SshPublicKeys =
                    {
                        new SshPublicKeyConfiguration
                        {
                            Path = "/home/azureuser/.ssh/authorized_keys",
                            KeyData = key.PublicKey
                        }
                    }
                }
            },
            StorageProfile = new VirtualMachineStorageProfile()
            {
                OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                {
                    OSType = SupportedOperatingSystemType.Linux,
                    Name = "OsDiskDefaultName",
                    ManagedDisk = new VirtualMachineManagedDisk
                    {
                        StorageAccountType = StorageAccountType.StandardLrs
                    }
                },
                ImageReference = settings.ImageReference,
                DataDisks =
                {
                }
            },
            HardwareProfile = new VirtualMachineHardwareProfile() { VmSize = settings.VmSize },
        };
        _ = Console.Out.WriteLineAsync($"{nameof(AzureManager)}: Creating Virtual Machine");
        return (await rg.GetVirtualMachines().CreateOrUpdateAsync(Azure.WaitUntil.Completed, VmName, VmData)).Value;
    }

    private static async Task<SshPublicKeyGenerateKeyPairResult> GenerateSshKeyPair(
        string region,
        string VmName,
        string RgName,
        ResourceGroupResource rg
    ){
        _ = Console.Out.WriteLineAsync($"{nameof(AzureManager)}: Generating Ssh Key Pair");
        var keyData = new SshPublicKeyData(region)
        {
            PublicKey = null // Set this to null to let Azure generate the key
        };
        var key = (await rg.GetSshPublicKeys().CreateOrUpdateAsync(Azure.WaitUntil.Completed,"SshKeyDefaultName",keyData)).Value;
        var pair = (await key.GenerateKeyPairAsync()).Value;

        //saving private key locally
        var sshPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Moonfire",
            "Ssh",
            $"{RgName}"
        );
        var keyPath = Path.Combine(sshPath,$"{VmName}-Key.pem");

        if(!Directory.Exists(sshPath))
            Directory.CreateDirectory(sshPath);

        await File.WriteAllTextAsync(keyPath, pair.PrivateKey);
        Syscall.chmod(keyPath, FilePermissions.S_IRUSR | FilePermissions.S_IWUSR);

        return pair;
    }
}

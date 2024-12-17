using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using System;
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
                ip = (await rg.GetPublicIPAddresses().GetAsync($"{vmName}PublicIP")).Value.Data.IPAddress;
            }
            catch {
                //behavior if resource group does not exist
                _ = Log(vmName,rgName,nameof(Allocate),$"Creating Resource Group");
                rg = (await client
                    .GetDefaultSubscription()
                    .GetResourceGroups()
                    .CreateOrUpdateAsync(Azure.WaitUntil.Completed, rgName, new ResourceGroupData(region))).Value;

                //begin vnet and pip allocation and continue once both are done
                var vnetTask = AllocateVnet(region,vmName,rgName,rg);
                var pipTask = AllocatePip(region,vmName,rgName,rg);
                var nsgTask = AllocateNsg(region,vmName,rgName,rg,settings);
                var keyTask = GenerateSshKeyPair(region,vmName,rgName,rg);
                await Task.WhenAll(vnetTask,pipTask,nsgTask,keyTask);
                var vnet = await vnetTask;
                var pip = await pipTask;
                var nsg = await nsgTask;
                var key = await keyTask;

                ip = pip.Data.IPAddress;

                //allocate nic then vm
                var nic = await AllocateNic(region,vmName,rgName,rg,vnet,pip,nsg);
                vm = await AllocateVm(region,vmName,rgName,rg,nic,key,settings);
            }

            _ = Log(vmName,rgName,nameof(Allocate),$"Completed Allocation");
            //return allocated vm interface
            return new AzureVM(vm, vmName, ip, rg, rgName);
        } 
        catch (Exception e){
            _ = Log(vmName,rgName,nameof(Allocate),$"Failed to allocate.\n{e}");
            if(rg!=null){
                _ = Log(vmName,rgName,nameof(Allocate),$"Cleaning up partial VM");
                await DeAllocate(vmName,rgName,rg);
                _ = Log(vmName,rgName,nameof(Allocate),$"Partial VM deallocated");
            } else{
                _ = Log(vmName,rgName,nameof(Allocate),$"Did not create any resources in Azure. No clean up is necessary");
            }
        }
        return null;
    }

    public static async Task DeAllocate(
        string vmName,
        string rgName,
        ResourceGroupResource resourceGroup
    ){
        try{
            await resourceGroup.DeleteAsync(Azure.WaitUntil.Completed);
        }
        catch (Exception e){
            _ = Log(vmName,rgName,nameof(DeAllocate),$"Failed deallocation\n{e}");
        }
    }

    private static async Task<VirtualNetworkResource> AllocateVnet(
        string region,
        string vmName,
        string rgName,
        ResourceGroupResource rg)
    {
        var vnetData = new VirtualNetworkData()
        {
            Location = region,
            AddressPrefixes = { "10.0.0.0/16" },
            Subnets = { new SubnetData() { Name = $"{vmName}Subnet", AddressPrefix = "10.0.0.0/24" } }
        };
        _ = Log(vmName,rgName,nameof(AllocateVnet),$"Creating Virtual Network");
        return (await rg.GetVirtualNetworks().CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"{vmName}Vnet", vnetData)).Value;
    }

    private static async Task<PublicIPAddressResource> AllocatePip(
        string region,
        string vmName,
        string rgName,
        ResourceGroupResource rg
    ){
        var publicIp = new PublicIPAddressData()
        {
            Location = region,
            Sku = new PublicIPAddressSku { Name = PublicIPAddressSkuName.Standard },
            PublicIPAllocationMethod = NetworkIPAllocationMethod.Static
        };
        _ = Log(vmName,rgName,nameof(AllocatePip),$"Creating Public Ip");
        return (await rg.GetPublicIPAddresses().CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"{vmName}PublicIP", publicIp)).Value;
    }

    private static async Task<NetworkSecurityGroupResource> AllocateNsg(
        string region,
        string vmName,
        string rgName,
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
        
        //adding ssh rule
        nsg.SecurityRules.Add(new SecurityRuleData
        {
            Name = "AllowSSH",
            Priority = 100,
            Access = "Allow",
            Direction = "Inbound",
            Protocol = "*",
            SourcePortRange = "*",
            DestinationPortRange = "22",
            SourceAddressPrefix = (await new HttpClient().GetStringAsync(@"https://checkip.amazonaws.com/")).Trim(),
            DestinationAddressPrefix = "*"
        });

        _ = Log(vmName,rgName,nameof(AllocateNsg),$"Creating Network Security Group");
        return (await rg.GetNetworkSecurityGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"{vmName}NetworkSG", nsg)).Value;
    }

    private static async Task<NetworkInterfaceResource> AllocateNic(
        string region,
        string vmName,
        string rgName,
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
                        Id = vnet.Data.Subnets.First(s => s.Name == $"{vmName}Subnet").Id
                    },
                    PublicIPAddress = new PublicIPAddressData(){
                        Id = pip.Id
                    }
                }
            }
        };
        _ = Log(vmName,rgName,nameof(AllocateNic),$"Creating Network Interface");
        return (await rg.GetNetworkInterfaces().CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"{vmName}NetworkInterface", nicData)).Value;
    }

    private static async Task<VirtualMachineResource> AllocateVm(
        string region,
        string vmName,
        string rgName,
        ResourceGroupResource rg,
        NetworkInterfaceResource nic,
        SshPublicKeyGenerateKeyPairResult key,
        AzureSettings settings
    ){
        //vm config
        var vmData = new VirtualMachineData(region)
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
                ComputerName = vmName,
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
                    Name = $"{vmName}OsDisk",
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

        //adding data disks
        if(settings.DataDisks!=null){
            foreach(var disk in settings.DataDisks){
                var i = vmData.StorageProfile.DataDisks.Count + 1;
                _ = Log(vmName,rgName,nameof(AllocateVm),$"Attaching Disk {i}");
                vmData.StorageProfile.DataDisks.Add(
                    new(i, DiskCreateOptionType.Empty)
                    {
                        Name = $"{vmName}DataDisk{i}",
                        DiskSizeGB = disk.Size,
                        ManagedDisk = new()
                        {
                            StorageAccountType = disk.Type
                        }
                    }
                );
            }
        }

        _ = Log(vmName,rgName,nameof(AllocateVm),$"Creating Virtual Machine");
        return (await rg.GetVirtualMachines().CreateOrUpdateAsync(Azure.WaitUntil.Completed, vmName, vmData)).Value;
    }

    private static async Task<SshPublicKeyGenerateKeyPairResult> GenerateSshKeyPair(
        string region,
        string vmName,
        string rgName,
        ResourceGroupResource rg
    ){
        _ = Log(vmName,rgName,nameof(GenerateSshKeyPair),$"Creating Ssh Key Pair");
        var keyData = new SshPublicKeyData(region)
        {
            PublicKey = null // Set this to null to let Azure generate the key
        };
        var key = (await rg.GetSshPublicKeys().CreateOrUpdateAsync(Azure.WaitUntil.Completed,$"{vmName}SshKey",keyData)).Value;
        var pair = (await key.GenerateKeyPairAsync()).Value;

        //saving private key locally
        var sshPath = Path.Combine(
            Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "",
            "Ssh",
            $"{rgName}"
        );
        var keyPath = Path.Combine(sshPath,$"{vmName}-Key.pem");

        //Ensure directory exists and delete old key if found
        if(!Directory.Exists(sshPath)) Directory.CreateDirectory(sshPath);
        if(File.Exists(keyPath)) File.Delete(keyPath);

        await File.WriteAllTextAsync(keyPath, pair.PrivateKey);

        //set key permission
        if(Syscall.chmod(keyPath, FilePermissions.S_IRUSR | FilePermissions.S_IWUSR)!=0) _ = Log(vmName,rgName,nameof(GenerateSshKeyPair),"chmod failure");

        return pair;
    }

    private static async Task Log(string vmName, string rgName, string funcName, string input) =>
        await Console.Out.WriteLineAsync($"{rgName}:{vmName}:{funcName}:{input}");
}

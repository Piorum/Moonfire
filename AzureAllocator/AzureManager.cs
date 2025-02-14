using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Mono.Unix.Native;
using FuncExt;

namespace AzureAllocator;

public static class AzureManager
{
    public static event EventHandler<AAAlertArgs>? ErrorAlert;
    private static ArmClient? armClient = null;
    private static QuotaManager? quotaManager = null;

    public static async Task<AzureVM?> Allocate(AzureSettings settings, string rgName, string vmName, CancellationToken token = default){
        var quotaM = await GetQuotaManager();
        (var AzureValues, var availability) = await quotaM.RequestQuota(settings);
        if(AzureValues is null || availability is QuotaManager.VMAvailability.INVALID || availability is QuotaManager.VMAvailability.GLOBALFULL){
            return null;
        }

        var region = AzureValues.Region;
        var azureVmSize = AzureValues.AzureVmName;
        //var region = "centralus";
        //var azureVmSize = "Standard_B2s";
        await Console.Out.WriteLineAsync($"region:{region}:vmSize:{azureVmSize}");

        var client = await GetArmClient();

        //component names specificed here
        //rgName and vmName specified by function caller
        var vnetName = $"{vmName}Vnet";
        var pipName = $"{vmName}PublicIP";
        var nsgName = $"{vmName}NetworkSG";
        var nicName = $"{vmName}NetworkInterface";
        var keyName = $"{vmName}SshKey";

        //defined here for use in try/catch statements
        ResourceGroupResource? rg = null;
        VirtualMachineResource? vm = null;
        VirtualNetworkResource? vnet = null;
        PublicIPAddressResource? pip = null;
        NetworkSecurityGroupResource? nsg = null;
        NetworkInterfaceResource? nic = null;
        string? key = null;
        
        var sub = await GetSubscriptionAsync();
        try{
            rg = await AllocateOrGetRG(region,vmName,rgName,sub,token) ?? throw new("Resource Group Allocation Failed");

            var vnetTask = AllocateOrGetVnet(region,vmName,rgName,rg,vnetName,token);
            var pipTask = AllocateOrGetPip(region,vmName,rgName,rg,pipName,token);
            var nsgTask = AllocateOrGetNsg(region,vmName,rgName,rg,settings,nsgName,token);
            var keyTask = AllocateOrGetSshKeyPair(region,vmName,rgName,rg,keyName,token);
            await Task.WhenAll(vnetTask,pipTask,nsgTask,keyTask);

            (vnet,var newVnet) = await vnetTask;
            if(vnet is null) throw new("Virtual Network Allocation Failed");

            (pip,var newPip)= await pipTask;
            if(pip is null) throw new("Public IP Allocation Failed");

            (nsg,var newNsg) = await nsgTask;
            if(nsg is null) throw new("Network Security Group Allocation Failed");

            (key,var newKey) = await keyTask;
            if(key is null) throw new("Shh Key Pair Allocation Failed");

            var completeBaseNetwork = !(newVnet || newPip || newNsg || newKey);

            (nic,var newNic) = await AllocateOrGetNic(region,vmName,rgName,rg,vnet,pip,nsg,nicName,completeBaseNetwork,token);
            if(nic is null) throw new("Network Interface Allocation Failed");

            var completeVM = completeBaseNetwork && !newNic;

            vm = await AllocateOrGetVm(region,vmName,rgName,rg,nic,key,settings,completeVM,azureVmSize,token) ?? throw new("Virtual Machine Allocation Failed");

            var success = await AzureVM.TryCreateAzureVMAsync(vmName,rgName,rg,vm,vnet,pip,nsg,nic,keyName,out var azureVM);
            return success ? azureVM : throw new("AzureVM Creation Failed");

        } catch (OperationCanceledException){
            await quotaM.ReleaseQuota(azureVmSize, region);

            //propagate upward
            throw;
        } catch (Exception e){
            await SendAlert("Allocation Failure",e);

            _ = Log(vmName,rgName,nameof(Allocate),$"Cleaning Up Failed Allocation");

            if(vm is null){
                await quotaM.ReleaseQuota(azureVmSize, region);
            }

            var cts = new CancellationTokenSource();
            await Ext.TimeoutTask
            (
                DeAllocate(rg,vm,vnet,pip,nsg,nic,keyName,cts.Token),
                new(0,10,0),
                cts
            );

            return null;
        }
    }

    public static async Task DeAllocate(
        ResourceGroupResource? rg,
        VirtualMachineResource? vm,
        VirtualNetworkResource? vnet,
        PublicIPAddressResource? pip,
        NetworkSecurityGroupResource? nsg,
        NetworkInterfaceResource? nic,
        string? keyName,
        CancellationToken token = default
    ){
        //if rg does not exist return
        if(rg is null) return;

        var sub = await GetSubscriptionAsync();
        if(!await sub.GetResourceGroups().ExistsAsync(rg.Data.Name,cancellationToken:token)) return;

        //set endTime for retrying deallocation
        var endTime = DateTime.Now.AddMinutes(4);

        while(DateTime.Now < endTime){
            try{
                List<Task<Azure.Response<bool>>> existTasks = [];

                bool vmExists = default;
                bool nicExists = default;
                bool pipExists = default;
                bool nsgExists = default;
                bool vnetExists = default;
                bool keyExists = default;

                Task<Azure.Response<bool>>? vmExistsTask = null;
                Task<Azure.Response<bool>>? nicExistsTask = null;
                Task<Azure.Response<bool>>? pipExistsTask = null;
                Task<Azure.Response<bool>>? nsgExistsTask = null;
                Task<Azure.Response<bool>>? vnetExistsTask = null;
                Task<Azure.Response<bool>>? keyExistsTask = null;

                if(vm is not null){
                    vmExistsTask = rg.GetVirtualMachines().ExistsAsync(vm.Data.Name, cancellationToken:token);
                    existTasks.Add(vmExistsTask);

                    var vmSize = vm.Data.HardwareProfile.VmSize.ToString();
                    var region = vm.Data.Location.ToString();

                    var quotaM = await GetQuotaManager();

                    if(vmSize is not null && region is not null){
                        await quotaM.ReleaseQuota(vmSize, region);
                    }

                }
                if(nic is not null){
                    nicExistsTask = rg.GetNetworkInterfaces().ExistsAsync(nic.Data.Name, cancellationToken:token);
                    existTasks.Add(nicExistsTask);
                }
                if(pip is not null){
                    pipExistsTask = rg.GetPublicIPAddresses().ExistsAsync(pip.Data.Name, cancellationToken:token);
                    existTasks.Add(pipExistsTask);
                }
                if(nsg is not null){
                    nsgExistsTask = rg.GetNetworkSecurityGroups().ExistsAsync(nsg.Data.Name, cancellationToken:token);
                    existTasks.Add(nsgExistsTask);
                }
                if(vnet is not null){
                    vnetExistsTask = rg.GetVirtualNetworks().ExistsAsync(vnet.Data.Name, cancellationToken:token);
                    existTasks.Add(vnetExistsTask);
                }
                if(keyName is not null){
                    keyExistsTask = rg.GetSshPublicKeys().ExistsAsync(keyName, cancellationToken:token);
                    existTasks.Add(keyExistsTask);
                }

                await Task.WhenAll(existTasks);

                if(vmExistsTask is not null)
                    vmExists = await vmExistsTask;
                if(nicExistsTask is not null)
                    nicExists = await nicExistsTask;
                if(pipExistsTask is not null)
                    pipExists = await pipExistsTask;
                if(nsgExistsTask is not null)
                    nsgExists = await nsgExistsTask;
                if(vnetExistsTask is not null)
                    vnetExists = await vnetExistsTask;
                if(keyExistsTask is not null)
                    keyExists = await keyExistsTask;

                //Do not change order
                //Deallocate in opposite order of allocation
                //resourceExists cannot equal true if resource is null
                if(vmExists)
                    await vm!.DeleteAsync(Azure.WaitUntil.Completed,cancellationToken:token);
                if(nicExists)
                    await nic!.DeleteAsync(Azure.WaitUntil.Completed,cancellationToken:token);
                if(pipExists)
                    await pip!.DeleteAsync(Azure.WaitUntil.Completed,cancellationToken:token);
                if(nsgExists)
                    await nsg!.DeleteAsync(Azure.WaitUntil.Completed,cancellationToken:token);
                if(vnetExists)
                    await vnet!.DeleteAsync(Azure.WaitUntil.Completed,cancellationToken:token);

                if(keyExists){
                    var sshKey = (await rg.GetSshPublicKeyAsync(keyName,cancellationToken:token)).Value;
                    await sshKey.DeleteAsync(Azure.WaitUntil.Completed,cancellationToken:token);
                }

                //end deallocation if any other resources are found
                await foreach (var _ in rg.GetGenericResourcesAsync(cancellationToken:token)){
                    return;
                }
                //otherwise delete resource group
                await rg.DeleteAsync(Azure.WaitUntil.Completed,cancellationToken:token);

                return; //end deallocation to avoid retrying

            } catch (OperationCanceledException){
                //propagate upward
                throw;
            } catch (Exception e){
                _ = Console.Out.WriteLineAsync($"Deallocation Failed.\n{e}");
            }

            await Task.Delay(30 * 1000, token); //delay before retrying deallocation
        }
        
        //output rgName and vmName to identify problem VM
        await SendAlert($"Deallocation Timed Out:rg.Data.Name:{rg.Data.Name}:vm.Data.Name:{vm?.Data.Name ?? "NoNameFound"}");
    }

    private static async Task<QuotaManager> GetQuotaManager(){
        if(quotaManager is null){
            var quotaJsonPath = Path.Combine(Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "", "quotas.json");
            var quotaJsonString = File.ReadAllText(quotaJsonPath);

            quotaManager = await QuotaManager.CreateAsync(quotaJsonString);

            return quotaManager;
        } else {
            return quotaManager;
        }
    }

    private static Task<ArmClient> GetArmClient(){
        if (armClient != null){
            
            return Task.FromResult(armClient);

        }else{

            var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "";
            var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? "";
            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "";
            var subscription = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID") ?? "";
            ClientSecretCredential credential = new(tenantId, clientId, clientSecret);
            armClient = new(credential, subscription);
            return Task.FromResult(armClient);

        }
    }

    private static async Task<SubscriptionResource> GetSubscriptionAsync() =>
        (await GetArmClient()).GetSubscriptionResource(new($"/subscriptions/{Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID")}"));

    private static async Task<TResource?> GetResourceAsync<TResource>(
        string vmName,
        string rgName,
        Func<string, Task<Azure.Response<bool>>> existsAsync,
        Func<string, Task<Azure.Response<TResource>>> getAsync,
        string resourceName
    ) where TResource : class
    {
        var existsResponse = await existsAsync(resourceName);
        if (existsResponse.Value)
        {
            _ = Log(vmName,rgName,nameof(GetResourceAsync),$"Found {typeof(TResource).Name}");
            return (await getAsync(resourceName)).Value;
        }
        return null;

    }

    private static async Task<ResourceGroupResource?> AllocateOrGetRG(
        string region,
        string vmName,
        string rgName,
        SubscriptionResource subscription,
        CancellationToken token = default
    ){
        var rgR = await GetResourceAsync(
            vmName,
            rgName,
            a => subscription.GetResourceGroups().ExistsAsync(a,cancellationToken:token),
            a => subscription.GetResourceGroupAsync(a,cancellationToken:token),
            rgName
        );

        if(rgR!=null ) 
            return rgR;
        else
            _ = Log(vmName,rgName,nameof(AllocateOrGetRG),$"Creating Resource Group");

        try{
            return (await subscription.GetResourceGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, rgName, new ResourceGroupData(region),cancellationToken:token)).Value;
        } catch (OperationCanceledException){
            //propagate upward
            throw;
        } catch (Exception e){
            _ = Log(vmName,rgName,nameof(AllocateOrGetRG),$"{e}");
            return null;
        }
    }

    private static async Task<(VirtualNetworkResource?,bool)> AllocateOrGetVnet(
        string region,
        string vmName,
        string rgName,
        ResourceGroupResource rg,
        string vnetName,
        CancellationToken token = default
    ){
        var vnetR = await GetResourceAsync(
            vmName,
            rgName,
            a => rg.GetVirtualNetworks().ExistsAsync(a,cancellationToken:token),
            a => rg.GetVirtualNetworkAsync(a,cancellationToken:token),
            vnetName
        );

        if(vnetR!=null) return (vnetR,false);

        var vnetData = new VirtualNetworkData()
        {
            Location = region,
            AddressPrefixes = { "10.0.0.0/16" },
            Subnets = { new SubnetData() { Name = $"{vmName}Subnet", AddressPrefix = "10.0.0.0/24" } }
        };

        _ = Log(vmName,rgName,nameof(AllocateOrGetVnet),$"Creating Virtual Network");
        try{
            return ((await rg.GetVirtualNetworks().CreateOrUpdateAsync(Azure.WaitUntil.Completed, vnetName, vnetData,cancellationToken:token)).Value,true);
        } catch (OperationCanceledException){
            //propagate upward
            throw;
        } catch (Exception e){
            _ = Log(vmName,rgName,nameof(AllocateOrGetRG),$"{e}");
            return (null,true);
        }
    }

    private static async Task<(PublicIPAddressResource?,bool)> AllocateOrGetPip(
        string region,
        string vmName,
        string rgName,
        ResourceGroupResource rg,
        string pipName,
        CancellationToken token = default
    ){
        var pipR = await GetResourceAsync(
            vmName,
            rgName,
            a => rg.GetPublicIPAddresses().ExistsAsync(a,cancellationToken:token),
            a => rg.GetPublicIPAddressAsync(a,cancellationToken:token),
            pipName
        );

        if(pipR!=null) return (pipR,false);

        var publicIp = new PublicIPAddressData()
        {
            Location = region,
            Sku = new PublicIPAddressSku { Name = PublicIPAddressSkuName.Standard },
            PublicIPAllocationMethod = NetworkIPAllocationMethod.Static
        };

        _ = Log(vmName,rgName,nameof(AllocateOrGetPip),$"Creating Public Ip");
        try{
            return ((await rg.GetPublicIPAddresses().CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"{vmName}PublicIP", publicIp,cancellationToken:token)).Value,true);
        } catch (OperationCanceledException){
            //propagate upward
            throw;
        } catch (Exception e){
            _ = Log(vmName,rgName,nameof(AllocateOrGetRG),$"{e}");
            return (null,true);
        }
    }

    private static async Task<(NetworkSecurityGroupResource?,bool)> AllocateOrGetNsg(
        string region,
        string vmName,
        string rgName,
        ResourceGroupResource rg,
        AzureSettings settings,
        string nsgName,
        CancellationToken token = default
    ){
        var nsgR = await GetResourceAsync(
            vmName,
            rgName,
            a => rg.GetNetworkSecurityGroups().ExistsAsync(a,cancellationToken:token),
            a => rg.GetNetworkSecurityGroupAsync(a,cancellationToken:token),
            nsgName
        );

        if(nsgR!=null) return (nsgR,false);

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

        _ = Log(vmName,rgName,nameof(AllocateOrGetNsg),$"Creating Network Security Group");
        try{
            return ((await rg.GetNetworkSecurityGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, nsgName, nsg,cancellationToken:token)).Value,true);
        } catch (OperationCanceledException){
            //propagate upward
            throw;
        } catch (Exception e){
            _ = Log(vmName,rgName,nameof(AllocateOrGetRG),$"{e}");
            return (null,true);
        }
    }

    private static async Task<(NetworkInterfaceResource?,bool)> AllocateOrGetNic(
        string region,
        string vmName,
        string rgName,
        ResourceGroupResource rg,
        VirtualNetworkResource vnet,
        PublicIPAddressResource pip,
        NetworkSecurityGroupResource nsg,
        string nicName,
        bool completeBase,
        CancellationToken token = default
    ){
        var nicR = await GetResourceAsync(
            vmName,
            rgName,
            a => rg.GetNetworkInterfaces().ExistsAsync(a,cancellationToken:token),
            a => rg.GetNetworkInterfaceAsync(a,cancellationToken:token),
            nicName
        );

        //recreate nic if base resources had to be remade
        if(nicR!=null && completeBase) return (nicR,false);

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

        _ = Log(vmName,rgName,nameof(AllocateOrGetNic),$"Creating Network Interface");
        try{
            return ((await rg.GetNetworkInterfaces().CreateOrUpdateAsync(Azure.WaitUntil.Completed, nicName, nicData,cancellationToken:token)).Value,true);
        } catch (OperationCanceledException){
            //propagate upward
            throw;
        } catch (Exception e){
            _ = Log(vmName,rgName,nameof(AllocateOrGetRG),$"{e}");
            return (null,true);
        }
    }

    private static async Task<VirtualMachineResource?> AllocateOrGetVm(
        string region,
        string vmName,
        string rgName,
        ResourceGroupResource rg,
        NetworkInterfaceResource nic,
        string publicKey,
        AzureSettings settings,
        bool completeVM,
        string azureVmSize,
        CancellationToken token = default
    ){
        var vmR = await GetResourceAsync(
            vmName,
            rgName,
            a => rg.GetVirtualMachines().ExistsAsync(a,cancellationToken:token),
            a => rg.GetVirtualMachineAsync(a,cancellationToken:token),
            vmName
        );

        //need to recreate VM resource if any other part was recreated
        if(vmR!=null && completeVM){
            var vmSize = vmR.Data.HardwareProfile.VmSize.ToString();

            //if cannot find vmSize recreate VM
            if(vmSize is not null){
                _ = Log(vmName,rgName,nameof(AllocateOrGetVm),vmSize);
                //send vmSize to quota manager here

                return vmR;
            }
        }
        
        string? azureVmSizeName = null;
        if(settings.VmSize is not null){
            azureVmSizeName = await settings.VmSize.ToAzureName();
        }
        azureVmSizeName ??= "Standard_B2s";

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
                            KeyData = publicKey
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
            HardwareProfile = new VirtualMachineHardwareProfile() { VmSize = azureVmSizeName },
        };

        //adding data disks
        if(settings.DataDisks!=null){
            foreach(var disk in settings.DataDisks){
                var i = vmData.StorageProfile.DataDisks.Count + 1;
                _ = Log(vmName,rgName,nameof(AllocateOrGetVm),$"Attaching Disk {i}");
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

        //set disks to be deleted with VM deletion
        vmData.StorageProfile.OSDisk.DeleteOption = DiskDeleteOptionType.Delete;

        foreach(var disk in vmData.StorageProfile.DataDisks){
            disk.DeleteOption = DiskDeleteOptionType.Delete;
        }

        _ = Log(vmName,rgName,nameof(AllocateOrGetVm),$"Creating Virtual Machine");
        try{
            return (await rg.GetVirtualMachines().CreateOrUpdateAsync(Azure.WaitUntil.Completed, vmName, vmData,cancellationToken:token)).Value;
        } catch (OperationCanceledException){
            //propagate upward
            throw;
        } catch (Exception e){
            _ = Log(vmName,rgName,nameof(AllocateOrGetRG),$"{e}");
            return null;
        }
    }

    private static async Task<(string?,bool)> AllocateOrGetSshKeyPair(
        string region,
        string vmName,
        string rgName,
        ResourceGroupResource rg,
        string keyName,
        CancellationToken token = default
    ){
        var sshPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ssh",
            $"{rgName}"
        );
        var keyPath = Path.Combine(sshPath,$"{vmName}-Key.pem");

        var sshR = await GetResourceAsync(
            vmName,
            rgName,
            a => rg.GetSshPublicKeys().ExistsAsync(a,cancellationToken:token),
            a => rg.GetSshPublicKeyAsync(a,cancellationToken:token),
            keyName
        );

        if(sshR!=null){
            if(File.Exists(keyPath)){
                _ = Log(vmName,rgName,nameof(AllocateOrGetSshKeyPair),$"Found Private Key");
                return (sshR.Data.PublicKey,false);
            }
            
            _ = Log(vmName,rgName,nameof(AllocateOrGetSshKeyPair),$"Recreating Ssh Key Pair");
            try{
                await sshR.DeleteAsync(Azure.WaitUntil.Completed,cancellationToken:token);
            } catch (OperationCanceledException){
                //propagate upward
                throw;
            } catch (Exception e){
                _ = Log(vmName,rgName,nameof(AllocateOrGetSshKeyPair),$"{e}");
                return (null,true);
            }
        }

        _ = Log(vmName,rgName,nameof(AllocateOrGetSshKeyPair),$"Creating Ssh Key Pair");
        var keyData = new SshPublicKeyData(region)
        {
            PublicKey = null // Set this to null to let Azure generate the key
        };
        try{
            var key = (await rg.GetSshPublicKeys().CreateOrUpdateAsync(Azure.WaitUntil.Completed,keyName,keyData,cancellationToken:token)).Value;
            var pair = (await key.GenerateKeyPairAsync(cancellationToken:token)).Value;

            //Ensure directory exists and delete old key if found
            if(!Directory.Exists(sshPath)) Directory.CreateDirectory(sshPath);
            if(File.Exists(keyPath)) File.Delete(keyPath);

            await File.WriteAllTextAsync(keyPath, pair.PrivateKey, cancellationToken:token);

            //set key permission
            if(Syscall.chmod(keyPath, FilePermissions.S_IRUSR | FilePermissions.S_IWUSR)!=0) _ = Log(vmName,rgName,nameof(AllocateOrGetSshKeyPair),"chmod failure");

            return (pair.PublicKey,true);
        } catch (OperationCanceledException){
            //propagate upward
            throw;
        } catch (Exception e){
            _ = Log(vmName,rgName,nameof(AllocateOrGetRG),$"{e}");
            return (null,true);
        }
    }

    private static Task SendAlert(string message, Exception? exception = null){
        ErrorAlert?.Invoke(typeof(AzureManager),new(message,exception));
        return Task.CompletedTask;
    }

    private static async Task Log(string vmName, string rgName, string funcName, string input) =>
        await Console.Out.WriteLineAsync($"{rgName}:{vmName}:{funcName}:{input}");
}

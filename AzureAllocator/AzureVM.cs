using System.Diagnostics;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.ResourceManager.Network;
using FuncExt;
using AzureAllocator.Managers;

namespace AzureAllocator;

public class AzureVM
{
    public bool Connected = false;
    public bool Started => CheckStarted();
    public ulong? Guid => CheckGuid();

    public readonly string vmName;
    public readonly string rgName;
    public readonly string? ip;
    public readonly int HourlyCost;
    private readonly ResourceGroupResource rg;
    private readonly VirtualMachineResource vm;
    private readonly VirtualNetworkResource vnet;
    private readonly PublicIPAddressResource pip;
    private readonly NetworkSecurityGroupResource nsg;
    private readonly NetworkInterfaceResource nic;
    private readonly string keyName;

    private AzureVM(
        string vmName, 
        string rgName, 
        int HourlyCost,
        ResourceGroupResource rg,
        VirtualMachineResource vm,
        VirtualNetworkResource vnet,
        PublicIPAddressResource pip,
        NetworkSecurityGroupResource nsg,
        NetworkInterfaceResource nic,
        string keyName
    ){
        this.vmName = vmName;
        this.rgName = rgName;
        this.HourlyCost = HourlyCost;
        this.rg = rg;
        this.vm = vm;
        this.vnet = vnet;
        this.pip = pip;
        this.nsg = nsg;
        this.nic = nic;
        this.keyName = keyName;
        ip = pip.Data.IPAddress;
    }

    //use AzureManager allocator
    public static Task<bool> TryCreateAzureVMAsync(
        string vmName, 
        string rgName,
        int HourlyCost,
        ResourceGroupResource? rg,
        VirtualMachineResource? vm,
        VirtualNetworkResource? vnet,
        PublicIPAddressResource? pip,
        NetworkSecurityGroupResource? nsg,
        NetworkInterfaceResource? nic,
        string? keyName,
        out AzureVM? azureVM
    ){
        if(rg==null || vm==null || vnet==null || pip==null || nsg==null || nic==null || keyName==null){
            azureVM = null;
            return Task.FromResult(false);
        }

        azureVM = new(vmName, rgName, HourlyCost, rg, vm, vnet, pip, nsg, nic, keyName);
        return Task.FromResult(true);
    }

    public async Task Start(){
        if(!Started) await StartVM();
    }

    public async Task Deallocate(CancellationToken token = default){
        if(Started){
            _ = Log(nameof(Deallocate),$"Deallocating VM");
            await AzureManager.DeAllocate(rg,vm,vnet,pip,nsg,nic,keyName,token);
            Connected = false;
            _ = Log(nameof(Deallocate),$"Deallocated VM");
        }
    }

    public static Task<Process> BuildSshClient(AzureVM vm) =>
        Task.FromResult(
            new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ssh", 
                    Arguments =
                        $"-t " +
                        $"-o StrictHostKeyChecking=no " +
                        $"-i {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.ssh/{vm.rgName}/{vm?.vmName}-Key.pem " +
                        $"azureuser@{vm?.ip}",
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            }
        );

    public async Task StartSSH(Process _client){
        TaskCompletionSource<bool> _connected = new();
        CancellationTokenSource cts = new();

        _ = Log(nameof(StartSSH),$"trying to connect");
        _client.Start();

        var monitorTask = Task.Run(async () => {
            while(!_connected.Task.IsCompleted){
                cts.Token.ThrowIfCancellationRequested();
                string? output = await _client.StandardOutput.ReadLineAsync();
                if(output!=null && output.Contains("Welcome")){
                    _ = Log(nameof(StartSSH),$"welcome received");
                    _connected.TrySetResult(true);
                }
            };
        },cts.Token);

        await Ext.TimeoutTask(monitorTask,new(0,0,10),cts);
        Connected = true;

        _ = Log(nameof(StartSSH),$"ssh connection attempted");
    }
    //This connects through SSH and sends console input through the standard input
    public async Task ConsoleDirect(string input, Process _client){
        await Start();
        if (!Connected) await StartSSH(_client);
        _ = Log(nameof(ConsoleDirect),$"sent:{input}");
        await _client.StandardInput.WriteLineAsync(input);
    }
    //This runs a bash script on the machine
    public async Task RunScript(string script, CancellationToken token = default){
        await Start();
        var scriptParams = new RunCommandInput("RunShellScript")
        {
            Script = 
            {
                script
            }
        };
        _ = Log(nameof(RunScript),$"{script}");
        await vm.RunCommandAsync(Azure.WaitUntil.Completed, scriptParams,cancellationToken:token);
    }

    public static Task<Uri> GetDownloadSas(string container, string name){
        string connectionString = Environment.GetEnvironmentVariable("MOONFIRE_STORAGE_STRING") ?? "";
        var blobClient = new BlobClient(connectionString, container, name);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = container,
            BlobName = name,
            Resource = "b", //blob
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            Protocol = SasProtocol.Https
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder));
    }

    public async Task DownloadBlob(string container, string name, string destination, CancellationToken token = default){
        _ = Log(nameof(DownloadBlob),$"Downloading '{container}/{name}' to '{destination}'");
        await RunScript($"curl -o {destination} '{await GetDownloadSas(container,name)}'",token);
    }

    private async Task StartVM(){
        _ = Log(nameof(StartVM),$"Starting VM");

        await vm.PowerOnAsync(Azure.WaitUntil.Completed);
        
        _ = Log(nameof(StartVM),$"Started VM");
    }
    
    private bool CheckStarted(){
        return "VM running" == vm.InstanceView().Value.Statuses.FirstOrDefault(status => status.Code.StartsWith("PowerState/"))?.DisplayStatus;
    }

    private ulong CheckGuid(){
        return ulong.TryParse(Reg.NumericRegex().Match(rgName).Value,out var guid) ? guid : 0;
    }

    private async Task Log(string funcName, string input) =>
        await Console.Out.WriteLineAsync($"{rgName}:{vmName}:{funcName}:{input}");
}
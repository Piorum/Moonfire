using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System.Diagnostics;

namespace AzureAllocator;

public class AzureVM
{
    private readonly VirtualMachineResource vm;
    private readonly ResourceGroupResource rg;
    private bool connected = false;
    public bool Started => CheckStarted();
    public readonly string name;
    public readonly string rgname;
    public readonly string ip;

    public AzureVM(VirtualMachineResource _vm, string _name, string _ip, ResourceGroupResource _rg, string _rgname){
        name = _name;
        ip = _ip;
        vm = _vm;
        rg = _rg;
        rgname = _rgname;

        //check initial VM power state
        _ = Log("AzureVM",$"Started:{Started}");
    }
    public async Task Start(){
        if(!Started) await StartVM();
    }
    public async Task Deallocate(){
        if(Started){
            _ = Log(nameof(Deallocate),$"Deallocating VM");
            await AzureManager.DeAllocate(name,rgname,rg);
            connected = false;
            _ = Log(nameof(Deallocate),$"Deallocated VM");
        }
    }
    //This connects through SSH and sends console input through the standard input
    public async Task ConsoleDirect(string input, Process _client){
        await Start();
        if (!connected) await StartSSH(_client);
        _ = Log(nameof(ConsoleDirect),$"sent:{input}");
        await _client.StandardInput.WriteLineAsync(input);
    }
    public async Task StartSSH(Process _client){
        _ = Log(nameof(StartSSH),$"trying to connect");
        _client.Start();

        TaskCompletionSource<bool> _connectionAttempted = new();
        var monitorTask = Task.Run(async () => {
            while(!_connectionAttempted.Task.IsCompleted){
                string? output = await _client.StandardOutput.ReadLineAsync();
                if(output!=null && output.Contains("Welcome")){
                    _connectionAttempted.TrySetResult(true);
                    _ = Log(nameof(StartSSH),$"welcome received");
                }
                if(_client.StandardOutput.EndOfStream){
                    _client.Start();
                }
            };
        });

        //FAIL-OPEN logic, assumes welcome message was missed
        var timeoutTask = Task.Delay(10000).ContinueWith(_ => {
            if(!_connectionAttempted.Task.IsCompleted)
                _ = Log(nameof(StartSSH),$"connection attempt timed out");
            _connectionAttempted.TrySetResult(true);
        });

        await Task.WhenAny(monitorTask, timeoutTask);
        await Task.Delay(200); //leave time to connection to settle
        connected = true;

        _ = Log(nameof(StartSSH),$"connection attempted");
    }
    //This runs a bash script on the machine
    public async Task RunScript(string script){
        var scriptParams = new RunCommandInput("RunShellScript")
        {
            Script = 
            {
                script
            }
        };
        _ = Log(nameof(RunScript),$"{script}");
        await vm.RunCommandAsync(Azure.WaitUntil.Completed, scriptParams);
    }
    public Task<Uri> GetDownloadSas(string container, string name){
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
    public async Task DownloadBlob(string container, string name, string destination){
        _ = Log(nameof(DownloadBlob),$"Downloading '{container}/{name}' to '{destination}'");
        await RunScript($"curl -o {destination} '{await GetDownloadSas(container,name)}'");
    }
    private async Task StartVM(){
        _ = Log(nameof(StartVM),$"Starting VM");

        await vm.PowerOnAsync(Azure.WaitUntil.Completed);
        
        _ = Log(nameof(StartVM),$"Started VM");
    }
    private bool CheckStarted(){
        return "VM running" == vm.InstanceView().Value.Statuses.FirstOrDefault(status => status.Code.StartsWith("PowerState/"))?.DisplayStatus;
    }

    private async Task Log(string funcName, string input) =>
        await Console.Out.WriteLineAsync($"{rgname}:{name}:{funcName}:{input}");
}

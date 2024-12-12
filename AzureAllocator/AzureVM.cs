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
        Console.WriteLine($"{name} is Started: {Started}");
    }
    public async Task Start(){
        if(!Started) await StartVM();
    }
    public async Task Deallocate(){
        if(Started){
            Console.WriteLine($"{name}: Deallocating VM");
            await AzureManager.DeAllocate(rg);
            connected = false;
            Console.WriteLine($"{name}: Deallocated VM");
        }
    }
    //This connects through SSH and sends console input through the standard input
    public async Task ConsoleDirect(string input, Process _client){
        await Start();
        if (!connected) await StartSSH(_client);
        Console.WriteLine($"{name}: sent '{input}'");
        await _client.StandardInput.WriteLineAsync(input);
    }
    public async Task StartSSH(Process _client){
        Console.WriteLine($"{name}: StartSSH trying to connect");
        _client.Start();

        TaskCompletionSource<bool> _connectionAttempted = new();
        var monitorTask = Task.Run(async () => {
            while(!_connectionAttempted.Task.IsCompleted){
                string? output = await _client.StandardOutput.ReadLineAsync();
                if(output!=null && output.Contains("Welcome")){
                    _connectionAttempted.TrySetResult(true);
                }
                if(_client.StandardOutput.EndOfStream){
                    _client.Start();
                }
            }
            Console.WriteLine($"{name}: StartSSH welcome received");
        });

        //FAIL-OPEN logic, assumes welcome message was missed
        var timeoutTask = Task.Delay(10000).ContinueWith(_ => {
            if(!_connectionAttempted.Task.IsCompleted) 
                Console.WriteLine($"{name}: StartSSH connection welcome not received.");
            _connectionAttempted.TrySetResult(true);
        });

        await Task.WhenAny(monitorTask, timeoutTask);
        await Task.Delay(200); //leave time to connection to settle
        connected = true;

        Console.WriteLine($"{name}: StartSSH connection attempted");
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
        await vm.RunCommandAsync(Azure.WaitUntil.Completed, scriptParams);
    }
    public async Task DownloadBlob(string container, string name, string destination){
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

        Uri sasUri = blobClient.GenerateSasUri(sasBuilder);

        await RunScript($"curl -o {destination} '{sasUri}'");
    }
    public async Task DeAllocate(){
        //check for null, but should never be null here
        if(rg==null){
            await Console.Out.WriteLineAsync($"{name}: Resource Group Null, Deallocation Failed");
            return;
        }
        await Console.Out.WriteLineAsync($"{name}: Deallocating");
        await AzureManager.DeAllocate(rg);
        await Console.Out.WriteLineAsync($"{name}: Deallocated");
    }
    private async Task StartVM(){
        Console.WriteLine($"{name}: Starting VM");

        await vm.PowerOnAsync(Azure.WaitUntil.Completed);
        
        Console.WriteLine($"{name}: Started VM");
    }
    private bool CheckStarted(){
        return "VM running" == vm.InstanceView().Value.Statuses.FirstOrDefault(status => status.Code.StartsWith("PowerState/"))?.DisplayStatus;
    }
}

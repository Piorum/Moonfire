using Azure.Identity;
using Azure.ResourceManager;
using Azure.Storage.Blobs;
using System.Diagnostics;


namespace AzureAllocator;

public class Program{
    
    //entry point
    public static async Task Main(){
        var CONFIG_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Sunfire"
        );
        Environment.SetEnvironmentVariable(nameof(CONFIG_PATH),CONFIG_PATH,EnvironmentVariableTarget.Process);

        //loading .env
        var envPath = Path.Combine(
            Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "",
            ".env"
        );
        foreach(string line in File.ReadAllLines(envPath)){
            //From NAME="value" Env.Set(NAME,value,process)
            if(line[0]=='#') continue;
            Environment.SetEnvironmentVariable(line[0..line.IndexOf('=')],line[(line.IndexOf('=')+2)..line.LastIndexOf('"')],EnvironmentVariableTarget.Process);
        }

        //Building Sunfire
        var _process = new Process {
        StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = @"/home/username/Documents/Singularity/Sunfire/Sunfire",
                Arguments = @"build",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        _process.Start();
        _ = Task.Run(async () => {
            while(!_process.StandardOutput.EndOfStream){
                string? output = await _process.StandardOutput.ReadLineAsync();
                if(output!=null){
                    Console.WriteLine(output);
                }
            }
        });
        await _process.WaitForExitAsync();

        //Compressing Sunfire
        _process = new Process {
        StartInfo = new ProcessStartInfo
            {
                FileName = "tar",
                WorkingDirectory = @"/home/username/Documents/Singularity/Sunfire/Sunfire/bin/Debug/",
                Arguments = @"-czvf /home/username/Documents/Singularity/Builds/Sunfire.tar.gz ./net8.0",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        _process.Start();
        _ = Task.Run(async () => {
            while(!_process.StandardOutput.EndOfStream){
                string? output = await _process.StandardOutput.ReadLineAsync();
                if(output!=null){
                    Console.WriteLine(output);
                }
            }
        });
        await _process.WaitForExitAsync();

        //Compressing Sunfire Config
        _process = new Process {
        StartInfo = new ProcessStartInfo
            {
                FileName = "tar",
                WorkingDirectory = @"/home/username/.config/",
                Arguments = @"-czvf /home/username/Documents/Singularity/Builds/SunfireConfig.tar.gz ./Sunfire",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        _process.Start();
        _ = Task.Run(async () => {
            while(!_process.StandardOutput.EndOfStream){
                string? output = await _process.StandardOutput.ReadLineAsync();
                if(output!=null){
                    Console.WriteLine(output);
                }
            }
        });
        await _process.WaitForExitAsync();

        //Connecting to storage account
        string connectionString = Environment.GetEnvironmentVariable("MOONFIRE_STORAGE_STRING") ?? "";
        string filePath;

        //Uploading Sunfire
        BlobClient blobClient = new(connectionString, "bot", "Sunfire.tar.gz");
        filePath = @"/home/username/Documents/Singularity/Builds/Sunfire.tar.gz";
        using (FileStream uploadFileStream = File.OpenRead(filePath))
        {
            blobClient.Upload(uploadFileStream, overwrite: true);
        }

        //Uploading Sunfire Config
        blobClient = new(connectionString, "bot", "SunfireConfig.tar.gz");
        filePath = @"/home/username/Documents/Singularity/Builds/SunfireConfig.tar.gz";
        using (FileStream uploadFileStream = File.OpenRead(filePath))
        {
            blobClient.Upload(uploadFileStream, overwrite: true);
        }

        Console.WriteLine("Upload completed successfully.");

        //creating ArmClient
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "";
        var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? "";
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "";
        var subscription = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID") ?? "";
        ClientSecretCredential credential = new(tenantId, clientId, clientSecret);
        ArmClient client = new(credential, subscription);

        //loading settings
        var AzureSettingsPath = Path.Combine(
            Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "",
            "Config",
            "Bot.json"
        );
        var settings = await AzureSettings.CreateAsync(AzureSettingsPath);
        
        //allocating vm
        var vm = await AzureManager.Allocate(client, settings, "SunfireRG", "SunfireVM");

        //check for null, but should never be null here
        if(vm==null){
            return;
        }

        //Downloading Sunfire from storage account, extracting, running
        _ = Console.Out.WriteLineAsync("Running Install Script");
        var script = @"
            #!/bin/bash
            while [ ! -b /dev/sda ]; do
                :
            done

            apt-get install -y dotnet-runtime-8.0

            export HOME=/home/azureuser

            killall -9 Sunfire
            rm -rf ~/.config
            rm -rf ~/Sunfire.tar.gz
            rm -rf ~/Sunfire

            mkdir -p ~/.config &&
            ";
        script += $"curl -o ~/Sunfire.tar.gz '{await vm.GetDownloadSas(@"bot",@"Sunfire.tar.gz")}' &&\n";
        script += $"curl -o ~/.config/SunfireConfig.tar.gz '{await vm.GetDownloadSas(@"bot",@"SunfireConfig.tar.gz")}' &&\n";
        script += @"
            tar -xvzf ~/Sunfire.tar.gz -C ~/ &&
            tar -xvzf ~/.config/SunfireConfig.tar.gz -C ~/.config &&

            chmod -R 777 ~/.config &&
            chmod -R 777 ~/net8.0 &&
            
            sudo -u azureuser ~/net8.0/Sunfire > ~/Sunfire.log 2>&1 &
            ";
        await vm.RunScript(script); //This will never exit

        //setup datadisk
        /*var script = @"
            #!/bin/bash
            while [ ! -b /dev/sdc ]; do
                :
            done

            parted /dev/sdc --script mklabel gpt
            parted /dev/sdc --script mkpart primary ext4 0% 100%

            sudo mkfs.ext4 /dev/sdc1

            sudo mkdir -p /datadrive
            sudo mount /dev/sdc1 /datadrive

            sudo chmod -R 777 /datadrive
            ";
        _ = Console.Out.WriteLineAsync("Formatting Disk 1");
        await vm.RunScript(script);*/

        /*_ = Console.Out.WriteLineAsync("Downloading Blob");
        await vm.DownloadBlob(@"scpcontainer",@"scpcontainer.tar.gz",@"/datadrive/scpcontainer.tar.gz");
        _ = Console.Out.WriteLineAsync("Extracting");
        await vm.RunScript(@"tar -xvzf /datadrive/scpcontainer.tar.gz -C /datadrive");*/
    }
}
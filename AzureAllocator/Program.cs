using Azure.Identity;
using Azure.ResourceManager;
using Azure.Storage.Blobs;
using System.Diagnostics;


namespace AzureAllocator;

public class Program{
    
    //entry point
    public static async Task Main(){
        var ver = "Moonfire";

        var CONFIG_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ver
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

        //build and deploy application
        await SetupHelper.UpdateBuild(ver);

    }
}

public static class SetupHelper{
    public static async Task UpdateBuild(string ver){
        //building project
        await BuildDotnet($"/home/username/Documents/Singularity/{ver}/{ver}");
        _ = Console.Out.WriteLineAsync("Project Built");

        //uploading to storage account
        await UploadFolder(
            $"/home/username/Documents/Singularity/{ver}/{ver}/bin/Debug/",@"net8.0",
            $"{ver}.tar.gz");
        await UploadFolder(
            $"/home/username/.config/",$"{ver}",
            $"{ver}Config.tar.gz");
        _ = Console.Out.WriteLineAsync("Build Uploaded");

        //loading application hardware settings
        var AzureSettingsPath = Path.Combine(
            Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "",
            "Config",
            "Bot.json"
        );
        var settings = await AzureSettings.CreateAsync(AzureSettingsPath);
        
        //allocating vm
        var vm = await AzureManager.Allocate(await AzureManager.BuildArmClient(), settings, $"{ver}RG", $"{ver}VM");
        _ = Console.Out.WriteLineAsync("VM Allocated");

        //check for null, but should never be null here
        if(vm==null){
            return;
        }

        await InstallScript(vm,ver);
        _ = Console.Out.WriteLineAsync("Install Script Finished");
    }

    private static async Task InstallScript(AzureVM vm, string ver){
        _ = Console.Out.WriteLineAsync("Running Install Script");
        
        var script = "";
        void f(string a) => script += a + Environment.NewLine;
        f(@"#!/bin/bash");
        f(@"while [ ! -b /dev/sda ]; do");
        f(@"    :");
        f(@"done");

        f(@"apt-get install -y dotnet-runtime-8.0");

        f(@"export HOME=/home/azureuser");

        f($"killall -9 {ver}");
        f(@"rm -rf ~/.config");
        f($"rm -rf ~/{ver}.tar.gz");
        f($"rm -rf ~/{ver}");

        f(@"mkdir -p ~/.config");

        f($"curl -o ~/{ver}.tar.gz '{await vm.GetDownloadSas(@"bot",$"{ver}.tar.gz")}'");
        f($"curl -o ~/.config/{ver}Config.tar.gz '{await vm.GetDownloadSas(@"bot",$"{ver}Config.tar.gz")}'");

        f($"tar -xvzf ~/{ver}.tar.gz -C ~/");
        f($"tar -xvzf ~/.config/{ver}Config.tar.gz -C ~/.config");

        f(@"chmod -R 777 ~/.config");
        f(@"chmod -R 777 ~/net8.0");

        f($"sudo -u azureuser ~/net8.0/{ver} > ~/{ver}.log 2>&1 &");
        
        await vm.RunScript(script);
    }

    private static async Task BuildDotnet(string cwd){
        var _process = new Process {
        StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = cwd,
                Arguments = @"build",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        await RunProcess(_process);
    }

    private static async Task UploadFolder(string cwd, string folder, string target){
        var _process = new Process {
        StartInfo = new ProcessStartInfo
            {
                FileName = "tar",
                WorkingDirectory = cwd,
                Arguments = $"-czvf /home/username/Documents/Singularity/Builds/{target} ./{folder}",
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        await RunProcess(_process);

        await UploadTar(target);
    }

    private static async Task RunProcess(Process _process){
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
    }

    private static async Task UploadTar(string file){
        string connectionString = Environment.GetEnvironmentVariable("MOONFIRE_STORAGE_STRING") ?? "";
        string filePath = @"/home/username/Documents/Singularity/Builds/";

        BlobClient blobClient = new(connectionString, "bot", file);
        filePath = Path.Combine(filePath,file);

        using FileStream uploadFileStream = File.OpenRead(filePath);
        await blobClient.UploadAsync(uploadFileStream, overwrite: true);
    }
}
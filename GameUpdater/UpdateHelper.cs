using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Diagnostics;
using AzureAllocator;

namespace GameUpdater;

public static class UpdateHelper
{
    public static async Task UpdateSCP(){
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await Console.Out.WriteLineAsync("Updating SCP");
        await AzureManager.StoreTableEntity("Updatefire","updating","game","scp",true);
        await RunBash($"steamcmd",$"+force_install_dir {home}/Documents/SCP +login anonymous +app_update 996560 -beta public validate +quit");
        await RunBash($"rm",$"-rf {home}/Documents/scpcontainer");
        await RunBash($"rm",$"-rf {home}/Documents/scpcontainer.tar.gz");
        await RunBash($"cp",$"-r {home}/Documents/SCP {home}/Documents/AzureContainers/scpcontainer");
        await RunBash($"cp",$"-r {home}/Documents/AzureContainers/SCPFILES/config {home}/Documents/AzureContainers/scpcontainer/config");
        await RunBash($"tar",$"-czvf {home}/Documents/AzureContainers/scpcontainer.tar.gz ./scpcontainer",$"{home}/Documents/AzureContainers");
        await UploadTar("scpcontainer.tar.gz","scpcontainer",$"{home}/Documents/AzureContainers/");
        await AzureManager.StoreTableEntity("Updatefire","updating","game","scp",false);
    }

    public static async Task UpdateMINECRAFT(){
        await Console.Out.WriteLineAsync("Updating MINECRAFT");
    }

    public static async Task UpdateGMOD(){
        await Console.Out.WriteLineAsync("Updating GMOD");
    }

    public static async Task MaintenanceLock(string ver){
        await Console.Out.WriteLineAsync("Locking Bot For Maintenance");

        await AzureManager.StoreTableEntity("Updatefire","maintenance","bot","rebuilding",true);

        //i = to maintenace period in minutes
        for(var i = 3; i > 0; i--){
            await Console.Out.WriteLineAsync($"{i} minutes to restart");
            await AzureManager.StoreTableEntity("Updatefire","maintenance","bot","time",i);
            await Task.Delay(60000);
        }

        await SetupHelper.UpdateBuild(ver);

        await AzureManager.StoreTableEntity("Updatefire","maintenance","bot","rebuilding",false);
    }

    private static async Task RunBash(string app, string arg, string? cwd = null){
        cwd ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var _process = new Process {
        StartInfo = new ProcessStartInfo
            {
                FileName = app,
                WorkingDirectory = cwd,
                Arguments = arg,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        await RunProcess(_process);
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

    private static async Task UploadTar(string file, string container, string filePath){
        string connectionString = Environment.GetEnvironmentVariable("MOONFIRE_STORAGE_STRING") ?? "";

        BlobClient blobClient = new(connectionString, container, file);


        filePath = Path.Combine(filePath,file);
        long fileSize = new FileInfo(filePath).Length;
        using FileStream uploadFileStream = File.OpenRead(filePath);

        if (await blobClient.ExistsAsync())
        {
            Console.WriteLine("Blob already exists. Deleting Old");
            await blobClient.DeleteAsync();
        }

        var progressHandler = new Progress<long>(progress =>
        {
            double percentage = (double)progress / fileSize * 100;
            Console.WriteLine($"Uploaded {progress} bytes of {fileSize} bytes. ({percentage:0.00}%)");
        });

        await Console.Out.WriteLineAsync($"Uploading File {filePath}");
        await blobClient.UploadAsync(uploadFileStream, new BlobUploadOptions
            {
                TransferOptions = new StorageTransferOptions
                {
                    MaximumTransferSize = 16 * 1024 * 1024
                },
                ProgressHandler = progressHandler
            });
        await Console.Out.WriteLineAsync($"Uploaded File {filePath}");
    }
}

using System.Diagnostics;

namespace SCDisc;

public class SCPProcessInterface
{   
    private const string serverDirectory = @"SCP/"; // *Directory inside documents folder
    private const string processName = "LocalAdmin"; // This shouldn't change
    private const string port = "7777"; // Will be passed to server
    private readonly Process _process;
    private bool _started = false;

    public SCPProcessInterface(){
        string documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal); //Documents Path -Cross Platform Implementation

        ProcessStartInfo startInfo = new(){
            FileName = Path.Combine(documentsDir, serverDirectory, processName), //Process requires full path to executeable
            Arguments = port,
            WorkingDirectory = Path.Combine(documentsDir, serverDirectory), //SCP Server requires working enviroment be the same as the containing folder
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = new Process{
            StartInfo = startInfo
        };
    }

    public async Task StartServer(){
        if(!_started){
            _process.Start();

            TaskCompletionSource<bool> _heartbeatReceived = new();

            //Read the output asynchronously to console
            _ = Task.Run(async () => {
                while(!_process.StandardOutput.EndOfStream){
                    string? output = await _process.StandardOutput.ReadLineAsync();
                    if(output!=null){
                        Console.WriteLine(output);
                        if(output.Contains("Received first heartbeat.")) _heartbeatReceived.TrySetResult(true);
                    }
                }
            });

            //Waits for heartbeat report to continue
            await _heartbeatReceived.Task;
            _started = true;

        }else{
            Console.WriteLine("Process should already be started.");
        }
    }

    public async Task StopServer(){
        if(_started){
            await SendConsoleInput("exit");
            if(_process.WaitForExit(5000)) _process.Kill();
            _started = false;
        }else{
            Console.WriteLine("Process should already be dead.");
        }
    }

    public async Task SendConsoleInput(string input){   
        if(input=="stop"){
            await StopServer();
        }else if(_started){
            await _process.StandardInput.WriteLineAsync(input);
        }else{
            Console.WriteLine($"Process is not started!!! Attemped Input: {input}");
        }
    }

}
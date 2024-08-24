using System;
using System.Diagnostics;

namespace SCDisc;

public class SCPProcessInterface
{   
    private const string serverDirectory = @"Documents/SCP/"; // ~/ implied
    private const string processName = "LocalAdmin"; // This shouldn't change
    private const string port = "7777"; // Will be passed to server
    private readonly Process _process;
    private bool _started;

    public SCPProcessInterface(){
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), serverDirectory, processName),
            Arguments = port,
            WorkingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), serverDirectory),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = new Process
        {
            StartInfo = startInfo
        };
    }

    public async Task StartServer(){
        if(!_started){
            _process.Start();

            TaskCompletionSource<bool> _heartbeatReceived = new();

            //Read the output asynchronously to console
            _ = Task.Run(async () => 
            {
                while (!_process.StandardOutput.EndOfStream)
                {
                    string? output = await _process.StandardOutput.ReadLineAsync();
                    if(output!=null) {
                        Console.WriteLine(output);
                        if(output.Contains("Received first heartbeat. Silent crash detection is now active.")) _heartbeatReceived.TrySetResult(true);
                    }
                }
            });

            //Waits for heartbeat report to end
            await _heartbeatReceived.Task;
            _started = true;

        } else {
            Console.WriteLine("Process should already be started.");
        }
    }

    public async Task StopServer(){
        if (_process != null && !_process.HasExited){
            //Attempts to exit
            await SendConsoleInput("exit");
            bool exited = _process.WaitForExit(5000);
            //Kills after 5 seconds of waiting
            if (!exited)
            {
                _process.Kill();
            }
            _started = false;
        } else {
            Console.WriteLine("Process should already be dead.");
        }
    }

    public async Task SendConsoleInput(string input)
    {
        if (_process != null && !_process.HasExited){
            await _process.StandardInput.WriteLineAsync(input);
        } else{
            Console.WriteLine($"Process is not started!!! Attemped Input: {input}");
        }
    }

}

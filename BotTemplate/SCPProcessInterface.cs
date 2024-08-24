using System;
using System.Diagnostics;

namespace SCDisc;

public class SCPProcessInterface
{   
    private const string serverDirectory = @"Documents/SCP/";
    private const string processName = "LocalAdmin";
    private const string port = "7777";
    private Process _process;
    private bool _started;

    public SCPProcessInterface(){
        ProcessStartInfo startInfo = new ProcessStartInfo
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
            _started = true;

            //Read the output asynchronously
            await Task.Run(() => 
            {
                while (!_process.StandardOutput.EndOfStream)
                {
                    string? output = _process.StandardOutput.ReadLine();
                    if(output!=null) Console.WriteLine(output);
                }
            });
        }
    }

    public async Task StopServer(){
        if (_process != null && !_process.HasExited){
            await SendConsoleInput("exit");
            bool exited = _process.WaitForExit(5000);
            if (!exited)
            {
                _process.Kill();
            }
            _started = false;
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

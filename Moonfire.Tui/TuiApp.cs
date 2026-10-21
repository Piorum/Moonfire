using Moonfire.Input;
using Moonfire.Rendering;
using Moonfire.Rendering.Interfaces;
using Moonfire.Logging;
using Moonfire.Logging.Models;
using Moonfire.Logging.Sinks;

namespace Moonfire.Tui;

public class TuiApp(IMoonfireView rootView, TuiAppOptions? options = null)
{
    public InputHandler InputHandler = new(options?.SequenceKeybindsTimeoutMs);
    public Renderer Renderer = new(new() { View = rootView }, options?.RendererBatchTimeout);

    private readonly CancellationTokenSource cts = new();

    public static void InitLogging(List<LogLevel> logLevels) =>
        Logger.AddSink(new(new BufferSink(), [.. logLevels]));

    public void Run(bool dumpLogs = true)
    {
        void cancelHandler(object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Stop();
        }
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Stop();
        };;

        try
        {
            InputHandler.Run(cts.Token);

            var renderThread = new Thread(() =>
            {
                try
                {
                    Renderer.Run(cts.Token);
                }
                catch (Exception ex)
                {
                    Logger.Error(nameof(Tui), $"Renderer Crash:\n{ex}");
                }
            })
            {
                IsBackground = true,
                Name = "MoonfireRenderer"
            };

            renderThread.Start();
            renderThread.Join();
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            Stop();
        }

        Logger.Debug(nameof(Tui), "[Shutdown]");
        if(dumpLogs)
            Logger.StopAndFlush();
    }

    public void Stop()
    {
        if(!cts.IsCancellationRequested)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException) { }
        }
    }
}

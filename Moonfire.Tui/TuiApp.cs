using Moonfire.Input;
using Moonfire.Rendering;
using Moonfire.Rendering.Interfaces;
using Moonfire.Logging;
using Moonfire.Logging.Models;
using Moonfire.Logging.Sinks;

namespace Moonfire.Tui;

public class TuiApp
{
    public InputHandler InputHandler = new();
    public Renderer Renderer;

    public TuiApp(IMoonfireView rootView)
    {
        Renderer = new(new() { View = rootView });
    }

    public static async Task InitLogging(List<LogLevel> logLevels) =>
        await Logger.AddSink(new(new BufferSink(), [.. logLevels]));

    public async Task Run(bool dumpLogs = true)
    {
        CancellationTokenSource cts = new();

        var inputTask = InputHandler.Run(cts.Token);
        var renderTask = Renderer.Run(cts.Token);

        _ = await Task.WhenAny(inputTask, renderTask);

        await Stop(cts);

        try
        {
            await Task.WhenAll(inputTask, renderTask);
        }
        catch (Exception ex)
        {
            var exs = ex is AggregateException ae ? ae.InnerExceptions : (IEnumerable<Exception>)[ex];
            foreach(var ie in exs)
                await Logger.Error(nameof(Tui), $"Major Exception:\n{ex}");
        }

        await Logger.Debug(nameof(Tui), "[Shutdown]");
        if(dumpLogs)
            await Logger.StopAndFlush();
    }

    private static async Task Stop(CancellationTokenSource cts)
    {
        if(cts is not null)
            try
            {
                await cts.CancelAsync();
                cts.Dispose();
            }
            catch (ObjectDisposedException) { }
    }
}

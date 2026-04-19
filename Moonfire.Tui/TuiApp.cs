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

    public static async Task InitLogging(List<LogLevel> logLevels) =>
        await Logger.AddSink(new(new BufferSink(), [.. logLevels]));

    public async Task Run(bool dumpLogs = true)
    {
        cts.TryReset();

        var inputTask = InputHandler.Run(cts.Token);
        var renderTask = Renderer.Run(cts.Token);

        _ = await Task.WhenAny(inputTask, renderTask);

        await Stop();

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

    public async Task Stop()
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

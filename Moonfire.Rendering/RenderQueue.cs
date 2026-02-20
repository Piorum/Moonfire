using System.Threading.Channels;
using Moonfire.Logging;
using Moonfire.Shared;

namespace Moonfire.Rendering;

internal class RenderQueue
{
    private readonly Channel<Func<Task>> renderQueue = Channel.CreateUnbounded<Func<Task>>();

    private readonly PrecisionDelay precisionDelay = new();
    private readonly List<Task> runningTasks = [];

    internal readonly List<(int x, int y, int w, int h)> clearTasks = [];
    internal readonly List<Func<Task>> postRenderTasks = [];

    internal async Task<bool> WaitAndRun(TimeSpan batchTimeout, CancellationToken token)
    {
        runningTasks.Clear();

        //Wait for first action
        var firstAction = await renderQueue.Reader.ReadAsync(token);
        await TryAddAction(runningTasks, firstAction);

        //Wait for batch timeout
        await precisionDelay.Delay(batchTimeout, token);

        //Drain queue
        while (renderQueue.Reader.TryRead(out var action))
            await TryAddAction(runningTasks, action);

        await TryAwaitTasks(runningTasks);

        return runningTasks.Count > 0 && !token.IsCancellationRequested;
    }

    internal async Task RunPostRenderTasks()
    {
        if(postRenderTasks.Count > 0)
        {
            List<Task> tasks = [];

            foreach(var task in postRenderTasks)
                await TryAddAction(tasks, task);

            await TryAwaitTasks(tasks);

            postRenderTasks.Clear();

        }
    }

    private async static Task TryAddAction(List<Task> tasks, Func<Task> action)
    {
        //Sync async tasks will start immediately, this will protect the render from errors thrown here.
        try { 
            tasks.Add(action()); 
            }
        catch (Exception ex) 
        { 
            await Logger.Error(nameof(Rendering), $"Action Failed To Start\n{ex}"); 
        }
    }

    private async Task TryAwaitTasks(List<Task> tasks)
    {
        //Catch and log exceptions but don't crash the renderer
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            var exs = ex is AggregateException ae ? ae.InnerExceptions : (IEnumerable<Exception>)[ex];
            foreach(var ie in exs)
                _ = Logger.Error(nameof(Rendering), $"Render Task Failed\n{ex}");
        }
    }
    
    internal async Task EnqueueAction(Func<Task> action) =>
        await renderQueue.Writer.WriteAsync(action);
    internal async Task EnqueueActionClear(int x, int y, int w, int h) =>
        await EnqueueAction(async () => clearTasks.Add((x, y, w, h)));
    internal async Task EnqueueActionPostRender(Func<Task> action) =>
        await EnqueueAction(async () => postRenderTasks.Add(action));


}

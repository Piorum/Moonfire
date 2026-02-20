using System.Threading.Channels;
using Moonfire.Logging;

namespace Moonfire.Rendering;

internal class RenderQueue
{
    private readonly Channel<Func<Task>> renderQueue = Channel.CreateUnbounded<Func<Task>>();

    private readonly List<Task> runningTasks = [];

    internal readonly List<(int x, int y, int w, int h)> clearTasks = [];
    internal readonly List<Func<Task>> postRenderTasks = [];

    internal async Task<bool> WaitAndRun(TimeSpan batchTimeout, CancellationToken token)
    {
        runningTasks.Clear();

        //Get first action and start batch timer
        var firstAction = await renderQueue.Reader.ReadAsync(token);

        try { runningTasks.Add(firstAction()); }
        catch (Exception ex) { await Logger.Error(nameof(Rendering), $"Action Failed To Start\n{ex}"); }

        var batchTimer = Task.Delay(batchTimeout, token);

        while (true)
        {
            //Read any available actions
            while (renderQueue.Reader.TryRead(out var action))
                try { runningTasks.Add(action()); }
                catch (Exception ex) { await Logger.Error(nameof(Rendering), $"Action Failed To Start\n{ex}"); }

            //Wait for more actions or for batch timer to end
            var waitForMoreActions = renderQueue.Reader.WaitToReadAsync(token).AsTask();
            var completedTask = await Task.WhenAny(waitForMoreActions, batchTimer);

            //Exit if batch timer is done, loop if not (more actions were queued)
            if (completedTask == batchTimer)
                break;
        }


        await AwaitAndCatchTasks(runningTasks);

        return runningTasks.Count > 0 && !token.IsCancellationRequested;
    }

    internal async Task RunPostRenderTasks()
    {
        if(postRenderTasks.Count > 0)
        {
            List<Task> tasks = [];

            foreach(var task in postRenderTasks)
                try { tasks.Add(task()); }
                catch (Exception ex) { await Logger.Error(nameof(Rendering), $"Action Failed To Start\n{ex}"); }

            await AwaitAndCatchTasks(tasks);

            postRenderTasks.Clear();

        }
    }

    private async Task AwaitAndCatchTasks(List<Task> tasks)
    {
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

using System.Collections.Concurrent;
using Moonfire.Logging;
using Moonfire.Shared;

namespace Moonfire.Rendering;

internal class RenderQueue
{
    // BlockingCollection replaces Channel for synchronous producer-consumer queuing
    private readonly BlockingCollection<Action> renderQueue = [];

    private readonly List<Action> runningActions = [];

    internal readonly List<(int x, int y, int w, int h)> clearTasks = [];
    internal readonly List<Action> postRenderTasks = [];

    internal bool WaitAndRun(TimeSpan batchTimeout, CancellationToken token)
    {
        runningActions.Clear();

        // Wait for the first action to arrive (blocks synchronously until an item is available or canceled)
        var firstAction = renderQueue.Take(token);
        runningActions.Add(firstAction);

        // Wait for batch timeout to allow near-simultaneous render commands to arrive
        PrecisionTimer.SpinWait(batchTimeout, 20, token);

        // Drain any actions that arrived during the wait
        while (renderQueue.TryTake(out var action))
        {
            runningActions.Add(action);
        }

        // Execute all batched actions
        foreach (var action in runningActions)
        {
            ExecuteAction(action);
        }

        return runningActions.Count > 0 && !token.IsCancellationRequested;
    }

    internal void RunPostRenderTasks()
    {
        if (postRenderTasks.Count > 0)
        {
            foreach (var action in postRenderTasks)
            {
                ExecuteAction(action);
            }

            postRenderTasks.Clear();
        }
    }

    private static void ExecuteAction(Action action)
    {
        // Catch and log exceptions so an error in one action doesn't crash the renderer
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.Error(nameof(Rendering), $"Render Action Failed\n{ex}");
        }
    }

    internal void EnqueueAction(Action action) =>
        renderQueue.Add(action);

    internal void EnqueueActionClear(int x, int y, int w, int h) =>
        EnqueueAction(() => clearTasks.Add((x, y, w, h)));

    internal void EnqueueActionPostRender(Action action) =>
        EnqueueAction(() => postRenderTasks.Add(action));
}
using System.Diagnostics;

namespace Moonfire.Shared;

public static class PrecisionTimer
{
    public static void SpinWait(TimeSpan timeSpan, int spinIterations = 20, CancellationToken token = default)
    {
        if(token.IsCancellationRequested)
            return;

        var startTick = Stopwatch.GetTimestamp();
        long waitTicks = (long)(timeSpan.TotalSeconds * Stopwatch.Frequency);
        long targetTick = startTick + waitTicks;

        while(Stopwatch.GetTimestamp() < targetTick)
            Thread.SpinWait(spinIterations);
    }
}

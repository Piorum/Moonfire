using System.Diagnostics;

namespace Moonfire.Shared;

public class PrecisionDelay(int spinIterations = 20)
{
    public Task Delay(TimeSpan timeSpan, CancellationToken token = default)
    {
        var sw = Stopwatch.StartNew();
        double targetTicks = timeSpan.TotalMicroseconds * Stopwatch.Frequency / 1_000_000.0;

        while(sw.ElapsedTicks < targetTicks && !token.IsCancellationRequested)
            Thread.SpinWait(spinIterations);

        return Task.CompletedTask;
    }
}

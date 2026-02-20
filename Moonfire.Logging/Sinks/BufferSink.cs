using System.Collections.Concurrent;
using System.Text;
using Moonfire.Ansi;
using Moonfire.Ansi.Models;
using Moonfire.Logging.Interfaces;
using Moonfire.Logging.Models;

namespace Moonfire.Logging.Sinks;

public class BufferSink(int? capacity = null) : ILogSink
{
    private static readonly Dictionary<string, AnsiTruecolor> providerColors = [];
    private static readonly List<AnsiTruecolor> colors =
    [
        new(255,65,74),
        new(69,255,190),
        new(239,252,122),
        new(255,148,54),
        new(28,174,252)
    ];
    private static readonly Random rng = new((int)DateTime.Now.Ticks);

    private readonly ConcurrentQueue<string> logBuffer = new();

    private readonly int _capacity = capacity is not null ? capacity > 0 ? (int)capacity : 1000 : 1000;

    public Task WriteAsync(LogMessage message)
    {
        AnsiStringBuilder asb = new();

        var providerColor = GetProviderColor(message.Provider);

        asb
        .Append
        (
            $"[{message.CreationTime:HH:mm:ss.fffffff}] ",
            new(ForegroundColor: new(221, 221, 221), Properties: AnsiProperty.Bold),
            null
        )
        .Append
        (
            $"[{message.Level}] ",
            new(ForegroundColor: GetLogLevelColor(message.Level), Properties: AnsiProperty.Bold),
            null
        )
        .Append
        (
            $"[{message.Provider}] ",
            new(ForegroundColor: providerColor, Properties: AnsiProperty.Bold),
            null
        )
        .Append
        (
            message.Message,
            new(ForegroundColor: providerColor),
            null
        )
        .ResetPropertiesNewLine();

        logBuffer.Enqueue(asb.ToString());

        while (logBuffer.Count > _capacity)
            logBuffer.TryDequeue(out _);

        return Task.CompletedTask;
    }

    public async Task Flush()
    {
        StringBuilder sb = new();
        foreach (var log in logBuffer)
            sb.Append(log);

        await Console.Out.WriteLineAsync(sb.ToString());
    }

    private static AnsiTruecolor GetLogLevelColor(LogLevel level)
    {
        return level switch
        {   
            LogLevel.Debug => new(168,230,207),
            LogLevel.Info => new(220,237,193),
            LogLevel.Warn => new(255,211,182),
            LogLevel.Error => new(255,170,165),
            LogLevel.Fatal => new(255,139,149),
            _ => new(255, 255, 255)
        };
    }

    private static AnsiTruecolor GetProviderColor(string provider)
    {
        if (!providerColors.TryGetValue(provider, out var color))
        {
            color = PickProviderColor();
            providerColors[provider] = color;
        }

        return color;
    }

    private static AnsiTruecolor PickProviderColor()
    {
        AnsiTruecolor color;
        if (colors.Count > 0)
        {
            color = colors[rng.Next(0, colors.Count )];
            colors.Remove(color);
        }
        else
        {
            color = new
            (
                (byte)rng.Next(100, 255),
                (byte)rng.Next(100, 255),
                (byte)rng.Next(100, 255)
            );
        }
        return color;
    }
}

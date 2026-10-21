using System.Collections.Concurrent;
using System.Text;
using Moonfire.Ansi;
using Moonfire.Ansi.Models;
using Moonfire.Logging.Interfaces;
using Moonfire.Logging.Models;

namespace Moonfire.Logging.Sinks;

public class BufferSink(int? capacity = null) : ILogSink
{
    private static readonly Lock _colorLock = new();
    private static readonly Dictionary<string, AnsiTruecolor> _providerColors = [];
    private static readonly List<AnsiTruecolor> _availableColors =
    [
        new(255, 65, 74),
        new(69, 255, 190),
        new(239, 252, 122),
        new(255, 148, 54),
        new(28, 174, 252)
    ];

    private readonly ConcurrentQueue<string> _logBuffer = new();
    private readonly int _capacity = capacity is > 0 ? capacity.Value : 1000;

    public void Write(LogMessage message)
    {
        AnsiStringBuilder asb = new();
        var providerColor = GetProviderColor(message.Provider);

        asb
            .Append(
                $"[{message.CreationTime:HH:mm:ss.fffffff}] ",
                new(ForegroundColor: new(221, 221, 221), Properties: AnsiProperty.Bold),
                null
            )
            .Append(
                $"[{message.Level}] ",
                new(ForegroundColor: GetLogLevelColor(message.Level), Properties: AnsiProperty.Bold),
                null
            )
            .Append(
                $"[{message.Provider}] ",
                new(ForegroundColor: providerColor, Properties: AnsiProperty.Bold),
                null
            )
            .Append(
                message.Message,
                new(ForegroundColor: providerColor),
                null
            )
            .ResetPropertiesNewLine();

        _logBuffer.Enqueue(asb.ToString());

        while (_logBuffer.Count > _capacity && _logBuffer.TryDequeue(out _)) { }
    }

    public void Flush()
    {
        StringBuilder sb = new();
        foreach (var log in _logBuffer)
        {
            sb.Append(log);
        }

        Console.Out.Write(sb.ToString());
        Console.Out.Flush();
    }

    private static AnsiTruecolor GetLogLevelColor(LogLevel level) => level switch
    {
        LogLevel.Debug => new(168, 230, 207),
        LogLevel.Info  => new(220, 237, 193),
        LogLevel.Warn  => new(255, 211, 182),
        LogLevel.Error => new(255, 170, 165),
        LogLevel.Fatal => new(255, 139, 149),
        _              => new(255, 255, 255)
    };

    private static AnsiTruecolor GetProviderColor(string provider)
    {
        lock (_colorLock)
        {
            if (!_providerColors.TryGetValue(provider, out var color))
            {
                color = PickProviderColor();
                _providerColors[provider] = color;
            }
            return color;
        }
    }

    private static AnsiTruecolor PickProviderColor()
    {
        if (_availableColors.Count > 0)
        {
            int index = Random.Shared.Next(0, _availableColors.Count);
            var color = _availableColors[index];
            _availableColors.RemoveAt(index);
            return color;
        }

        return new(
            (byte)Random.Shared.Next(100, 255),
            (byte)Random.Shared.Next(100, 255),
            (byte)Random.Shared.Next(100, 255)
        );
    }
}
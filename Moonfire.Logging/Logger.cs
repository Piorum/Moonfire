using System.Threading.Channels;
using Moonfire.Logging.Interfaces;
using Moonfire.Logging.Models;

namespace Moonfire.Logging;

public static class Logger
{
    private static readonly List<SinkConfiguration> sinks = [];

    private static readonly Channel<LogMessage> channel = Channel.CreateUnbounded<LogMessage>();

    private static readonly Task logTask;
    private static readonly CancellationTokenSource cts;

    static Logger()
    {
        cts = new();
        logTask = Task.Run(() => Log(cts.Token));
    }

    public static Task AddSink(SinkConfiguration sinkConfiguration)
    {
        sinks.Add(sinkConfiguration);
        return Task.CompletedTask;
    }

    public static async Task StopAndFlush()
    {
        channel.Writer.Complete();
        cts?.Cancel();

        if (logTask is not null)
            await logTask;

        foreach (var sink in sinks)
            await sink.Sink.Flush();
    }

    public static async Task Debug(string provider, string message) => await LogMessage(provider, message, LogLevel.Debug);
    public static async Task Info(string provider, string message) => await LogMessage(provider, message, LogLevel.Info);
    public static async Task Warn(string provider, string message) => await LogMessage(provider, message, LogLevel.Warn);
    public static async Task Error(string provider, string message) => await LogMessage(provider, message, LogLevel.Error);
    public static async Task Fatal(string provider, string message) => await LogMessage(provider, message, LogLevel.Fatal);
    private static Task LogMessage(string provider, string message, LogLevel logLevel)
    {
        channel.Writer.TryWrite(new(DateTime.Now, logLevel, provider, message));
        return Task.CompletedTask;
    }

    private static async Task Log(CancellationToken token)
    {
        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(token))
            {
                var tasks = sinks.Where(s => s.Levels.Contains(message.Level)).Select(s => WriteAndCatch(s.Sink, message));

                await Task.WhenAll(tasks);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task WriteAndCatch(ILogSink sink, LogMessage message)
    {
        try
        {
            await sink.WriteAsync(message);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[{DateTime.Now}] [Error] [{nameof(Logging)}] {ex}");
        }
    }
}
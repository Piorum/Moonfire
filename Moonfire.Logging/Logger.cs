using System.Collections.Concurrent;
using Moonfire.Logging.Models;

namespace Moonfire.Logging;

public static class Logger
{
    private static readonly List<SinkConfiguration> _sinks = [];
    private static readonly BlockingCollection<LogMessage> _queue = new();
    private static readonly Thread _logThread;

    static Logger()
    {
        _logThread = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "MoonfireLogger"
        };
        _logThread.Start();
    }

    public static void AddSink(SinkConfiguration sinkConfiguration)
    {
        lock (_sinks)
        {
            _sinks.Add(sinkConfiguration);
        }
    }

    public static void StopAndFlush()
    {
        // 1. Tell the queue no more items are coming
        _queue.CompleteAdding();

        // 2. Wait for background thread to drain every remaining log message
        if (_logThread.IsAlive)
            _logThread.Join();

        // 3. Synchronously flush each sink
        lock (_sinks)
        {
            foreach (var sink in _sinks)
            {
                try
                {
                    sink.Sink.Flush();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Logger Flush Error] {ex}");
                }
            }
        }
    }

    public static void Debug(string provider, string message) => Log(provider, message, LogLevel.Debug);
    public static void Info(string provider, string message)  => Log(provider, message, LogLevel.Info);
    public static void Warn(string provider, string message)  => Log(provider, message, LogLevel.Warn);
    public static void Error(string provider, string message) => Log(provider, message, LogLevel.Error);
    public static void Fatal(string provider, string message) => Log(provider, message, LogLevel.Fatal);

    private static void Log(string provider, string message, LogLevel logLevel)
    {
        if (!_queue.IsAddingCompleted)
        {
            try
            {
                _queue.TryAdd(new LogMessage(DateTime.Now, logLevel, provider, message));
            }
            catch (InvalidOperationException) { }
        }
    }

    private static void ProcessQueue()
    {
        try
        {
            // Blocks while messages arrive; exits loop only after CompleteAdding() is called AND queue is empty
            foreach (var message in _queue.GetConsumingEnumerable())
            {
                WriteToSinks(message);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Fatal Logger Error] {ex}");
        }
    }

    private static void WriteToSinks(LogMessage message)
    {
        lock (_sinks)
        {
            for (int i = 0; i < _sinks.Count; i++)
            {
                var sinkConfig = _sinks[i];
                if (sinkConfig.Levels.Contains(message.Level))
                {
                    try
                    {
                        sinkConfig.Sink.Write(message);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[{DateTime.Now}] [Error] [{nameof(Logging)}] {ex}");
                    }
                }
            }
        }
    }
}
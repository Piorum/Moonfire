using Moonfire.Logging.Interfaces;

namespace Moonfire.Logging.Models;

public readonly record struct SinkConfiguration(
    ILogSink Sink,
    HashSet<LogLevel> Levels
);

namespace Moonfire.Logging.Models;

public readonly record struct LogMessage(
    DateTime CreationTime,
    LogLevel Level,
    string Provider,
    string Message
);

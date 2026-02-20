using Moonfire.Logging.Models;

namespace Moonfire.Logging.Interfaces;

public interface ILogSink
{
    Task WriteAsync(LogMessage message);
    Task Flush();
}

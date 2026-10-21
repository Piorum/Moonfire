using Moonfire.Logging.Models;

namespace Moonfire.Logging.Interfaces;

public interface ILogSink
{
    void Write(LogMessage message);
    void Flush();
}
using System.Threading.Channels;
using Moonfire.Input.Models;

namespace Moonfire.Input;

public class RawInputChannel() : IDisposable
{
    private volatile bool _disposed = false;

    private Channel<TerminalInput>? channel = Channel.CreateUnbounded<TerminalInput>(new UnboundedChannelOptions()
    {
        SingleReader = true,
        SingleWriter = true
    });
    public ChannelReader<TerminalInput> Reader => _disposed 
        ? throw new ObjectDisposedException(nameof(RawInputChannel)) 
        : channel!.Reader;

    internal ChannelWriter<TerminalInput> Writer => _disposed 
        ? throw new ObjectDisposedException(nameof(RawInputChannel)) 
        : channel!.Writer;

    public void Dispose()
    {
        if(Interlocked.Exchange(ref _disposed, true) == false)
        {
            channel!.Writer.TryComplete();
            channel = null;
        }

        GC.SuppressFinalize(this);
    }
}

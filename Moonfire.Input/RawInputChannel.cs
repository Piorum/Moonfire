using System.Collections.Concurrent;
using Moonfire.Input.Models;

namespace Moonfire.Input;

public class RawInputChannel(Action? onDispose = null) : IDisposable
{
    private readonly BlockingCollection<TerminalInput> queue = [];
    private bool _disposed = false;

    public bool IsDisposed => Volatile.Read(ref _disposed) != false;
    
    public TerminalInput Take(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return queue.Take(token);
    }
    internal bool Write(TerminalInput input)
    {
        if(IsDisposed)
            return false;

        try
        {
            return queue.TryAdd(input);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if(Interlocked.Exchange(ref _disposed, true) == false)
        {
            try
            {
                queue.CompleteAdding();
            }
            catch (ObjectDisposedException) { }
            
            queue.Dispose();

            onDispose?.Invoke();
        }

        GC.SuppressFinalize(this);
    }
}

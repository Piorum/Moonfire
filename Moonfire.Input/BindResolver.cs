using System.Threading.Channels;
using Moonfire.Input.Models;

namespace Moonfire.Input;

public class BindResolver(int? sequenceTimeoutMs = null)
{
    internal readonly Dictionary<InputKey, Bind> indifferentBinds = [];
    internal readonly SequenceState sequenceState = new(sequenceTimeoutMs ?? 1000);

    private readonly Channel<(Bind bind, InputData data)> bindChannel = Channel.CreateUnbounded<(Bind bind, InputData data)>(new() { SingleReader = true, SingleWriter = true });
    public ChannelReader<(Bind bind, InputData data)> BindReader => bindChannel.Reader;

    public async Task Process(TerminalInput evt, CancellationToken token)
    {
        var sequenceBind = sequenceState.Process(evt);
        if(sequenceBind is not null)
        {
            await bindChannel.Writer.WriteAsync((sequenceBind.Value, evt.InputData), token);
            return;
        }

        if(indifferentBinds.TryGetValue(evt.Key, out var indifferentBind))
            await bindChannel.Writer.WriteAsync((indifferentBind, evt.InputData), token);
    }
}

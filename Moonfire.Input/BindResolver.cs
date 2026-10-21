using System.Collections.Concurrent;
using Moonfire.Input.Models;

namespace Moonfire.Input;

public class BindResolver(int? sequenceTimeoutMs = null)
{
    internal readonly Dictionary<InputKey, Bind> indifferentBinds = [];
    internal readonly SequenceState sequenceState = new(sequenceTimeoutMs ?? 1000);

    private readonly BlockingCollection<(Bind bind, InputData data)> bindQueue = [];
    internal (Bind bind, InputData data) Take(CancellationToken token) => 
        bindQueue.Take(token);

    internal void Process(TerminalInput evt)
    {
        var sequenceBind = sequenceState.Process(evt);
        if(sequenceBind is not null)
        {
            bindQueue.Add((sequenceBind.Value, evt.InputData));
            return;
        }

        if(indifferentBinds.TryGetValue(evt.Key, out var indifferentBind))
            bindQueue.Add((indifferentBind, evt.InputData));
    }
}

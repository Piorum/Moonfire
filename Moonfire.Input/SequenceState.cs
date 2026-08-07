using Moonfire.Shared.DataStructures;
using Moonfire.Input.Models;

namespace Moonfire.Input;

internal class SequenceState
{
    internal readonly TrieNode<InputKey, Bind?> rootNode = new();
    private TrieNode<InputKey, Bind?> currentNode;

    //private readonly List<InputKey> currentSequence = [];

    private readonly System.Timers.Timer sequenceTimeoutTimer;

    internal SequenceState(int sequenceTimeoutMs = 1000)
    {
        currentNode = rootNode;

        sequenceTimeoutTimer = new(sequenceTimeoutMs);
        sequenceTimeoutTimer.Elapsed += (source, e) => ResetSequence();
    }

    internal Bind? Process(TerminalInput evt)
    {
        ResetTimeout();

        currentNode.Children.TryGetValue(evt.Key, out var node);

        if(node is null)
        {
            ResetSequence();
            return null;
        }

        //Update node path
        currentNode = node;

        //No binds here
        if(!node.IsTerminal)
            return null;

        ResetSequence();
        return node.Result;
    }

    private void ResetTimeout()
    {
        sequenceTimeoutTimer.Stop();
        sequenceTimeoutTimer.Start();
    }

    private void ResetSequence()
    {
        currentNode = rootNode;
        //currentSequence.Clear();
    }
}

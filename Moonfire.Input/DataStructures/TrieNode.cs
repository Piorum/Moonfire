using Moonfire.Input.Models;

namespace Moonfire.Input.DataStructures;

public class TrieNode
{
    public Dictionary<InputKey, TrieNode> Children { get; } = [];
    public Bind? Binding { get; }

    public bool IsTerminal => Binding is not null;
}

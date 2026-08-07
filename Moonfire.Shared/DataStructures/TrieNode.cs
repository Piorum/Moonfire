namespace Moonfire.Shared.DataStructures;

public class TrieNode<TKey, TResult> where TKey : notnull
{
    public Dictionary<TKey, TrieNode<TKey, TResult>> Children { get; } = [];
    public TResult? Result { set; get; }

    public bool IsTerminal => Result is not null;
}

using Moonfire.Input.Models;

namespace Moonfire.Input;

public class KeybindBuilder(InputHandler inputHandler)
{
    private readonly List<InputKey> keys = [];
    private Bind? _bind;

    private static void NoKeysError() => 
        throw new InvalidOperationException("Atleast one InputKey must be added.");
    private static void NoBindError() => 
        throw new InvalidOperationException( "Must provide a Bind.");

    public KeybindBuilder WithKey(InputKey key)
    {
        keys.Add(key);
        return this;
    }

    public KeybindBuilder WithBind(Bind bind)
    {
        _bind = bind;
        return this;
    }

    public void Register()
    {
        if(keys.Count == 0)
            NoKeysError();
        else if(_bind is null)
            NoBindError();

        if(keys.Count == 1)
            RegisterIndifferent();
        else
            RegisterSequence();
    }

    private void RegisterIndifferent()
    {
        inputHandler.indifferentBinds.Add(keys.First(), _bind!.Value);
    }

    private void RegisterSequence()
    {
        var sequenceState = inputHandler.sequenceState;
        var currentNode = sequenceState.rootNode;

        foreach(var key in keys)
        {
            if(currentNode.Children.TryGetValue(key, out var value))
                currentNode = value;
            else
            {
                value = new();
                currentNode.Children.Add(key, value);

                currentNode = value;
            }
        }

        currentNode.Binding = _bind;
    }
}

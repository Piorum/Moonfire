namespace Moonfire.Input.Models;

public readonly record struct Bind
{
    public Guid Id { get; }
    public Action<InputData> Action { get; }

    public Bind(Action<InputData> action)
    {
        Id = Guid.NewGuid();
        Action = action;
    }
}

namespace Moonfire;

public class Command(string _name, string _description, Rank _rank, List<CommandOption>? _options = null)
{
    public readonly string Name = _name;
    public readonly string Description = _description;
    public readonly Rank Rank = _rank;
    public readonly List<CommandOption> Options = _options ?? [];
}

public enum Rank{
        User,
        Admin,
        Owner
}

namespace Moonfire.Types;

public class Command(string _name, string _description, Rank _rank, List<CommandOption>? _options = null)
{
    public readonly string Name = _name;
    public readonly string Description = _description;
    public readonly Rank Rank = _rank;
    public readonly List<CommandOption> Options = _options ?? [];
}

public class CommandOption(string name = "", ApplicationCommandOptionType type = ApplicationCommandOptionType.String, string description = "", bool isRequired = false, List<string>? choices = null)
{
    public string Name{ get; set; } = name;
    public ApplicationCommandOptionType Type { get; set; } = type;
    public string Description { get; set; } = description;
    public bool IsRequired { get; set; } = isRequired;
    public List<string> Choices { get; set; } = choices ?? [];

}

public enum Rank{
        User,
        Admin,
        Owner
}

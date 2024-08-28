namespace SCDisc;

public class CommandOption(string name = "", ApplicationCommandOptionType type = ApplicationCommandOptionType.String, string description = "", bool isRequired = false)
{
    public string Name{ get; set; } = name;
    public ApplicationCommandOptionType Type { get; set; } = type;
    public string Description { get; set; } = description;
    public bool IsRequired { get; set; } = isRequired;

}

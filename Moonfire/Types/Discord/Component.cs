namespace Moonfire.Types.Discord;

public class MoonfireComponent(List<MoonfireSelectMenuComponent>? selectMenus = default, List<MoonfireButtonComponent>? buttons = default)
{
    public readonly List<MoonfireSelectMenuComponent> SelectMenus = selectMenus ?? [];
    public readonly List<MoonfireButtonComponent> Buttons = buttons ?? [];
}

public class MoonfireSelectMenuComponent(
    string customId,
    List<MoonfireSelectMenuComponentOption>? options = default,
    string? placeholder = null,
    bool disabled = false,
    ComponentType type = ComponentType.SelectMenu,
    List<ChannelType>? channelTypes = null
){
    public readonly string CustomId = customId;
    public readonly List<MoonfireSelectMenuComponentOption> Options = options is null || options is [] ? [new("emptyoption","emptyoption")] : options;
    public readonly string? Placeholder = placeholder;
    public readonly bool Disabled = disabled;
    public readonly ComponentType Type = type;
    public readonly List<ChannelType> ChannelTypes = channelTypes ?? [];
}

public class MoonfireSelectMenuComponentOption(
    string label,
    string value,
    string? description = null,
    IEmote? emote = null,
    bool isDefault = false
){
    public readonly string Label = label;
    public readonly string Value = value;
    public readonly string? Description = description;
    public readonly IEmote? Emote = emote;
    public readonly bool IsDefault = isDefault;
}

public class MoonfireButtonComponent(
    string label, 
    string customId, 
    ButtonStyle? style = null, 
    IEmote? emote = null,
    string? url = null,
    bool disabled = false
){
    public readonly string Label = label;
    public readonly string CustomId = customId;
    public readonly ButtonStyle Style = style ?? (url is null ? ButtonStyle.Primary : ButtonStyle.Link); //defaults to primary unless link is provided
    public readonly IEmote? Emote = emote;
    public readonly string? Url = url;
    public readonly bool Disabled = disabled;
}

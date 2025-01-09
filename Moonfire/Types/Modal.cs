namespace Moonfire.Types;

public class MoonfireModal(
    string title,
    string customId,
    List<MoonfireModalTextInput>? textInputs = null
){
    public readonly string Title = title;
    public readonly string CustomId = customId;
    public readonly List<MoonfireModalTextInput> TextInputs = textInputs is null || textInputs is [] ? [new("empty","empty")] : textInputs;
}

public class MoonfireModalTextInput(
    string label,
    string customId,
    TextInputStyle style = TextInputStyle.Short,
    string? placeholder = null,
    int? minLength = null,
    int? maxLength = null,
    bool required = false
){
    public readonly string Label = label;
    public readonly string CustomId = customId;
    public readonly TextInputStyle Style = style;
    public readonly string? Placeholder = placeholder;
    public readonly int? MinLength = minLength;
    public readonly int? MaxLength = maxLength;
    public readonly bool Required = required;

}

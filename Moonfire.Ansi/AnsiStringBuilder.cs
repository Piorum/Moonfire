using System.Text;
using Moonfire.Ansi.Models;
using Moonfire.Ansi.Registries;

namespace Moonfire.Ansi;

public class AnsiStringBuilder()
{
    private readonly StringBuilder sb = new();
    private AnsiStyleData currentState = new();

    private readonly static (AnsiProperty, string, string)[] modifierActionsLookup =
    [
        (AnsiProperty.Bold, AnsiRegistry.Bold, AnsiRegistry.DisableBold),
        (AnsiProperty.Italic, AnsiRegistry.Italic, AnsiRegistry.DisableItalic),
        (AnsiProperty.Underline, AnsiRegistry.Underline, AnsiRegistry.DisableUnderline),
        (AnsiProperty.Highlight, AnsiRegistry.ReverseVideoMode, AnsiRegistry.DisableReverseVideoMode),
        (AnsiProperty.Strikethrough, AnsiRegistry.Strikethrough, AnsiRegistry.DisableStrikethrough)
    ];
    
    public AnsiStringBuilder Append(string text, AnsiStyleData desiredState, (int X, int Y)? desiredCursorPos) =>
        Append(text.AsSpan(), desiredState, desiredCursorPos);

    public AnsiStringBuilder Append(ReadOnlySpan<char> text, AnsiStyleData desiredState, (int X, int Y)? desiredCursorPos)
    {
        UpdateStyle(desiredState, desiredCursorPos);
        if (text.Length > 0)
            sb.Append(text);

        return this;
    }

    private void UpdateStyle(AnsiStyleData desiredState, (int X, int Y)? desiredCursorPos)
    {
        if (desiredCursorPos is { } pos)
            sb.Append(AnsiRegistry.MoveCursor(pos.Y, pos.X));

        if (currentState.ForegroundColor != desiredState.ForegroundColor)
            sb.Append(AnsiRegistry.SetForegroundColor(desiredState.ForegroundColor));
        if (currentState.BackgroundColor != desiredState.BackgroundColor)
            sb.Append(AnsiRegistry.SetBackgroundColor(desiredState.BackgroundColor));

        var removedProperties = currentState.Properties & ~desiredState.Properties;
        var addedProperties = desiredState.Properties & ~currentState.Properties;

        foreach (var (modifier, onCode, offCode) in modifierActionsLookup)
            if (addedProperties.HasFlag(modifier))
                sb.Append(onCode);
            else if (removedProperties.HasFlag(modifier))
                sb.Append(offCode);

        currentState = desiredState;
    }

    public AnsiStringBuilder ShowCursor()
    {
        sb.Append(AnsiRegistry.ShowCursor);
        return this;
    }
    public AnsiStringBuilder HideCursor()
    {
        sb.Append(AnsiRegistry.HideCursor);
        return this;
    }

    public AnsiStringBuilder ResetProperties()
    {
        sb.Append(AnsiRegistry.ResetProperties);
        return this;
    }
    public AnsiStringBuilder ResetPropertiesNewLine()
    {
        sb.Append('\n');
        sb.Append(AnsiRegistry.ResetProperties);
        return this;
    }

    public override string ToString() => sb.ToString();

    public void Clear()
    {
        sb.Clear();
        currentState = new();
    }
}

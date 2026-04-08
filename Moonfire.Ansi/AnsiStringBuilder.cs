using System.Text;
using Moonfire.Ansi.Models;
using Moonfire.Ansi.Registries;

namespace Moonfire.Ansi;

public class AnsiStringBuilder(int bufferSize = 2<<15)
{
    private byte[] buffer = new byte[bufferSize];
    private int position = 0;
    private AnsiStyleData currentState = new();

    public bool IsEmpty => position == 0;
    
    public AnsiStringBuilder Append(string text, AnsiStyleData desiredState, (int X, int Y)? desiredCursorPos) =>
        Append(Encoding.UTF8.GetBytes(text) , desiredState, desiredCursorPos);

    public AnsiStringBuilder Append(ReadOnlySpan<byte> text, AnsiStyleData desiredState, (int X, int Y)? desiredCursorPos)
    {
        UpdateStyle(desiredState, desiredCursorPos);

        if (text.Length > 0)
        {
            EnsureCapacity(text.Length);
            text.CopyTo(buffer.AsSpan(position));
            position += text.Length;
        }

        return this;
    }

    public AnsiStringBuilder ShowCursor()
    {
        AppendRaw(AnsiRegistry.ShowCursorBytes);
        return this;
    }
    public AnsiStringBuilder HideCursor()
    {
        AppendRaw(AnsiRegistry.HideCursorBytes);
        return this;
    }
    public AnsiStringBuilder EnterAlternateScreen()
    {
        AppendRaw(AnsiRegistry.EnterAlternateScreenBytes);
        return this;
    }
    public AnsiStringBuilder ExitAlternateScreen()
    {
        AppendRaw(AnsiRegistry.ExitAlternateScreenBytes);
        return this;
    }
    public AnsiStringBuilder ClearScreen()
    {
        AppendRaw(AnsiRegistry.ClearScreenBytes);
        return this;
    }
    public AnsiStringBuilder ResetProperties()
    {
        AppendRaw(AnsiRegistry.ResetPropertiesBytes);
        return this;
    }
    public AnsiStringBuilder ResetPropertiesNewLine()
    {
        AppendRaw((byte)'\n');
        AppendRaw(AnsiRegistry.ResetPropertiesBytes);
        return this;
    }

    public ReadOnlyMemory<byte> ToBuffer() => 
        buffer.AsMemory(0, position);

    public override string ToString() => 
        Encoding.UTF8.GetString(buffer.AsSpan(0, position));

    public void Clear() =>
        (position, currentState) = (0, new());


    private void UpdateStyle(AnsiStyleData desiredState, (int X, int Y)? desiredCursorPos)
    {
        if (desiredCursorPos is { } pos)
        {
            EnsureCapacity(AnsiRegistry.MaxMoveCursorBytes);
            position += AnsiRegistry.MoveCursor(buffer.AsSpan(position), pos.Y, pos.X);
        }
        if (currentState.ForegroundColor != desiredState.ForegroundColor)
        {
            EnsureCapacity(AnsiRegistry.MaxSetColorBytes);
            position += AnsiRegistry.SetForegroundColor(buffer.AsSpan(position), desiredState.ForegroundColor);
        }
        if (currentState.BackgroundColor != desiredState.BackgroundColor)
        {
            EnsureCapacity(AnsiRegistry.MaxSetColorBytes);
            position += AnsiRegistry.SetBackgroundColor(buffer.AsSpan(position), desiredState.BackgroundColor);
        }

        var removedProperties = currentState.Properties & ~desiredState.Properties;
        var addedProperties = desiredState.Properties & ~currentState.Properties;

        if(addedProperties.HasFlag(AnsiProperty.Bold))
            AppendRaw(AnsiRegistry.BoldBytes);
        else if (removedProperties.HasFlag(AnsiProperty.Bold))
            AppendRaw(AnsiRegistry.DisableBoldBytes);

        if(addedProperties.HasFlag(AnsiProperty.Italic))
            AppendRaw(AnsiRegistry.ItalicBytes);
        else if (removedProperties.HasFlag(AnsiProperty.Italic))
            AppendRaw(AnsiRegistry.DisableItalicBytes);

        if(addedProperties.HasFlag(AnsiProperty.Underline))
            AppendRaw(AnsiRegistry.UnderlineBytes);
        else if (removedProperties.HasFlag(AnsiProperty.Underline))
            AppendRaw(AnsiRegistry.DisableUnderlineBytes);

        if(addedProperties.HasFlag(AnsiProperty.Highlight))
            AppendRaw(AnsiRegistry.ReverseVideoModeBytes);
        else if (removedProperties.HasFlag(AnsiProperty.Highlight))
            AppendRaw(AnsiRegistry.DisableReverseVideoModeBytes);

        if(addedProperties.HasFlag(AnsiProperty.Strikethrough))
            AppendRaw(AnsiRegistry.StrikethroughBytes);
        else if (removedProperties.HasFlag(AnsiProperty.Strikethrough))
            AppendRaw(AnsiRegistry.DisableStrikethroughBytes);

        currentState = desiredState;
    }
    private void EnsureCapacity(int needed)
    {
        if (position + needed > buffer.Length)
            Array.Resize(ref buffer, Math.Max(buffer.Length * 2, position + needed));
    }
    private void AppendRaw(byte utf8Data)
    {
        EnsureCapacity(1);
        buffer[position++] = utf8Data;
    }
    private void AppendRaw(ReadOnlySpan<byte> utf8Data)
    {
        EnsureCapacity(utf8Data.Length);
        utf8Data.CopyTo(buffer.AsSpan(position));
        position += utf8Data.Length;
    }
}

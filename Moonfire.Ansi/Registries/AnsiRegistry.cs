using System.Buffers.Text;
using Moonfire.Ansi.Models;

namespace Moonfire.Ansi.Registries;

public static class AnsiRegistry
{
    //Commands added through builder calls
    //Reset
    public const string ResetProperties = "\x1B[0m";
    public static ReadOnlySpan<byte> ResetPropertiesBytes => "\x1B[0m"u8;

    //Cursor
    public const string HideCursor = "\x1B[?25l";
    public static ReadOnlySpan<byte> HideCursorBytes => "\x1B[?25l"u8;
    public const string ShowCursor = "\x1B[?25h";
    public static ReadOnlySpan<byte> ShowCursorBytes => "\x1B[?25h"u8;

    //Screen
    public const string EnterAlternateScreen = "\x1b[?1049h";
    public static ReadOnlySpan<byte> EnterAlternateScreenBytes => "\x1b[?1049h"u8;
    public const string ExitAlternateScreen = "\x1b[?1049l";
    public static ReadOnlySpan<byte> ExitAlternateScreenBytes => "\x1b[?1049l"u8;
    public const string ClearScreen = "\x1b[2J";
    public static ReadOnlySpan<byte> ClearScreenBytes => "\x1b[2J"u8;

    //Commands added through style updates
    //Color
    public const string ResetForegroundColor = "\x1b[39m";
    public static ReadOnlySpan<byte> ResetForegroundColorBytes => "\x1b[39m"u8;
    public const string ResetBackgroundColor = "\x1b[49m";
    public static ReadOnlySpan<byte> ResetBackgroundColorBytes => "\x1b[49m"u8;

    //Bold
    public const string Bold = "\x1b[1m";
    public static ReadOnlySpan<byte> BoldBytes => "\x1b[1m"u8;
    public const string DisableBold = "\x1b[22m";
    public static ReadOnlySpan<byte> DisableBoldBytes => "\x1b[22m"u8;

    //Italic
    public const string Italic = "\x1b[3m";
    public static ReadOnlySpan<byte> ItalicBytes => "\x1b[3m"u8;
    public const string DisableItalic = "\x1b[23m";
    public static ReadOnlySpan<byte> DisableItalicBytes => "\x1b[23m"u8;

    //Underline
    public const string Underline = "\x1b[4m";
    public static ReadOnlySpan<byte> UnderlineBytes => "\x1b[4m"u8;
    public const string DisableUnderline = "\x1b[24m";
    public static ReadOnlySpan<byte> DisableUnderlineBytes => "\x1b[24m"u8;

    //Highlight
    public const string ReverseVideoMode = "\x1b[7m";
    public static ReadOnlySpan<byte> ReverseVideoModeBytes => "\x1b[7m"u8;
    public const string DisableReverseVideoMode = "\x1b[27m";
    public static ReadOnlySpan<byte> DisableReverseVideoModeBytes => "\x1b[27m"u8;

    //Strikethrough
    public const string Strikethrough = "\x1b[9m";
    public static ReadOnlySpan<byte> StrikethroughBytes => "\x1b[9m"u8;
    public const string DisableStrikethrough = "\x1b[29m";
    public static ReadOnlySpan<byte> DisableStrikethroughBytes => "\x1b[29m"u8;

    private static ReadOnlySpan<byte> AnsiStartBytes => "\x1B["u8;
    private const byte AnsiSeparatorByte = (byte)';';


    private const byte ColorEndByte = (byte)'m';
    private static ReadOnlySpan<byte> ForegroundColorStartBytes => "\x1B[38;2;"u8;
    public const int MaxSetColorBytes = 19;
    public static int SetForegroundColor(Span<byte> destination, AnsiTruecolor? color)
    {
        if(!color.HasValue)
        {
            ResetForegroundColorBytes.CopyTo(destination);
            return ResetForegroundColorBytes.Length;
        }

        int offset = 0;

        ForegroundColorStartBytes.CopyTo(destination);
        offset += ForegroundColorStartBytes.Length;

        Utf8Formatter.TryFormat(color.Value.R, destination[offset..], out int bytesWritten);
        offset += bytesWritten;
        destination[offset++] = AnsiSeparatorByte;

        Utf8Formatter.TryFormat(color.Value.G, destination[offset..], out bytesWritten);
        offset += bytesWritten;
        destination[offset++] = AnsiSeparatorByte;

        Utf8Formatter.TryFormat(color.Value.B, destination[offset..], out bytesWritten);
        offset += bytesWritten;

        destination[offset++] = ColorEndByte;

        return offset;
    }

    private static ReadOnlySpan<byte> BackgroundColorStartBytes => "\x1B[48;2;"u8;
    public static int SetBackgroundColor(Span<byte> destination, AnsiTruecolor? color)
    {
        if(!color.HasValue)
        {
            ResetBackgroundColorBytes.CopyTo(destination);
            return ResetBackgroundColorBytes.Length;
        }

        int offset = 0;

        BackgroundColorStartBytes.CopyTo(destination);
        offset += BackgroundColorStartBytes.Length;

        Utf8Formatter.TryFormat(color.Value.R, destination[offset..], out int bytesWritten);
        offset += bytesWritten;
        destination[offset++] = AnsiSeparatorByte;

        Utf8Formatter.TryFormat(color.Value.G, destination[offset..], out bytesWritten);
        offset += bytesWritten;
        destination[offset++] = AnsiSeparatorByte;

        Utf8Formatter.TryFormat(color.Value.B, destination[offset..], out bytesWritten);
        offset += bytesWritten;

        destination[offset++] = ColorEndByte;

        return offset;
    }

    private const byte AnsiMoveCursorEndByte = (byte)'H';
    public const int MaxMoveCursorBytes = 11;
    public static int MoveCursor(Span<byte> destination, int line, int column)
    {
        int offset = 0;

        AnsiStartBytes.CopyTo(destination);
        offset += AnsiStartBytes.Length;

        Utf8Formatter.TryFormat(line + 1, destination[offset..], out int bytesWritten);
        offset += bytesWritten;

        destination[offset++] = AnsiSeparatorByte;

        Utf8Formatter.TryFormat(column + 1, destination[offset..], out bytesWritten);
        offset += bytesWritten;

        destination[offset++] = AnsiMoveCursorEndByte;

        return offset;
    }

}

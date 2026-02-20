using Moonfire.Ansi.Models;

namespace Moonfire.Ansi.Registries;

public static class AnsiRegistry
{
    public const string ResetProperties = "\x1B[0m";

    public static string SetForegroundColor(AnsiTruecolor? color) =>
        color.HasValue ?
            $"\x1B[38;2;{color.Value.R};{color.Value.G};{color.Value.B}m" :
            ResetForegroundColor;
    public static string SetBackgroundColor(AnsiTruecolor? color) =>
        color.HasValue ?
            $"\x1B[48;2;{color.Value.R};{color.Value.G};{color.Value.B}m" :
            ResetBackgroundColor;
    public const string ResetForegroundColor = "\x1b[39m";
    public const string ResetBackgroundColor = "\x1b[49m";

    //Bold
    public const string Bold = "\x1b[1m";
    public const string DisableBold = "\x1b[22m";

    //Italic
    public const string Italic = "\x1b[3m";
    public const string DisableItalic = "\x1b[23m";

    //Underline
    public const string Underline = "\x1b[4m";
    public const string DisableUnderline = "\x1b[24m";

    //Highlight
    public const string ReverseVideoMode = "\x1b[7m";
    public const string DisableReverseVideoMode = "\x1b[27m";

    //Strikethrough
    public const string Strikethrough = "\x1b[9m";
    public const string DisableStrikethrough = "\x1b[29m";

    //CursorVisible
    public const string HideCursor = "\x1B[?25l";
    public const string ShowCursor = "\x1B[?25h";

    public static string MoveCursor(int line, int column) =>
        $"\x1B[{line + 1};{column + 1}H";

    public const string EnterAlternateScreen = "\x1b[?1049h";
    public const string ExitAlternateScreen = "\x1b[?1049l";
    public const string ClearScreen = "\x1b[2J";

}

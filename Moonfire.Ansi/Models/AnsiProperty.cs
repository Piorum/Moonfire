namespace Moonfire.Ansi.Models;

[Flags]
public enum AnsiProperty : byte
{
    None = 0,
    Bold = 1 << 0,
    Italic = 1 << 1,
    Underline = 1 << 2,
    Highlight = 1 << 3,
    Strikethrough = 1 << 4
}

namespace Moonfire.Ansi.Models;

public record AnsiStyleData(
    AnsiTruecolor? ForegroundColor = null,
    AnsiTruecolor? BackgroundColor = null,
    AnsiProperty Properties = AnsiProperty.None);

using System.Runtime.InteropServices;
using Moonfire.Ansi;
using Moonfire.Ansi.Models;
using Moonfire.Glyph;

namespace Moonfire.Rendering.Models;

[StructLayout(LayoutKind.Sequential, Size = 8)]
public readonly struct TerminalCell(ulong value)
{
    public readonly ulong Value = value;
    
    private const int GlyphIdBits = 24;
    private const int WidthBits = 8;

    private const ulong glyphIdMask = (1UL << GlyphIdBits) - 1;
    private const ulong widthMask = (1UL << WidthBits) - 1;

    public readonly int GlyphId => (int)(Value & glyphIdMask);
    public readonly byte Width => (byte)((Value >> GlyphIdBits) & widthMask);
    public readonly int StyleId => (int)(Value >> (GlyphIdBits + WidthBits));

    public TerminalCell(int glyphId, byte width, int styleId) : this(((ulong)(uint)styleId << 32) | ((ulong)width << 24) | (uint)glyphId) { }

    public bool Equals(TerminalCell other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is TerminalCell other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(TerminalCell left, TerminalCell right) => left.Value == right.Value;
    public static bool operator !=(TerminalCell left, TerminalCell right) => left.Value != right.Value;

    public static readonly TerminalCell Blank;

    static TerminalCell()
    {
        var (id, width) = GlyphFactory.GetGlyphIds(" ").First();
        var styleId = AnsiStyleFactory.GetStyleId((null, null, AnsiProperty.None));

        Blank = new(id, width, styleId);
    }
}

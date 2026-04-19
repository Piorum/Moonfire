using System.Runtime.InteropServices;
using Moonfire.Ansi;
using Moonfire.Ansi.Models;
using Moonfire.Glyph;

namespace Moonfire.Rendering.Models;

[StructLayout(LayoutKind.Explicit)]
public readonly struct TerminalCell(ulong value)
{
    [FieldOffset(0)]
    public readonly ulong Value = value;

    [FieldOffset(0)]
    private readonly uint _lowerBits;

    [FieldOffset(4)]
    public readonly int StyleId;

    public readonly int GlyphId => (int)(_lowerBits & 0x7FFFFFFFUL);
    public readonly byte Width => (byte)((_lowerBits >> 31) + 1);

    public TerminalCell(int glyphId, byte width, int styleId)
        //Store style id in lower 32 bits
        : this(((ulong)(uint)styleId << 32)
            //Take 2nd bit from width, we add one on lookup, 0 -> 1, 1 -> 1, 2 -> 2 3 -> 2, others effectively clamped to 1 or 2 safely.
            | ((ulong)(width & 2) << 31)
            //Store glyphId in top-1 31 bits
            | ((uint)glyphId & 0x7FFFFFFFUL)) { }

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

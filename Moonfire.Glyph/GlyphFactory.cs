using System.Globalization;
using Moonfire.Glyph.Models;

namespace Moonfire.Glyph;

public static class GlyphFactory
{
    private readonly static GlyphCache glyphCache = new();

    private readonly static (int id, byte width) invalidGlyph = GetGlyphIds("\uFFFD").First();

    public static List<(int id, byte width)> GetGlyphIds(string text)
    {
        List<(int id, byte width)> glyphs = [];

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while(enumerator.MoveNext())
        {
            var cluster = enumerator.Current.ToString();

            if(string.IsNullOrEmpty(cluster))
                continue;

            var info = glyphCache.GetOrAdd(cluster, null);
            
            if(info.width != 0)
                glyphs.Add(info);
            else
                glyphs.Add(invalidGlyph);
        }

        return glyphs;
    }

    public static GlyphData Get(int id) =>
        glyphCache.Get(id);
}

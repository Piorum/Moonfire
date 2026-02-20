using Wcwidth;
using Moonfire.Glyph.Models;
using Moonfire.Shared;

namespace Moonfire.Glyph;

public class GlyphCache : IdIndexedCache<string , byte?, GlyphData, (int id, byte width)>
{
    protected override GlyphData CreateObject(string cluster, byte? overrideWidth){
        var realWidth = MeasureGramphemeCluster(cluster);
        GlyphData newData = new(cluster, realWidth, overrideWidth is null ? realWidth : overrideWidth.Value);

        return newData;
    }

    protected override (int id, byte width) CreateInfo(int id, GlyphData dataOjbect)
    {
        return (id, dataOjbect.VisualWidth);
    }

    protected override GlyphData Update(GlyphData dataObject, string cluster, byte? overrideWidth)
    {
        if(overrideWidth is not null)
            return dataObject with { VisualWidth = overrideWidth.Value };

        return dataObject;
    }

    private static byte MeasureGramphemeCluster(string graphemeCluster) =>
        (byte)graphemeCluster.EnumerateRunes().Sum(r => UnicodeCalculator.GetWidth(r));
}

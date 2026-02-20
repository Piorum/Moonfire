using Moonfire.Ansi.Models;
using Moonfire.Shared;

namespace Moonfire.Ansi;

public class AnsiStyleCache : IdIndexedCache<(AnsiTruecolor? fgColor, AnsiTruecolor? bgColor, AnsiProperty properties), AnsiStyleData, int>
{
    protected override int CreateInfo(int id, AnsiStyleData dataOjbect) =>
        id;

    protected override AnsiStyleData CreateObject((AnsiTruecolor? fgColor, AnsiTruecolor? bgColor, AnsiProperty properties) creationData) =>
        new() { ForegroundColor = creationData.fgColor, BackgroundColor = creationData.bgColor, Properties = creationData.properties };

    protected override AnsiStyleData Update(AnsiStyleData dataObject, (AnsiTruecolor? fgColor, AnsiTruecolor? bgColor, AnsiProperty properties) creationData) =>
        dataObject with { ForegroundColor = creationData.fgColor, BackgroundColor = creationData.bgColor, Properties = creationData.properties };
}

using Moonfire.Ansi.Models;

namespace Moonfire.Ansi;

public static class AnsiStyleFactory
{
    private static readonly AnsiStyleCache styleCache = new();

    public static int GetStyleId((AnsiTruecolor? fgColor, AnsiTruecolor? bgColor, AnsiProperty properties) creationData) =>
        styleCache.GetOrAdd(creationData);

    public static AnsiStyleData Get(int id) =>
        styleCache.Get(id);
}

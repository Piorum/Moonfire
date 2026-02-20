using Moonfire.Rendering.Models;

namespace Moonfire.Rendering;

internal class DoubleBuffer
{
    internal TerminalBuffer FrontBuffer;
    internal TerminalBuffer BackBuffer;

    private DoubleBuffer(TerminalBuffer frontBuffer, TerminalBuffer backBuffer)
    {
        FrontBuffer = frontBuffer;
        BackBuffer = backBuffer;
    }

    internal static DoubleBuffer New(int startX, int startY) =>
        new(new(startX,startY), new(startX,startY));

    internal async Task Resize(int x, int y)
    {
        FrontBuffer = new(x, y);
        BackBuffer = new(x, y);
    }

}

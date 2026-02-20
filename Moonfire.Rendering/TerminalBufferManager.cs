using Moonfire.Rendering.Models;

namespace Moonfire.Rendering;

internal class TerminalBufferManager
{
    internal TerminalBuffer FrontBuffer;
    internal TerminalBuffer BackBuffer;

    private TerminalBufferManager(TerminalBuffer frontBuffer, TerminalBuffer backBuffer)
    {
        FrontBuffer = frontBuffer;
        BackBuffer = backBuffer;
    }

    internal static TerminalBufferManager New(int startX, int startY) =>
        new(new(startX,startY), new(startX,startY));

    internal async Task Resize(int x, int y)
    {
        FrontBuffer = new(x, y);
        BackBuffer = new(x, y);
    }

}

using Moonfire.Rendering.Models;

namespace Moonfire.Rendering;

internal class DoubleBuffer
{
    internal TerminalBuffer FrontBuffer { get; private set; }
    internal TerminalBuffer BackBuffer { get; private set; }

    private DoubleBuffer(TerminalBuffer frontBuffer, TerminalBuffer backBuffer) =>
        (FrontBuffer, BackBuffer) = (frontBuffer, backBuffer);

    internal static DoubleBuffer New(int startX, int startY) =>
        new(new(startX,startY), new(startX,startY));

    internal async Task Resize(int x, int y) =>
        (FrontBuffer, BackBuffer) = (new(x, y), new(x,y));

    internal void Swap() => 
        (BackBuffer, FrontBuffer) = (FrontBuffer, BackBuffer);

}

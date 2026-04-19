using Moonfire.Rendering.Models;

namespace Moonfire.Rendering;

internal class DoubleBuffer
{
    internal TerminalBuffer FrontBuffer { get; private set; }
    internal TerminalBuffer BackBuffer { get; private set; }

    private DoubleBuffer(TerminalBuffer frontBuffer, TerminalBuffer backBuffer) =>
        (FrontBuffer, BackBuffer) = (frontBuffer, backBuffer);

    internal static DoubleBuffer New(int width, int height) =>
        new(new(width,height), new(width,height));

    internal async Task Resize(int width, int height) =>
        (FrontBuffer, BackBuffer) = (new(width, height), new(width,height));

    internal void Swap() => 
        (BackBuffer, FrontBuffer) = (FrontBuffer, BackBuffer);

}

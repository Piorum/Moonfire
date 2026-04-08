using Moonfire.Ansi;
using Moonfire.Ansi.Registries;
using Moonfire.Logging;
using Moonfire.Rendering.Factories;
using Moonfire.Rendering.Interfaces;

namespace Moonfire.Rendering;

internal class ResizeHelper
{
    private readonly AnsiStringBuilder asb;
    private readonly Renderer renderer;

    internal readonly IWindowResizer windowResizer = WindowResizerFactory.Create();
    internal bool Registered => windowResizer.Registered;

    internal async Task RegisterResizeEvent() =>
        await windowResizer.RegisterResizeEvent(renderer);

    internal ResizeHelper(Renderer renderer)
    {
        asb = new(AnsiRegistry.ClearScreenBytes.Length);
        asb.ClearScreen();

        this.renderer = renderer;
    }

    internal async Task Resize(RootView rootView, DoubleBuffer buffer, RenderQueue renderQueue, TerminalWriter writer)
    {
        await renderQueue.EnqueueAction(async () =>
        {
            var newWidth = Console.BufferWidth;
            var newHeight = Console.BufferHeight;
            await Logger.Debug(nameof(Rendering), $"[Resizing] ({rootView.SizeX},{rootView.SizeY}) -> ({newWidth},{newHeight})");

            //Writes clear screen ANSI command
            await writer.Write(asb);
            await buffer.Resize(newWidth, newHeight);
            await rootView.Invalidate();
        });
    }
}

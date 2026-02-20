using Moonfire.Ansi.Registries;
using Moonfire.Logging;
using Moonfire.Rendering.Terminal;

namespace Moonfire.Rendering;

internal class ResizeHelper(Renderer renderer)
{
    internal readonly IWindowResizer windowResizer = WindowResizerFactory.Create();
    internal bool Registered => windowResizer.Registered;

    internal async Task RegisterResizeEvent() =>
        await windowResizer.RegisterResizeEvent(renderer);

    internal async Task Resize(RootView rootView, DoubleBuffer buffer, RenderQueue renderQueue, TerminalWriter writer)
    {
        await renderQueue.EnqueueAction(async () =>
        {
            var newWidth = Console.BufferWidth;
            var newHeight = Console.BufferHeight;
            await Logger.Debug(nameof(Rendering), $"[Resizing] ({rootView.SizeX},{rootView.SizeY}) -> ({newWidth},{newHeight})");

            await writer.Write(AnsiRegistry.ClearScreen);
            await buffer.Resize(newWidth, newHeight);
            await rootView.Invalidate();
        });
    }
}

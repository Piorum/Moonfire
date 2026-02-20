using Moonfire.Ansi.Registries;
using Moonfire.Logging;
using Moonfire.Rendering.Terminal;

namespace Moonfire.Rendering;

internal class ResizeManager(Renderer renderer)
{
    private readonly IWindowResizer windowResizer = WindowResizerFactory.Create();
    internal bool Registered = false;

    internal async Task RegisterResizeEvent()
    {
        Registered = true;

        await windowResizer.RegisterResizeEvent(renderer);
    }

    internal async Task Resize(RootView rootView, RenderQueueManager renderQueue, TerminalBufferManager buffer, TerminalWriter writer)
    {
        await renderQueue.EnqueueAction(async () =>
        {
            await writer.Write(AnsiRegistry.ClearScreen);

            var newWidth = Console.BufferWidth;
            var newHeight = Console.BufferHeight;

            await Logger.Debug(nameof(Rendering), $"[Resizing] ({rootView.SizeX},{rootView.SizeY}) -> ({newWidth},{newHeight})");

            buffer.FrontBuffer = new(newWidth, newHeight);
            buffer.BackBuffer = new(newWidth, newHeight);

            await rootView.Invalidate();
        });
    }
}

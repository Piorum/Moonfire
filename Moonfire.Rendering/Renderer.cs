using Moonfire.Ansi;
using Moonfire.Ansi.Registries;
using Moonfire.Glyph;
using Moonfire.Logging;
using Moonfire.Rendering.Models;

namespace Moonfire.Rendering;

public class Renderer
{
    private static readonly TerminalWriter writer = new();

    private readonly RenderQueueManager renderQueue = new();
    private readonly ResizeManager resizeManager;
    private readonly TerminalBufferManager buffer ;

    private readonly RootView _rootView;
    private readonly TimeSpan _batchTimeout;

    private readonly RendererState rendererState = new(2048);

    public Renderer(RootView rootView, TimeSpan batchTimeout)
    {
        _rootView = rootView;
        _batchTimeout = batchTimeout;

        resizeManager= new(this);
        buffer = TerminalBufferManager.New(_rootView.SizeX, _rootView.SizeY);
    }

    /// <summary>
    /// Used to enqueue a task to the render queue.
    /// </summary>
    public async Task EnqueueAction(Func<Task> action) =>
        await renderQueue.EnqueueAction(action);

    /// <summary>
    /// Used to enqueue a task to invalidate an area of the screen.
    /// </summary>
    public async Task EnqueueActionClear(int x, int y, int w, int h) =>
        await renderQueue.EnqueueActionClear(x, y, w, h);

    /// <summary>
    /// Used to enqueue a task to execute after the render cycle.
    /// </summary>
    public async Task EnqueueActionPostRender(Func<Task> action) =>
        await renderQueue.EnqueueActionPostRender(action);

    public async Task Start(CancellationToken token)
    {
        if(!resizeManager.Registered)
            await resizeManager.RegisterResizeEvent();

        await writer.Write(AnsiRegistry.EnterAlternateScreen);
        
        await EnqueueAction(_rootView.Invalidate);

        AnsiStringBuilder asb = new();

        while(!token.IsCancellationRequested)
        {
            try
            {
                await Logger.Debug(nameof(Rendering), $"[Starting New Render Cycle]");
                var renderStartTime = DateTime.Now;
                
                if(await renderQueue.WaitAndRun(_batchTimeout, token))
                    await Render(asb);

                await renderQueue.RunPostRenderTasks();

                await Logger.Debug(nameof(Rendering), $" - (Total:    {(DateTime.Now - renderStartTime).TotalMicroseconds}us)");
            }
            catch (OperationCanceledException) {} //Expected
        }

        await writer.Write(AnsiRegistry.ExitAlternateScreen);
        await writer.Write(AnsiRegistry.ShowCursor);
    }

    private async Task Render(AnsiStringBuilder asb)
    {
        //Rearrange, returns true if something was changed
        if (!await _rootView.Arrange())
            return;

        //Force invalidation of front buffer
        if(renderQueue.clearTasks.Count != 0)
        {
            foreach(var clearTask in renderQueue.clearTasks)
                buffer.FrontBuffer.Clear(clearTask);
                
            renderQueue.clearTasks.Clear();
        }

        //Draw to back buffer
        await _rootView.Draw(new TerminalContext(0, 0, buffer.BackBuffer));

        //Clear builder, ensure cursor is hidden for draw, reset state
        asb.Clear();
        asb.HideCursor();
        rendererState.Reset();

        for (int y = 0; y < _rootView.SizeY; y++)
        {
            int x = 0;
            while(x < _rootView.SizeX)
            {
                var cell = buffer.BackBuffer[x,y];
                
                if(cell == buffer.FrontBuffer[x,y])
                {
                    FlushToAsb(asb);

                    x += cell.Width;
                    continue;
                }

                if(cell.StyleId != rendererState.CurrentStyleId)
                    FlushToAsb(asb);

                if(rendererState.OutputIndex == 0)
                {
                    rendererState.CurrentStyleId = cell.StyleId;
                    rendererState.CurrentStyle = AnsiStyleFactory.Get(rendererState.CurrentStyleId);
                    rendererState.OutputStart = (x,y);
                }

                var cluster = GlyphFactory.Get(cell.GlyphId);
                var text = cluster.GraphemeCluster.AsSpan();

                text.CopyTo(rendererState.OutputBuffer.AsSpan(rendererState.OutputIndex));
                rendererState.OutputIndex += text.Length;
                rendererState.CursorMovement += cluster.VisualWidth;

                //Add extra space to "Fake" 2 wide characters
                if(cluster.VisualWidth > 1 && cluster.RealWidth < cluster.VisualWidth)
                    rendererState.OutputBuffer[rendererState.OutputIndex++] = ' ';

                x += cluster.VisualWidth;
            }
            FlushToAsb(asb);
        }
        //Append final escape codes like resetting properties
        asb.ResetProperties();

        await writer.Write(asb.ToString());

        //Swap back buffer to front, clear back buffer
        (buffer.BackBuffer, buffer.FrontBuffer) = (buffer.FrontBuffer, buffer.BackBuffer);
        buffer.BackBuffer.Clear();
    }

    private void FlushToAsb(AnsiStringBuilder asb)
    {
        if (rendererState.OutputIndex == 0)
            return;

        //Change to new style, append text, move cursor if cursor not in the correct place already
        asb.Append(
            rendererState.OutputBuffer.AsSpan(0, rendererState.OutputIndex), 
            rendererState.CurrentStyle,
            rendererState.Cursor == rendererState.OutputStart ? null : rendererState.OutputStart
        );

        rendererState.Cursor = (rendererState.OutputStart.X + rendererState.CursorMovement, rendererState.OutputStart.Y);
        rendererState.OutputIndex = 0; 
        rendererState.CursorMovement = 0;
    }

    internal async Task Resize() =>
        await resizeManager.Resize(_rootView, renderQueue, buffer, writer);

}

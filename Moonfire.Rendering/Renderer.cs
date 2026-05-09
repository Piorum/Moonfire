using Moonfire.Ansi;
using Moonfire.Glyph;
using Moonfire.Logging;
using Moonfire.Rendering.Models;

namespace Moonfire.Rendering;

public class Renderer
{
    private static readonly TerminalWriter writer = new();

    private readonly RenderQueue renderQueue = new();
    private readonly DoubleBuffer buffer ;
    
    private readonly ResizeHelper resizeHelper;

    private readonly RootView _rootView;
    private readonly TimeSpan _batchTimeout;

    public Renderer(RootView rootView, TimeSpan? batchTimeout = null)
    {
        _rootView = rootView;
        _batchTimeout = batchTimeout ?? TimeSpan.FromMicroseconds(100);

        resizeHelper = new(this);
        buffer = DoubleBuffer.New(_rootView.SizeX, _rootView.SizeY);
    }

    public async Task EnqueueAction(Func<Task> action) =>
        await renderQueue.EnqueueAction(action);
    public async Task EnqueueActionClear(int x, int y, int w, int h) =>
        await renderQueue.EnqueueActionClear(x, y, w, h);
    public async Task EnqueueActionPostRender(Func<Task> action) =>
        await renderQueue.EnqueueActionPostRender(action);

    public async Task Run(CancellationToken token)
    {
        AnsiStringBuilder asb = new();
        RenderState rs = new();

        if(!resizeHelper.Registered)
            await resizeHelper.RegisterResizeEvent();

        asb.EnterAlternateScreen().HideCursor();
        await writer.Write(asb);
        
        await EnqueueAction(_rootView.Invalidate);

        while(!token.IsCancellationRequested)
        {
            try
            {
                var shouldRender = await renderQueue.WaitAndRun(_batchTimeout, token);

                await Logger.Debug(nameof(Rendering), $"[Starting New Render Cycle]");
                var renderStartTime = DateTime.Now;
                
                if(shouldRender)
                    await Render(asb, rs);

                await renderQueue.RunPostRenderTasks();

                await Logger.Debug(nameof(Rendering), $" - (Total:    {(DateTime.Now - renderStartTime).TotalMicroseconds}us)");
            }
            catch (OperationCanceledException) {} //Expected
        }

        asb.Clear();
        asb.ExitAlternateScreen().ShowCursor();
        await writer.Write(asb);
    }

    private async Task Render(AnsiStringBuilder asb, RenderState rs)
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
        await _rootView.Draw(new TerminalContext(buffer.BackBuffer));

        //Clear builder, reset state
        asb.Clear();
        rs.Reset();

        var frontBuffer = buffer.FrontBuffer.AsSpan();
        var backBuffer = buffer.BackBuffer.AsSpan();
        var width = _rootView.SizeX;

        for (int y = 0; y < _rootView.SizeY; y++)
        {
            int x = 0;
            var offset = y * width;
            while(x < width)
            {
                var cell = backBuffer[offset + x];
                
                if(cell == frontBuffer[offset + x])
                {
                    rs.FlushToAsb(asb);

                    x += cell.Width;
                    continue;
                }

                if(cell.StyleId != rs.CurrentStyleId)
                    rs.FlushToAsb(asb);

                if(rs.OutputIndex == 0)
                {
                    rs.CurrentStyleId = cell.StyleId;
                    rs.CurrentStyle = AnsiStyleFactory.Get(rs.CurrentStyleId);
                    rs.OutputStart = (x,y);
                }

                var glyph = GlyphFactory.Get(cell.GlyphId);
                x += glyph.VisualWidth;

                //Fallback to space if 2 wide glyph will be out of bounds.
                if(x > width)
                {
                    rs.OutputBuffer[rs.OutputIndex++] = (byte)' ';
                    rs.CursorMovement++;
                    continue;
                }

                var bytes = glyph.GraphemeCluster.AsSpan();

                bytes.CopyTo(rs.OutputBuffer.AsSpan(rs.OutputIndex));

                rs.OutputIndex += bytes.Length;
                rs.CursorMovement += glyph.VisualWidth;

                //Add extra space to "Fake" 2 wide characters
                if(glyph.VisualWidth > 1 && glyph.RealWidth < glyph.VisualWidth)
                    rs.OutputBuffer[rs.OutputIndex++] = (byte)' ';
            }
            rs.FlushToAsb(asb);
        }
        //Append final escape codes like resetting properties
        asb.ResetProperties();

        await writer.Write(asb);

        //Swap back buffer to front, clear back buffer
        buffer.Swap();
        buffer.BackBuffer.Clear();
    }

    internal async Task Resize() =>
        await resizeHelper.Resize(_rootView, buffer, renderQueue, writer);

}

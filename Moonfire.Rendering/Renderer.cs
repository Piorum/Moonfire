using Moonfire.Ansi;
using Moonfire.Ansi.Registries;
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

    public Renderer(RootView rootView, TimeSpan batchTimeout)
    {
        _rootView = rootView;
        _batchTimeout = batchTimeout;

        resizeHelper= new(this);
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
        if(!resizeHelper.Registered)
            await resizeHelper.RegisterResizeEvent();

        await writer.Write(AnsiRegistry.EnterAlternateScreen);
        
        await EnqueueAction(_rootView.Invalidate);

        AnsiStringBuilder asb = new();
        RenderState rs = new(2048);

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

        await writer.Write(AnsiRegistry.ExitAlternateScreen);
        await writer.Write(AnsiRegistry.ShowCursor);
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
        await _rootView.Draw(new TerminalContext(0, 0, buffer.BackBuffer));

        //Clear builder, ensure cursor is hidden for draw, reset state
        asb.Clear();
        asb.HideCursor();
        rs.Reset();

        for (int y = 0; y < _rootView.SizeY; y++)
        {
            int x = 0;
            while(x < _rootView.SizeX)
            {
                var cell = buffer.BackBuffer[x,y];
                
                if(cell == buffer.FrontBuffer[x,y])
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

                var cluster = GlyphFactory.Get(cell.GlyphId);
                var text = cluster.GraphemeCluster.AsSpan();

                text.CopyTo(rs.OutputBuffer.AsSpan(rs.OutputIndex));
                rs.OutputIndex += text.Length;
                rs.CursorMovement += cluster.VisualWidth;

                //Add extra space to "Fake" 2 wide characters
                if(cluster.VisualWidth > 1 && cluster.RealWidth < cluster.VisualWidth)
                    rs.OutputBuffer[rs.OutputIndex++] = ' ';

                x += cluster.VisualWidth;
            }
            rs.FlushToAsb(asb);
        }
        //Append final escape codes like resetting properties
        asb.ResetProperties();

        await writer.Write(asb.ToString());

        //Swap back buffer to front, clear back buffer
        (buffer.BackBuffer, buffer.FrontBuffer) = (buffer.FrontBuffer, buffer.BackBuffer);
        buffer.BackBuffer.Clear();
    }

    internal async Task Resize() =>
        await resizeHelper.Resize(_rootView, buffer, renderQueue, writer);

}

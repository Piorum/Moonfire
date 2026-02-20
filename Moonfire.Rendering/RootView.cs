using Moonfire.Rendering.Models;
using Moonfire.Rendering.Interfaces;

namespace Moonfire.Rendering;

/// <summary>
/// Root view that all arranges, draws, and invalidations will initially propogate from.
/// </summary>
/// <param name="sizeX">Requested Width or null for Console.BufferWidth</param>
/// <param name="sizeY">Requested Height or null for Console.BufferHeight</param>
public class RootView(int? sizeX = null, int? sizeY = null) : IMoonfireView
{
    public int OriginX { set; get; } = 0;
    public int OriginY { set; get; } = 0;
    public int SizeX { set; get; } = sizeX ?? Console.BufferWidth;
    public int SizeY { set; get; } = sizeY ?? Console.BufferHeight;

    public bool Dirty { set; get; }

    required public IMoonfireView View;

    public async Task<bool> Arrange()
    {
        //Tracking wasDirty to avoid resizing edge case
        bool wasDirty = false;
        if (Dirty)
        {
            await OnArrange();
            Dirty = false;
            wasDirty = true;
        }

        var workDone = await View.Arrange();

        return workDone || wasDirty;
    }

    private Task OnArrange()
    {
        View.OriginX = 0;
        View.OriginY = 0;

        View.SizeX = SizeX;
        View.SizeY = SizeY;

        return Task.CompletedTask;
    }

    public async Task Draw(TerminalContext context) =>
        await View.Draw(context);

    public async Task Invalidate()
    {
        Dirty = true;
        await View.Invalidate();
    }
}

namespace Moonfire.Rendering.Models;

public class TerminalBuffer(int width, int height)
{
    private readonly TerminalCell[] cells = new TerminalCell[width * height];
    public int Width { private set; get; } = width;
    public int Height { private set; get; } = height;

    public ref TerminalCell this[int x, int y]
    {
        get => ref cells[y * Width + x];
    }

    public Span<TerminalCell> AsSpan() =>
        cells.AsSpan();

    public void Clear((int x, int y, int w, int h) area)
    {
        for(int i = area.y; i < area.y + area.h; i++)
            Array.Clear(cells, i * Width + area.x, area.w);
    }

    public void Clear() =>
        Array.Clear(cells);
}

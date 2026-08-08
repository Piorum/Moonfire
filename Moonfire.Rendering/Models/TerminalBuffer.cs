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

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void InvalidFill(int Width, int Height, int x, int y, int w, int h) =>
            throw new ArgumentException($"Invalid use of {nameof(TerminalBuffer)} {nameof(Fill)} method. Given invalid clear dimensions for the current size.\n[Width] : {Width}\n[Height] : {Height}\n[X] : {x}\n[Y] : {y}\n[W] : {w}\n[H] : {h}\n");

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void InvalidCopyTo(TerminalCell[] source, TerminalCell[] destination) =>
            throw new ArgumentException($"Invalid use of {nameof(TerminalBuffer)} {nameof(CopyTo)} method, given mismatched sized buffers.\n[Source] : {source.Length}\n[Destination] : {destination.Length}\n");

    internal void CopyTo(TerminalBuffer destination){
        if(destination.cells.Length != cells.Length)
            InvalidCopyTo(cells, destination.cells);
            
        cells.CopyTo(destination.cells);
    }

    public void Fill((int x, int y, int w, int h) area, TerminalCell cell)
    {
        var (x, y, w, h) = area;

        if((uint)x > (uint)Width || (uint)y > (uint)Height || (uint)w > (uint)(Width - x) || (uint)h > (uint)(Height - y))
            InvalidFill(Width, Height, x, y, w, h);

        //Contiguous Path
        if(x == 0 && w == Width)
        {
            cells.AsSpan(y * Width, Width * h).Fill(cell);
            return;
        }

        var endY = y+h;
        var span = AsSpan();
        for(int i = y; i < endY; i++)
        {
            span.Slice(i * Width + x, w).Fill(cell);
        }
    }
}

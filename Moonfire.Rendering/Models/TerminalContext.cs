namespace Moonfire.Rendering.Models;

/// <summary>
/// Minimal context struct that shifts the origin and bounds checks SVBuffer.
/// </summary>
public readonly struct TerminalContext
{
    public readonly int X;
    public readonly int Y;
    public readonly uint W;
    public readonly uint H;

    private readonly TerminalBuffer Buffer;

    private static void CreationOutOfBounds(int x,int y, int w, int h, TerminalContext context) => 
        throw new($"SVContext region is out of bounds for the existing SVContext [Parameters] X:\"{x}\" | Y:\"{y}\" | W:\"{w}\" | H:\"{h}\" : [Context Dimensions] X:\"{context.X}\" | Y: \"{context.Y}\" | W:\"{context.W}\" | H:\"{context.H}\"");
    private static void AccessOutOfBounds(int X, int Y, uint W, uint H, int x, int y) => 
        throw new($"Coordinate out of bounds for the current SVContext X:\"{X}\" | Y:\"{Y}\" | W:\"{W}\" | H:\"{H}\" : Coordinate:({x},{y})");
    private static void CopyTooLarge(uint sW, uint sH, uint dW, uint dH) => 
        throw new($"TerminalContext to large to copy to given TerminalContext Source W:\"{sW}\" | H:\"{sH}\" : Destination W:\"{dW}\" | H:\"{dH}\"");

    /// <param name="x">X coordinate of new origin.</param>
    /// <param name="y">Y coordinate of new origin.</param>
    /// <param name="w">Width of new context.</param>
    /// <param name="h">Height of new context.</param>
    /// <param name="buffer">Buffer that data will be referenced from.</param>
    public TerminalContext(TerminalBuffer buffer)
    {
        Buffer = buffer;
        X = 0;
        Y = 0;
        W = (uint)buffer.Width;
        H = (uint)buffer.Height;
    }

    /// <param name="x">X coordinate of new origin.</param>
    /// <param name="y">Y coordinate of new origin.</param>
    /// <param name="w">Width of new context.</param>
    /// <param name="h">Height of new context.</param>
    /// <param name="context">Context that data will be referenced from.</param>
    public TerminalContext(int x, int y, int w, int h, TerminalContext context)
    {
        if(x < context.X || y < context.Y || w < 0 || h < 0 || x + w > context.X + context.W || y + h > context.Y + context.H)
            CreationOutOfBounds(x,y,w,h,context);

        Buffer = context.Buffer;
        X = x;
        Y = y;
        W = (uint)w;
        H = (uint)h;
    }

    /// <summary></summary>
    /// <returns>Cell at (x,y) relative to the top left origin (OriginX,OriginY)</returns>
    public ref TerminalCell this[int x, int y]
    {
        get
        {
            if((uint)x >= W || (uint)y >= H)
                AccessOutOfBounds(X,Y,W,H,x,y);

            return ref Buffer[X + x, Y + y];
        }
    }    
    
    private ref TerminalCell Raw(int x, int y) =>
        ref Buffer [X + x, Y + y];

    public void CopyTo(TerminalContext destination)
    {
        if(destination.H < H || destination.W < W)
            CopyTooLarge(W, H, destination.W, destination.H);

        for(int y = 0; y < H; y++)
            for(int x = 0; x < W; x++)
                destination.Raw(x, y) = Raw(x, y);
    }
    
    public TerminalContextEnumerator GetEnumerator() => new(this);

    public ref struct TerminalContextEnumerator
    {
        private readonly TerminalContext _context;
        private int _x;
        private int _y;

        public TerminalContextEnumerator(TerminalContext context) =>
            (_context, _x, _y) = (context, 0, 0);

        public bool MoveNext()
        {
            _x++;
            if(_x >= _context.W)
            {
                _x = 0;
                _y++;
            }

            return _y < _context.H;
        }

        public readonly ref TerminalCell Current => ref _context.Raw(_x, _y);
    }
}

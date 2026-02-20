using Moonfire.Ansi.Models;

namespace Moonfire.Rendering.Models;

public class RendererState(int bufferSize)
{
    //Oversized to allow for DWC, ZWC, etc...
    public char[] OutputBuffer = new char[bufferSize];
    public int OutputIndex;
    public int CursorMovement;

    public int CurrentStyleId;
    public AnsiStyleData CurrentStyle = new();

    public (int X, int Y) OutputStart;
    public (int X, int Y) Cursor;

    public void Reset()
    {
        OutputIndex = 0;
        CursorMovement = 0;

        CurrentStyle = new();

        OutputStart = (0, 0);
        Cursor = (-1, -1);
    }
}

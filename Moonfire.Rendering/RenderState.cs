using Moonfire.Ansi;
using Moonfire.Ansi.Models;

namespace Moonfire.Rendering;

internal class RenderState(int bufferSize = 4096)
{
    //Oversized to allow for DWC, ZWC, etc...
    internal byte[] OutputBuffer = new byte[bufferSize];
    internal int OutputIndex;
    internal int CursorMovement;

    internal int CurrentStyleId;
    internal AnsiStyleData CurrentStyle = new();

    internal (int X, int Y) OutputStart;
    internal (int X, int Y) Cursor;

    internal void FlushToAsb(AnsiStringBuilder asb)
    {
        if (OutputIndex == 0)
            return;

        //Change to new style, append text, move cursor if cursor not in the correct place already
        asb.Append(
            OutputBuffer.AsSpan(0, OutputIndex), 
            CurrentStyle,
            Cursor == OutputStart ? null : OutputStart
        );

        Cursor = (OutputStart.X + CursorMovement, OutputStart.Y);
        OutputIndex = 0; 
        CursorMovement = 0;
    }

    internal void Reset()
    {
        OutputIndex = 0;
        CursorMovement = 0;

        CurrentStyle = new();

        OutputStart = (0, 0);
        Cursor = (-1, -1);
    }
}

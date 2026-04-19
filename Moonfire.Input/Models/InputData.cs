namespace Moonfire.Input.Models;

public class InputData
{
    public char? UTFChar { get; } = null;
    public int? X { get; } = null;
    public int? Y { get; } = null;
    public int? ScrollDelta { get; } = null;

    private InputData(char? utfChar = null, int? x = null, int? y = null, int? scrollDelta = null)
    {
        UTFChar = utfChar;

        X = x;
        Y = y;
        ScrollDelta = scrollDelta;
    }

    public static InputData KeyboardData(char utfChar) =>
        new(utfChar: utfChar);
    public static InputData MouseData(int x, int y, int? scrollDelta = null) =>
        new(x: x, y: y, scrollDelta: scrollDelta);
}

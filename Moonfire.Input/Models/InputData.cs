namespace Moonfire.Input.Models;

public readonly record struct InputData(
    char? UTFChar = null,
    int? X = null,
    int? Y = null,
    int? ScrollDelta = null
)
{
    public static InputData KeyboardData(char utfChar) =>
        new(UTFChar: utfChar);

    public static InputData MouseData(int x, int y, int? scrollDelta = null) =>
        new(X: x, Y: y, ScrollDelta: scrollDelta);
}

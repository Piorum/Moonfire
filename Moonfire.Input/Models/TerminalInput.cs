using Moonfire.Input.Enums;

namespace Moonfire.Input.Models;

public class TerminalInput
{
    public InputKey Key { get; }
    public InputData InputData { get; }

    private TerminalInput(InputKey key, InputData inputData)
    {
        Key = key;
        InputData = inputData;
    }

    public static TerminalInput KeyboardInput(ConsoleKey keyboardKey, char utfChar, InputModifier? modifiers = null) =>
        new(InputKey.KeyboardBind(keyboardKey, modifiers), InputData.KeyboardData(utfChar));
    public static TerminalInput MouseInput(MouseAction mouseKey, int x, int y, int? scrollDelta = null, InputModifier? modifiers = null) =>
        new(InputKey.MouseBind(mouseKey, modifiers), InputData.MouseData(x, y, scrollDelta));
}

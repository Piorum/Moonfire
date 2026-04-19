using Moonfire.Input.Enums;

namespace Moonfire.Input.Models;

public class InputKey
{
    public InputType InputType { get; }
    public InputModifier Modifiers { get; } = InputModifier.None;

    public ConsoleKey? KeyboardKey { get; } = null;

    public MouseAction? MouseKey { get; } = null;

    private InputKey(InputType inputType, InputModifier modifiers, ConsoleKey? key = null, MouseAction? mouseKey = null)
    {
        InputType = inputType;
        Modifiers = modifiers;

        KeyboardKey = key;

        MouseKey = mouseKey;
    }

    public static InputKey KeyboardBind(ConsoleKey key, InputModifier? modifiers = null) =>
        new(InputType.Keyboard, modifiers ?? InputModifier.None, key: key);
    public static InputKey MouseBind(MouseAction mouseKey, InputModifier? modifiers = null) =>
        new(InputType.Mouse, modifiers ?? InputModifier.None, mouseKey: mouseKey);
}

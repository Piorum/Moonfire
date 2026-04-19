using Moonfire.Input.Enums;

namespace Moonfire.Input.Models;

public readonly record struct InputKey(
    InputType InputType,
    InputModifier Modifiers = InputModifier.None,
    ConsoleKey? KeyboardKey = null,
    MouseAction? MouseKey = null
)
{
    public static InputKey KeyboardBind(ConsoleKey key, InputModifier? modifiers = null) =>
        new(InputType.Keyboard, modifiers ?? InputModifier.None, KeyboardKey: key);

    public static InputKey MouseBind(MouseAction mouseKey, InputModifier? modifiers = null) =>
        new(InputType.Mouse, modifiers ?? InputModifier.None, MouseKey: mouseKey);
}

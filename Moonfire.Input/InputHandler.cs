using Moonfire.Input.Models;
using Moonfire.Logging;

namespace Moonfire.Input;

public class InputHandler(int? sequenceTimeoutMs = null)
{
    private readonly IInputTranslator inputTranslator = InputTraslatorFactory.Create();

    internal readonly Dictionary<InputKey, Bind> indifferentBinds = [];
    internal readonly SequenceState sequenceState = new(sequenceTimeoutMs ?? 1000);

    private RawInputChannel? rawInputChannel = null;

    public async Task Run(CancellationToken token)
    {
        //Move to background thread
        await Task.Run(async () =>
        {
            try
            {
                var inputChannel = inputTranslator.PollInput(token);

                await foreach(var evt in inputChannel.ReadAllAsync(token))
                    await HandleBind(evt, token);
            }
            catch (OperationCanceledException) {}
        }, CancellationToken.None);
    }

    public RawInputChannel OpenRaw()
    {
        RawInputChannel newRawInputChannel = new();
        var existingRawInputChannel = Interlocked.Exchange(ref rawInputChannel, newRawInputChannel);

        existingRawInputChannel?.Dispose();

        return newRawInputChannel;
    }

    public void CloseRaw()
    {
        var oldInputChannel = Interlocked.Exchange(ref rawInputChannel, null);
        oldInputChannel?.Dispose();
    }

    public KeybindBuilder Bind() =>
        new(this);

    private async Task HandleBind(TerminalInput evt, CancellationToken token)
    {
        await Logger.Debug(nameof(Input), $"[Key Received]");
        
        await Logger.Debug(nameof(Input), evt.Key.InputType switch
        {
            Enums.InputType.Mouse => $" - (Key: {evt.Key.MouseKey}, Modifiers: {evt.Key.Modifiers})",
            Enums.InputType.Keyboard => $" - (Key: {evt.Key.KeyboardKey}, Modifiers: {evt.Key.Modifiers})",
            _ => " - None"
        });
        await Logger.Debug(nameof(Input), evt.Key.InputType switch
        {
            Enums.InputType.Mouse => (evt.InputData.ScrollDelta is null) switch
                {
                    true => $" - (X: {evt.InputData.X}, Y: {evt.InputData.Y})",
                    false => $" - (X: {evt.InputData.X}, Y: {evt.InputData.Y}, ScrollDelta: {evt.InputData.ScrollDelta})"
                },
            Enums.InputType.Keyboard => $" - (UTFChar: {evt.InputData.UTFChar})",
            _ => " - None"
        });

        if(rawInputChannel is not null)
        {
            await rawInputChannel.Writer.WriteAsync(evt, token);
            return;
        }

        var sequenceBind = sequenceState.Step(evt);
        if(sequenceBind is not null)
        {
            await SafeExecuteBind(sequenceBind.Value, evt.InputData);
            return;
        }

        if(indifferentBinds.TryGetValue(evt.Key, out var indifferentBind))
            await SafeExecuteBind(indifferentBind, evt.InputData);
    }

    private async Task SafeExecuteBind(Bind bind, InputData inputData)
    {
        await Logger.Debug(nameof(Input), $"[Bind Received]");

        try
        {
            await bind.Task(inputData);
        }
        catch (Exception ex)
        {
            await Logger.Error(nameof(Input), $"Binding Failed\n{ex.Message}");
        }
    }
}

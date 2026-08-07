using Moonfire.Input.Models;
using Moonfire.Logging;

namespace Moonfire.Input;

public class InputHandler(int? sequenceTimeoutMs = null)
{
    private readonly IInputTranslator inputTranslator = InputTraslatorFactory.Create();
    internal readonly BindResolver bindResolver = new(sequenceTimeoutMs);

    private RawInputChannel? rawInputChannel = null;

    public async Task Run(CancellationToken token)
    {
        var inputWorker = Task.Run(async () =>
        {
            try
            {
                var inputChannel = inputTranslator.PollInput(token);

                await foreach(var evt in inputChannel.ReadAllAsync(token))
                    await HandleBind(evt, token);
            }
            catch (OperationCanceledException) {}
        }, CancellationToken.None);

        var bindWorker = Task.Run(async () =>
        {
            try
            {
                await foreach(var (bind, data) in bindResolver.BindReader.ReadAllAsync(token))
                    await SafeExecuteBind(bind, data);
            }
            catch (OperationCanceledException) {}
        }, CancellationToken.None);

        await Task.WhenAll(inputWorker, bindWorker);
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
        new(bindResolver);

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

        await bindResolver.Process(evt, token);
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

using Moonfire.Input.Models;
using Moonfire.Logging;

namespace Moonfire.Input;

public class InputHandler(int? sequenceTimeoutMs = null)
{
    private readonly IInputTranslator inputTranslator = InputTraslatorFactory.Create();
    internal readonly BindResolver bindResolver = new(sequenceTimeoutMs);

    private RawInputChannel? rawInputChannel = null;

    public void Run(CancellationToken token)
    {
        inputTranslator.Start(token);

        Thread routerThread = new(() =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var evt = inputTranslator.Take(token);
                    HandleBind(evt);
                }
            }
            catch (OperationCanceledException) {}
        })
        {
            IsBackground = true,
            Name = "MoonfireInputRouter"
        };

        Thread executorThread = new(() =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var (bind, data) = bindResolver.Take(token);
                    SafeExecuteBind(bind, data);
                }
            }
            catch (OperationCanceledException) {}
        })
        {
            IsBackground = true,
            Name = "MoonfireBindExecutor"
        };

        routerThread.Start();
        executorThread.Start();
    }

    public RawInputChannel OpenRaw()
    {
        RawInputChannel newRawInputChannel = null!;

        newRawInputChannel = new RawInputChannel(() =>
        {
            Interlocked.CompareExchange(ref rawInputChannel, null, newRawInputChannel);
        });

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

    private void HandleBind(TerminalInput evt)
    {
        Logger.Debug(nameof(Input), $"[Key Received]");
        
        Logger.Debug(nameof(Input), evt.Key.InputType switch
        {
            Enums.InputType.Mouse => $" - (Key: {evt.Key.MouseKey}, Modifiers: {evt.Key.Modifiers})",
            Enums.InputType.Keyboard => $" - (Key: {evt.Key.KeyboardKey}, Modifiers: {evt.Key.Modifiers})",
            _ => " - None"
        });
        Logger.Debug(nameof(Input), evt.Key.InputType switch
        {
            Enums.InputType.Mouse => (evt.InputData.ScrollDelta is null) switch
                {
                    true => $" - (X: {evt.InputData.X}, Y: {evt.InputData.Y})",
                    false => $" - (X: {evt.InputData.X}, Y: {evt.InputData.Y}, ScrollDelta: {evt.InputData.ScrollDelta})"
                },
            Enums.InputType.Keyboard => $" - (UTFChar: {evt.InputData.UTFChar})",
            _ => " - None"
        });

        var raw = Volatile.Read(ref rawInputChannel);
        if(raw is not null && !raw.IsDisposed)
        {
            if(raw.Write(evt))
                return;
        }

        bindResolver.Process(evt);
    }

    private void SafeExecuteBind(Bind bind, InputData inputData)
    {
        Logger.Debug(nameof(Input), $"[Bind Received]");

        try
        {
            bind.Action(inputData);
        }
        catch (Exception ex)
        {
            Logger.Error(nameof(Input), $"Binding Failed\n{ex.Message}");
        }
    }
}

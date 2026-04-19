using System.Threading.Channels;
using Moonfire.Input.Models;
using Moonfire.Logging;

namespace Moonfire.Input;

public class InputHandler(int sequenceTimeoutMs = 1000)
{
    private readonly IInputTranslator inputTranslator = InputTraslatorFactory.Create();

    internal readonly Dictionary<InputKey, Bind> indifferentBinds = [];
    internal readonly SequenceState sequenceState = new(sequenceTimeoutMs);

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
        Interlocked.Exchange(ref rawInputChannel, newRawInputChannel);

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
        try
        {
            await bind.Task(inputData);
        }
        catch (Exception ex)
        {
            await Logger.Error(nameof(Input), $"Binding Failed\n{ex.Message}");
        }
    }

    public class RawInputChannel() : IDisposable
    {
        private bool _disposed;

        private Channel<TerminalInput>? channel = Channel.CreateUnbounded<TerminalInput>();
        public ChannelReader<TerminalInput> Reader => _disposed 
            ? throw new ObjectDisposedException(nameof(RawInputChannel)) 
            : channel!.Reader;

        internal ChannelWriter<TerminalInput> Writer => _disposed 
            ? throw new ObjectDisposedException(nameof(RawInputChannel)) 
            : channel!.Writer;

        public void Dispose()
        {
            if(Interlocked.Exchange(ref _disposed, true) == false)
            {
                channel!.Writer.TryComplete();
                channel = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}

using System.Threading.Channels;
using Moonfire.Input.Enums;
using Moonfire.Input.Models;

namespace Moonfire.Input.Platform.Fallback;

public class FallbackInputTranslator : IInputTranslator
{
    public ChannelReader<TerminalInput> PollInput(CancellationToken token)
    {
        Channel<TerminalInput> inputChannel = Channel.CreateUnbounded<TerminalInput>();

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var key = await ReadKeyAsync(token);
                    var inputEvent = await Translate(key);

                    await inputChannel.Writer.WriteAsync(inputEvent, token);
                }
                catch (OperationCanceledException) { }
            }
        }, token);

        return inputChannel.Reader;
    }

    private static Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken token)
    {
        var tcs = new TaskCompletionSource<ConsoleKeyInfo>();
        var cancellationRegistration = token.Register(() => tcs.TrySetCanceled(token));

        _ = Task.Run(() =>
        {
            try
            {
                var key = Console.ReadKey(true);

                tcs.TrySetResult(key);
            }
            catch (InvalidOperationException)
            {
                tcs.TrySetCanceled();
            }
        }, CancellationToken.None);

        return tcs.Task;
    }

    private static Task<TerminalInput> Translate(ConsoleKeyInfo keyInfo)
    {
        var terminalInput = TerminalInput.KeyboardInput(keyInfo.Key, keyInfo.KeyChar, (InputModifier)keyInfo.Modifiers);

        return Task.FromResult(terminalInput);
    }
}

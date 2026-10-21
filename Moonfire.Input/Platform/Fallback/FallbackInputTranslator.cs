using System.Collections.Concurrent;
using Moonfire.Input.Enums;
using Moonfire.Input.Models;

namespace Moonfire.Input.Platform.Fallback;

public class FallbackInputTranslator : IInputTranslator
{
    private readonly BlockingCollection<TerminalInput> inputQueue = [];
    private int started;

    public void Start(CancellationToken token)
    {
        if(Interlocked.Exchange(ref started, 1) == 1)
            return;

        var thread = new Thread(() =>
        {
            while(!token.IsCancellationRequested)
            {
                try
                {
                    var key = Console.ReadKey(true);

                    if(token.IsCancellationRequested)
                        break;

                    var inputEvent = Translate(key);
                    inputQueue.Add(inputEvent);
                }
                catch (InvalidOperationException)
                {
                    break;
                }
            }

            inputQueue.CompleteAdding();
        })
        {
            IsBackground = true,
            Name = "SunfireFallbackInputReader"
        };

        thread.Start();
    }

    public TerminalInput Take(CancellationToken token) => inputQueue.Take(token);

    private static TerminalInput Translate(ConsoleKeyInfo keyInfo)
    {
        var terminalInput = TerminalInput.KeyboardInput(keyInfo.Key, keyInfo.KeyChar, (InputModifier)keyInfo.Modifiers);

        return terminalInput;
    }
}

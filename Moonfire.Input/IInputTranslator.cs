using System.Threading.Channels;
using Moonfire.Input.Models;
using Moonfire.Input.Platform.Fallback;

namespace Moonfire.Input;

public interface IInputTranslator
{
    ChannelReader<TerminalInput> PollInput(CancellationToken token);
}

internal static class InputTraslatorFactory
{
    public static IInputTranslator Create()
    {
        return new FallbackInputTranslator();
    }
}

using Moonfire.Input.Models;
using Moonfire.Input.Platform.Fallback;

namespace Moonfire.Input;

public interface IInputTranslator
{
    void Start(CancellationToken token);
    TerminalInput Take(CancellationToken token);
}

internal static class InputTraslatorFactory
{
    public static IInputTranslator Create()
    {
        return new FallbackInputTranslator();
    }
}

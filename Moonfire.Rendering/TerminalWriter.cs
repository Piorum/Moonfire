using Moonfire.Ansi;

namespace Moonfire.Rendering;

internal class TerminalWriter
{
    private readonly Stream s_stdout = Console.OpenStandardOutput();

    public async Task Write(AnsiStringBuilder asb)
    {
        if(asb.IsEmpty) 
            return;

        await s_stdout.WriteAsync(asb.ToBuffer());
        await s_stdout.FlushAsync();
    }
}

using Moonfire.Ansi;

namespace Moonfire.Rendering;

internal class TerminalWriter
{
    private readonly Stream s_stdout = Console.OpenStandardOutput();

    public void Write(AnsiStringBuilder asb)
    {
        if(asb.IsEmpty) 
            return;

        s_stdout.Write(asb.ToBuffer().Span);
        s_stdout.Flush();
    }
}

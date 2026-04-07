using System.Text;
using Moonfire.Ansi;

namespace Moonfire.Rendering;

internal class TerminalWriter
{
    private readonly Stream s_stdout = Console.OpenStandardOutput();
    private readonly UTF8Encoding s_utf8Encoder = new(false);

    public async Task Write(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        byte[] bytes = s_utf8Encoder.GetBytes(text);

        await s_stdout.WriteAsync(bytes);
        await s_stdout.FlushAsync();
    }

    public async Task Write(AnsiStringBuilder asb)
    {
        if(asb.IsEmpty) 
            return;

        await s_stdout.WriteAsync(asb.ToBuffer());
        await s_stdout.FlushAsync();
    }
}

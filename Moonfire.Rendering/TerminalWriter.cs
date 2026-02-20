using System.Text;

namespace Moonfire.Rendering;

internal class TerminalWriter
{
    private readonly Stream s_stdout = Console.OpenStandardOutput();
    private readonly UTF8Encoding s_utf8Encoder = new(false);

    internal async Task Write(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        byte[] bytes = s_utf8Encoder.GetBytes(text);

        await s_stdout.WriteAsync(bytes);
        await s_stdout.FlushAsync();
    }
}

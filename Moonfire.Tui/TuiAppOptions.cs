namespace Moonfire.Tui;

public record TuiAppOptions
{
    public int? SequenceKeybindsTimeoutMs { init; get; } = null;
    public TimeSpan? RendererBatchTimeout { init; get; } = null;
}

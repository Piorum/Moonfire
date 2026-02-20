using Moonfire.Rendering.Terminal.Platforms.Linux;

namespace Moonfire.Rendering.Terminal;

public interface IWindowResizer
{
    Task RegisterResizeEvent(Renderer root);
    bool Registered { get; }
}

internal static class WindowResizerFactory
{
    public static IWindowResizer Create()
    {
        if (OperatingSystem.IsLinux())
            return new LinuxWindowResizer();

        throw new PlatformNotSupportedException();
    }
}
using Moonfire.Rendering.Interfaces;
using Moonfire.Rendering.Platforms.Linux;

namespace Moonfire.Rendering.Factories;

internal static class WindowResizerFactory
{
    public static IWindowResizer Create()
    {
        if (OperatingSystem.IsLinux())
            return new LinuxWindowResizer();

        throw new PlatformNotSupportedException();
    }
}

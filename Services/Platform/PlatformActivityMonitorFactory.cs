using System;

namespace firstProject.Services.Platform;

public static class PlatformActivityMonitorFactory
{
    public static IPlatformActivityMonitor Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatformActivityMonitor();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacPlatformActivityMonitor();
        }

        return new NoopPlatformActivityMonitor();
    }

    public static IIdleTimeProvider CreateIdleTimeProvider()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsIdleTimeProvider();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacIdleTimeProvider();
        }

        return new NoopIdleTimeProvider();
    }

    private sealed class NoopPlatformActivityMonitor : IPlatformActivityMonitor
    {
        public bool HasRequiredPermissions() => true;

        public bool RequestPermissions() => true;

        public string GetActiveWindowTitle() => "Desktop / Unknown";

        public string? CaptureScreenshot(string screenshotFolder) => null;

        public string? LastError => null;
    }

    private sealed class NoopIdleTimeProvider : IIdleTimeProvider
    {
        public TimeSpan GetIdleTime() => TimeSpan.Zero;
    }
}

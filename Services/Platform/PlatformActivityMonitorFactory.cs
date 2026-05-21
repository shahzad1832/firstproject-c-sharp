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

    private sealed class NoopPlatformActivityMonitor : IPlatformActivityMonitor
    {
        public string GetActiveWindowTitle() => "Desktop / Unknown";

        public string? CaptureScreenshot(string screenshotFolder) => null;
    }
}
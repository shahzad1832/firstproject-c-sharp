namespace firstProject.Services.Platform;

public interface IPlatformActivityMonitor
{
    string GetActiveWindowTitle();

    string? CaptureScreenshot(string screenshotFolder);
}
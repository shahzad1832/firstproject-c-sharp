namespace firstProject.Services.Platform;

public interface IPlatformActivityMonitor
{
    bool HasRequiredPermissions();

    bool RequestPermissions();

    string GetActiveWindowTitle();

    string? CaptureScreenshot(string screenshotFolder);

    string? LastError { get; }
}

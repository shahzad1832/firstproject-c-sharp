using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace firstProject.Services.Platform;

[SupportedOSPlatform("macos")]
public sealed class MacPlatformActivityMonitor : IPlatformActivityMonitor
{
    public string GetActiveWindowTitle()
    {
        string? title = RunCommand(
            "/usr/bin/osascript",
            "-e 'tell application \"System Events\" to get name of first application process whose frontmost is true'");

        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        return "Desktop / Unknown";
    }

    public string? CaptureScreenshot(string screenshotFolder)
    {
        try
        {
            Directory.CreateDirectory(screenshotFolder);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(screenshotFolder, $"screenshot_{timestamp}.png");

            string? result = RunCommand("/usr/sbin/screencapture", $"-x \"{filePath}\"");

            return !string.IsNullOrWhiteSpace(result) || File.Exists(filePath) ? filePath : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? RunCommand(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            return null;
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return string.IsNullOrWhiteSpace(error) ? null : error.Trim();
        }

        return string.IsNullOrWhiteSpace(output) ? string.Empty : output.Trim();
    }
}
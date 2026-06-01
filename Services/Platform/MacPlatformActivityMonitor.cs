using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace firstProject.Services.Platform;

[SupportedOSPlatform("macos")]
public sealed class MacPlatformActivityMonitor : IPlatformActivityMonitor
{
    private static readonly string DetectorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "active_window_detector");

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern bool CGPreflightScreenCaptureAccess();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern bool CGRequestScreenCaptureAccess();

    public bool HasRequiredPermissions()
    {
        LastError = null;

        if (!CGPreflightScreenCaptureAccess())
        {
            LastError = "Enable Screen Recording in System Settings -> Privacy & Security -> Screen Recording.";
            return false;
        }

        return true;
    }

    public bool RequestPermissions()
    {
        LastError = null;

        bool screenAllowed = CGPreflightScreenCaptureAccess() || CGRequestScreenCaptureAccess();
        if (!screenAllowed)
        {
            LastError = "Screen Recording permission is required. Enable it in System Settings -> Privacy & Security -> Screen Recording.";
            return false;
        }

        return true;
    }

    public string GetActiveWindowTitle()
    {
        LastError = null;

        if (File.Exists(DetectorPath))
        {
            var result = RunCommand(DetectorPath, string.Empty);
            if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
            {
                return result.Output.Trim();
            }
        }
        else
        {
            LastError = "Missing active_window_detector in app bundle/output folder.";
        }

        const string script = "tell application \"System Events\" " +
                              "to tell (first application process whose frontmost is true) " +
                              "to get {name, name of front window}";

        var legacyResult = RunCommand("/usr/bin/osascript", $"-e \"{script}\"");

        if (legacyResult.Success && !string.IsNullOrWhiteSpace(legacyResult.Output))
        {
            if (!File.Exists(DetectorPath))
            {
                LastError = "Missing active_window_detector; using AppleScript fallback.";
            }

            return NormalizeWindowTitle(legacyResult.Output);
        }

        return "Desktop / Unknown";
    }

    public string? CaptureScreenshot(string screenshotFolder)
    {
        try
        {
            LastError = null;
            Directory.CreateDirectory(screenshotFolder);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(screenshotFolder, $"screenshot_{timestamp}.png");

            var result = RunCommand("/usr/sbin/screencapture", $"-x \"{filePath}\"");

            if (!result.Success)
            {
                LastError = "Enable Screen Recording in System Settings -> Privacy & Security -> Screen Recording.";
                return null;
            }

            return File.Exists(filePath) ? filePath : null;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to capture screenshot: {ex.Message}";
            return null;
        }
    }

    public string? LastError { get; private set; }

    private static CommandResult RunCommand(string fileName, string arguments)
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
            return new CommandResult(false, string.Empty, "Failed to start process.");
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return new CommandResult(false, string.Empty, string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Trim());
        }

        return new CommandResult(true, string.IsNullOrWhiteSpace(output) ? string.Empty : output.Trim(), string.Empty);
    }

    private static string NormalizeWindowTitle(string raw)
    {
        var parts = raw.Split(",", 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]) &&
            !string.Equals(parts[1], "missing value", StringComparison.OrdinalIgnoreCase))
        {
            return $"{parts[0]} - {parts[1]}";
        }

        return parts.Length > 0 ? parts[0] : "Desktop / Unknown";
    }

    private readonly record struct CommandResult(bool Success, string Output, string Error);
}

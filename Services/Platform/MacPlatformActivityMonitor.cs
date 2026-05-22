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

    static MacPlatformActivityMonitor()
    {
        try
        {
            if (!File.Exists(DetectorPath))
            {
                string swiftCode = """
                import AppKit
                import Cocoa
                import CoreGraphics

                func getActiveWindow() -> String {
                    guard let frontmostApp = NSWorkspace.shared.frontmostApplication else {
                        return "Desktop / Unknown"
                    }
                    
                    let frontmostPid = frontmostApp.processIdentifier
                    let appName = frontmostApp.localizedName ?? "Unknown"
                    
                    let options = CGWindowListOption(arrayLiteral: .excludeDesktopElements, .optionOnScreenOnly)
                    guard let windowListInfo = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: AnyObject]] else {
                        return appName
                    }
                    
                    for info in windowListInfo {
                        guard let pid = info[kCGWindowOwnerPID as String] as? Int, pid == frontmostPid else {
                            continue
                        }
                        
                        guard let layer = info[kCGWindowLayer as String] as? Int, layer == 0 else {
                            continue
                        }
                        
                        if let windowName = info[kCGWindowName as String] as? String, !windowName.isEmpty {
                            return "\(appName) - \(windowName)"
                        }
                    }
                    
                    return appName
                }

                print(getActiveWindow())
                """;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string swiftPath = Path.Combine(baseDir, "active_window.swift");
                File.WriteAllText(swiftPath, swiftCode);

                var compileStartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/swiftc",
                    Arguments = $"\"{swiftPath}\" -o \"{DetectorPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var compileProcess = Process.Start(compileStartInfo);
                compileProcess?.WaitForExit();

                if (File.Exists(swiftPath))
                {
                    File.Delete(swiftPath);
                }

                var chmodStartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{DetectorPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var chmodProcess = Process.Start(chmodStartInfo);
                chmodProcess?.WaitForExit();
            }
        }
        catch
        {
            // Fail silently
        }
    }

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

        // Fallback to basic app name using AppleScript if native tool is missing
        const string script = "tell application \"System Events\" " +
                              "to tell (first application process whose frontmost is true) " +
                              "to get {name, name of front window}";

        var legacyResult = RunCommand("/usr/bin/osascript", $"-e \"{script}\"");

        if (legacyResult.Success && !string.IsNullOrWhiteSpace(legacyResult.Output))
        {
            return NormalizeWindowTitle(legacyResult.Output);
        }

        return "Desktop / Unknown";
    }

    public string CaptureScreenshot(string screenshotFolder)
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
        catch
        {
            LastError = "Failed to capture screenshot.";
            return null;
        }
    }

    public string LastError { get; private set; }

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

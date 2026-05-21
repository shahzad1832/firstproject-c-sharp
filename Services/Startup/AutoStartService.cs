using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace firstProject.Services.Startup;

public sealed class AutoStartService
{
    private const string WindowsRunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string WindowsValueName = "firstProject";
    private const string MacPlistLabel = "com.firstproject.autostart";

    public void Apply(bool enabled)
    {
        if (OperatingSystem.IsWindows())
        {
            ApplyWindows(enabled);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            ApplyMac(enabled);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(WindowsRunKey, writable: true);
        if (key == null)
        {
            return;
        }

        if (!enabled)
        {
            key.DeleteValue(WindowsValueName, false);
            return;
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return;
        }

        key.SetValue(WindowsValueName, processPath);
    }

    [SupportedOSPlatform("macos")]
    private static void ApplyMac(bool enabled)
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        string agentsDir = Path.Combine(home, "Library", "LaunchAgents");
        Directory.CreateDirectory(agentsDir);

        string plistPath = Path.Combine(agentsDir, $"{MacPlistLabel}.plist");

        if (!enabled)
        {
            if (File.Exists(plistPath))
            {
                File.Delete(plistPath);
            }
            return;
        }

        string plist = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
    <!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
    <plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>{MacPlistLabel}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{processPath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>";

        File.WriteAllText(plistPath, plist);
    }
}

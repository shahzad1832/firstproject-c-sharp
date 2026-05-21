using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace firstProject.Services.Platform;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformActivityMonitor : IPlatformActivityMonitor
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    public string GetActiveWindowTitle()
    {
        const int nChars = 256;
        StringBuilder buffer = new(nChars);
        IntPtr handle = GetForegroundWindow();

        if (GetWindowText(handle, buffer, nChars) > 0)
        {
            return buffer.ToString();
        }

        return "Desktop / Unknown";
    }

    public string? CaptureScreenshot(string screenshotFolder)
    {
        try
        {
            int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            Directory.CreateDirectory(screenshotFolder);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(screenshotFolder, $"screenshot_{timestamp}.png");

            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                bitmap.Save(filePath, ImageFormat.Png);
            }

            return filePath;
        }
        catch
        {
            return null;
        }
    }
}
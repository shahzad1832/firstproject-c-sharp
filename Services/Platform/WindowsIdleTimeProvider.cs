using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace firstProject.Services.Platform;

[SupportedOSPlatform("windows")]
public sealed class WindowsIdleTimeProvider : IIdleTimeProvider
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public TimeSpan GetIdleTime()
    {
        var lastInputInfo = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref lastInputInfo))
        {
            return TimeSpan.Zero;
        }

        uint tickCount = (uint)Environment.TickCount;
        uint idleTicks = tickCount - lastInputInfo.dwTime;
        return TimeSpan.FromMilliseconds(idleTicks);
    }
}

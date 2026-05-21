using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace firstProject.Services.Platform;

[SupportedOSPlatform("macos")]
public sealed class MacIdleTimeProvider : IIdleTimeProvider
{
    private const int kCGEventSourceStateCombinedSessionState = 0;
    private const int kCGAnyInputEventType = -1;

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern double CGEventSourceSecondsSinceLastEventType(int sourceStateID, int eventType);

    public TimeSpan GetIdleTime()
    {
        double seconds = CGEventSourceSecondsSinceLastEventType(
            kCGEventSourceStateCombinedSessionState,
            kCGAnyInputEventType);

        if (seconds < 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(seconds);
    }
}

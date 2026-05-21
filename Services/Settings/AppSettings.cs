namespace firstProject.Services.Settings;

public sealed class AppSettings
{
    public string WorkdayStart { get; set; } = "09:00";
    public string WorkdayEnd { get; set; } = "18:00";
    public int ScreenshotMinMinutes { get; set; } = 10;
    public int ScreenshotMaxMinutes { get; set; } = 20;
    public int IdleThresholdSeconds { get; set; } = 100;
    public bool AutoStartEnabled { get; set; } = true;
}

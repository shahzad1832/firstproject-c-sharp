namespace firstProject.Services.Settings;

public sealed class AppSettings
{
    public string UserFullName { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string WorkdayStart { get; set; } = "09:00";
    public string WorkdayEnd { get; set; } = "18:00";
    public int ScreenshotMinMinutes { get; set; } = 10;
    public int ScreenshotMaxMinutes { get; set; } = 20;
    public int IdleThresholdSeconds { get; set; } = 300;
    public bool AutoStartEnabled { get; set; } = true;
    public bool ScreenshotsEnabled { get; set; } = true;
}

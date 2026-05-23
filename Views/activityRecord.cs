using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using Avalonia.Media.Imaging;

namespace firstProject.Models;

public class ActivityRecord
{
    public int Id { get; set; }           // Unique ID
    public string? OrganizationId { get; set; }
    public string? EmployeeId { get; set; }
    public string? DeviceId { get; set; }
    public string RecordType { get; set; } = "Activity";
    public string? AppNames { get; set; }   // App ka naam (e.g. Chrome)
    public DateTime StartTime { get; set; } 
    public DateTime EndTime { get; set; }
    public double DurationSeconds { get; set; }
    public string? ScreenshotPath { get; set; }

    [NotMapped]
    public bool HasScreenshot => !string.IsNullOrWhiteSpace(ScreenshotPath) && File.Exists(ScreenshotPath);

    [NotMapped]
    public Bitmap? ScreenshotPreview => HasScreenshot ? new Bitmap(ScreenshotPath!) : null;

    public string FormattedDuration
    {
        get
        {
            if (DurationSeconds < 60)
                return $"{(int)DurationSeconds}s";
            
            int minutes = (int)(DurationSeconds / 60);
            int seconds = (int)(DurationSeconds % 60);
            return $"{minutes}m {seconds}s";
        }
    }

    [NotMapped]
    public string TimeRangeText => $"{StartTime:hh:mm tt} to {EndTime:hh:mm tt}";

    [NotMapped]
    public string IdleDurationText => $"Idle time {FormattedDuration}";
}

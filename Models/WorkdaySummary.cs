using System;

namespace firstProject.Models;

public sealed class WorkdaySummary
{
    public DateTime Date { get; set; }
    public string ClockInText { get; set; } = "--:--";
    public string ClockOutText { get; set; } = "--:--";
    public string TotalText { get; set; } = "00:00:00";
    public bool IsOpen { get; set; }
    public bool ShowDateHeader { get; set; }

    public string DateText => Date.Date == DateTime.Today
        ? "Today"
        : Date.ToString("ddd, dd MMM");
}

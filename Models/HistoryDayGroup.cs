using System;
using System.Collections.Generic;

namespace firstProject.Models;

public class HistoryDayGroup
{
    public DateTime Date { get; set; }
    public List<ActivityRecord> Records { get; set; } = new();

    public string HeaderText => Date.Date == DateTime.Today
        ? "Today"
        : Date.ToString("dd-MM-yyyy");
}
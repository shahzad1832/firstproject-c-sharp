using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using firstProject.Data;
using firstProject.Models;

namespace firstProject.Services.Data;

public sealed class ActivityRepository
{
    public ActivityRepository()
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();
        EnsureActivitySchema();
    }

    public async Task<List<ActivityRecord>> GetHistoryAsync()
    {
        using var db = new AppDbContext();
        return await db.Activities
            .Where(a => a.AppNames != "Idle")
            .Where(a => a.AppNames == null || !a.AppNames.StartsWith("Screenshot - "))
            .OrderByDescending(a => a.StartTime)
            .ToListAsync<ActivityRecord>();
    }

    public async Task ClearAllAsync()
    {
        using var db = new AppDbContext();
        db.Activities.RemoveRange(db.Activities);
        await db.SaveChangesAsync();
    }

    public async Task<double> GetTodayTotalSecondsAsync()
    {
        DateTime today = DateTime.Today;
        DateTime tomorrow = today.AddDays(1);

        using var db = new AppDbContext();
        return await db.Activities
            .Where(a => a.StartTime >= today && a.StartTime < tomorrow)
            .Where(a => a.AppNames != "Idle")
            .Where(a => a.AppNames == null || !a.AppNames.StartsWith("Screenshot - "))
            .SumAsync(a => (double?)a.DurationSeconds) ?? 0;
    }

    public async Task<double> GetTodayIdleSecondsAsync()
    {
        DateTime today = DateTime.Today;
        DateTime tomorrow = today.AddDays(1);

        using var db = new AppDbContext();
        return await db.Activities
            .Where(a => a.StartTime >= today && a.StartTime < tomorrow)
            .Where(a => a.AppNames == "Idle")
            .SumAsync(a => (double?)a.DurationSeconds) ?? 0;
    }

    public void AddRecord(ActivityRecord record)
    {
        using var db = new AppDbContext();
        db.Activities.Add(record);
        db.SaveChanges();
    }

    private static void EnsureActivitySchema()
    {
        string dbPath = AppDbContext.GetDatabasePath();
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA table_info(Activities);";

        bool screenshotColumnExists = false;
        using (var reader = pragmaCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var columnName = reader.GetString(1);
                if (string.Equals(columnName, "ScreenshotPath", StringComparison.OrdinalIgnoreCase))
                {
                    screenshotColumnExists = true;
                    break;
                }
            }
        }

        if (!screenshotColumnExists)
        {
            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE Activities ADD COLUMN ScreenshotPath TEXT;";
            alterCommand.ExecuteNonQuery();
        }
    }
}

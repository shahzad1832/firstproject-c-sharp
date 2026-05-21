using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        EnsureSyncQueueSchema();
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
        db.SyncQueueItems.RemoveRange(db.SyncQueueItems);
        await db.SaveChangesAsync();
    }

    public async Task<int> GetPendingSyncCountAsync()
    {
        using var db = new AppDbContext();
        return await db.SyncQueueItems.CountAsync(item => item.Status == "Pending");
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

        db.SyncQueueItems.Add(new SyncQueueItem
        {
            ActivityRecordId = record.Id,
            PayloadJson = JsonSerializer.Serialize(new
            {
                record.Id,
                record.OrganizationId,
                record.EmployeeId,
                record.DeviceId,
                record.RecordType,
                record.AppNames,
                record.StartTime,
                record.EndTime,
                record.DurationSeconds,
                record.ScreenshotPath
            })
        });
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

        EnsureColumn(connection, "Activities", "OrganizationId", "TEXT");
        EnsureColumn(connection, "Activities", "EmployeeId", "TEXT");
        EnsureColumn(connection, "Activities", "DeviceId", "TEXT");
        EnsureColumn(connection, "Activities", "RecordType", "TEXT NOT NULL DEFAULT 'Activity'");
    }

    private static void EnsureSyncQueueSchema()
    {
        string dbPath = AppDbContext.GetDatabasePath();
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS SyncQueueItems (
                Id INTEGER NOT NULL CONSTRAINT PK_SyncQueueItems PRIMARY KEY AUTOINCREMENT,
                ActivityRecordId INTEGER NOT NULL,
                EntityType TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                Status TEXT NOT NULL,
                AttemptCount INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                LastAttemptAt TEXT NULL,
                SyncedAt TEXT NULL,
                LastError TEXT NULL
            );
            """;
        createCommand.ExecuteNonQuery();

        using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = "CREATE INDEX IF NOT EXISTS IX_SyncQueueItems_Status ON SyncQueueItems(Status);";
        indexCommand.ExecuteNonQuery();
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string definition)
    {
        using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = $"PRAGMA table_info({tableName});";

        using (var reader = pragmaCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};";
        alterCommand.ExecuteNonQuery();
    }
}

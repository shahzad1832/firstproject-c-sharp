using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using firstProject.Models;

namespace firstProject.Data;

public class AppDbContext : DbContext
{
    public DbSet<ActivityRecord> Activities { get; set; }
    public DbSet<SyncQueueItem> SyncQueueItems { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={GetDatabasePath()}");

    public static string GetDatabasePath()
    {
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataFolder, "firstProject");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, "tracking.db");
    }
}

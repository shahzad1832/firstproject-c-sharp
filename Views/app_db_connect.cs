using Microsoft.EntityFrameworkCore;
using firstProject.Models;

namespace firstProject.Data;

public class AppDbContext : DbContext
{
    // Hamara table
    public DbSet<ActivityRecord> Activities { get; set; }

    // Database file ka rasta (Path)
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=tracking.db");
}
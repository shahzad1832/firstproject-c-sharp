using System;

namespace firstProject.Models;

public sealed class SyncQueueItem
{
    public int Id { get; set; }
    public int ActivityRecordId { get; set; }
    public string EntityType { get; set; } = "ActivityRecord";
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string? LastError { get; set; }
}

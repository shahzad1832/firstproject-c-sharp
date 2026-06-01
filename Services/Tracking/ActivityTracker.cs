using System;
using System.Timers;
using firstProject.Models;
using firstProject.Services.Data;
using firstProject.Services.Platform;
using firstProject.Services.Settings;
namespace firstProject.Services.Tracking;

public sealed class ActivityTracker : IDisposable
{
    private readonly IPlatformActivityMonitor _platformActivityMonitor;
    private readonly IIdleTimeProvider _idleTimeProvider;
    private readonly ActivityRepository _repository;
    private readonly AppSettings _settings;
    private readonly string _screenshotFolder;
    private readonly Timer _timer;
    private readonly Random _random = new();
    private readonly object _stateLock = new();

    private string _lastWindowTitle = string.Empty;
    private DateTime _startTime;
    private DateTime _nextScreenshotCapture;
    private bool _isTracking;
    private bool _isIdle;
    private bool _isWithinWorkHours;
    private bool _isSuspended;

    public ActivityTracker(
        IPlatformActivityMonitor platformActivityMonitor,
        IIdleTimeProvider idleTimeProvider,
        ActivityRepository repository,
        AppSettings settings,
        string screenshotFolder)
    {
        _platformActivityMonitor = platformActivityMonitor;
        _idleTimeProvider = idleTimeProvider;
        _repository = repository;
        _settings = settings;
        _screenshotFolder = screenshotFolder;

        _timer = new Timer(1000);
        _timer.Elapsed += OnTimerElapsed;
    }

    public event Action<string> CurrentWindowChanged;
    public event Action<string> StatusChanged;
    public event Action<bool> TrackingStateChanged;

    public bool IsTracking => _isTracking;

    public ActivitySnapshot GetLiveSnapshot()
    {
        lock (_stateLock)
        {
            string recordType = string.Empty;
            if (!string.IsNullOrWhiteSpace(_lastWindowTitle))
            {
                recordType = _isIdle ? "Idle" : "Activity";
            }

            return new ActivitySnapshot(
                _isTracking,
                _isIdle,
                _isWithinWorkHours,
                _startTime,
                _lastWindowTitle,
                recordType);
        }
    }

    public void Start()
    {
        if (_isTracking)
        {
            return;
        }

        DateTime now = DateTime.Now;
        if (!IsWithinWorkHours(now))
        {
            StatusChanged?.Invoke("Outside working hours");
            TrackingStateChanged?.Invoke(false);
            return;
        }

        if (!_platformActivityMonitor.HasRequiredPermissions() &&
            !_platformActivityMonitor.RequestPermissions())
        {
            StatusChanged?.Invoke(_platformActivityMonitor.LastError ?? "Required permissions are missing.");
            TrackingStateChanged?.Invoke(false);
            return;
        }

        lock (_stateLock)
        {
            _isTracking = true;
            _lastWindowTitle = string.Empty;
            _startTime = now;
            _nextScreenshotCapture = CalculateNextScreenshotTime();
            _isIdle = false;
            _isWithinWorkHours = IsWithinWorkHours(now);
            _isSuspended = false;
        }
        _timer.Start();
        TrackingStateChanged?.Invoke(true);
    }

    public void Stop()
    {
        if (!_isTracking)
        {
            return;
        }

        _timer.Stop();
        SavePendingRecord();
        lock (_stateLock)
        {
            _lastWindowTitle = string.Empty;
            _isIdle = false;
            _isTracking = false;
            _isWithinWorkHours = false;
            _isSuspended = false;
        }
        TrackingStateChanged?.Invoke(false);
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        bool isTracking;
        bool isSuspended;
        lock (_stateLock)
        {
            isTracking = _isTracking;
            isSuspended = _isSuspended;
        }

        if (!isTracking && !isSuspended)
        {
            return;
        }

        DateTime now = DateTime.Now;
        bool withinWorkHours = IsWithinWorkHours(now);
        lock (_stateLock)
        {
            _isWithinWorkHours = withinWorkHours;
        }

        if (!withinWorkHours)
        {
            Stop();
            CurrentWindowChanged?.Invoke("Outside working hours");
            StatusChanged?.Invoke(null);
            return;
        }

        TimeSpan idleTime = _idleTimeProvider.GetIdleTime();
        int suspendSeconds = Math.Max(_settings.SleepSuspendSeconds, _settings.IdleThresholdSeconds + 30);
        if (!isSuspended && idleTime.TotalSeconds >= suspendSeconds)
        {
            SavePendingRecord();
            lock (_stateLock)
            {
                _lastWindowTitle = string.Empty;
                _isIdle = false;
                _isTracking = false;
                _isSuspended = true;
                _startTime = now;
            }
            TrackingStateChanged?.Invoke(false);
            CurrentWindowChanged?.Invoke("Suspended");
            StatusChanged?.Invoke(null);
            return;
        }

        if (isSuspended)
        {
            if (idleTime.TotalSeconds >= _settings.IdleThresholdSeconds)
            {
                StatusChanged?.Invoke(null);
                return;
            }

            lock (_stateLock)
            {
                _isSuspended = false;
                _isTracking = true;
                _isIdle = false;
                _lastWindowTitle = string.Empty;
                _startTime = now;
                _nextScreenshotCapture = CalculateNextScreenshotTime();
            }
            TrackingStateChanged?.Invoke(true);
            isTracking = true;
        }

        bool isIdle = idleTime.TotalSeconds >= _settings.IdleThresholdSeconds;
        bool wasIdle;

        lock (_stateLock)
        {
            wasIdle = _isIdle;
        }

        if (isIdle)
        {
            if (!wasIdle)
            {
                DateTime idleStartTime = now - idleTime;
                SavePendingRecord(idleStartTime);
                lock (_stateLock)
                {
                    _lastWindowTitle = "Idle";
                    _startTime = idleStartTime;
                    _isIdle = true;
                }
                CurrentWindowChanged?.Invoke("Idle");
            }

            StatusChanged?.Invoke(null);
            return;
        }

        if (wasIdle)
        {
            SavePendingRecord();
            lock (_stateLock)
            {
                _lastWindowTitle = string.Empty;
                _isIdle = false;
                _startTime = now;
                _nextScreenshotCapture = CalculateNextScreenshotTime();
            }
        }

        string currentWindow = _platformActivityMonitor.GetActiveWindowTitle();

        string lastError = _platformActivityMonitor.LastError;
        StatusChanged?.Invoke(string.IsNullOrWhiteSpace(lastError) ? null : lastError);

        string lastWindowTitle;
        lock (_stateLock)
        {
            lastWindowTitle = _lastWindowTitle;
        }

        if (!string.Equals(currentWindow, lastWindowTitle, StringComparison.Ordinal))
        {
            SavePendingRecord();
            lock (_stateLock)
            {
                _lastWindowTitle = currentWindow;
                _startTime = now;
            }
        }

        CurrentWindowChanged?.Invoke(currentWindow);

        DateTime nextScreenshotCapture;
        lock (_stateLock)
        {
            nextScreenshotCapture = _nextScreenshotCapture;
        }

        if (_settings.ScreenshotsEnabled && now >= nextScreenshotCapture)
        {
            string screenshotPath = _platformActivityMonitor.CaptureScreenshot(_screenshotFolder);
            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {
                _repository.AddRecord(CreateRecord(
                    $"Screenshot - {currentWindow}",
                    now,
                    now,
                    0,
                    "Screenshot",
                    screenshotPath));
            }

            if (!string.IsNullOrWhiteSpace(_platformActivityMonitor.LastError))
            {
                StatusChanged?.Invoke(_platformActivityMonitor.LastError);
            }

            lock (_stateLock)
            {
                _nextScreenshotCapture = CalculateNextScreenshotTime();
            }
        }
        else if (!_settings.ScreenshotsEnabled && now >= nextScreenshotCapture)
        {
            lock (_stateLock)
            {
                _nextScreenshotCapture = CalculateNextScreenshotTime();
            }
        }
    }

    private ActivityRecord CreateRecord(
        string appName,
        DateTime startTime,
        DateTime endTime,
        double durationSeconds,
        string recordType,
        string screenshotPath = null)
    {
        return new ActivityRecord
                {
            OrganizationId = _settings.OrganizationId,
            EmployeeId = _settings.EmployeeId,
            DeviceId = _settings.DeviceId,
            RecordType = recordType,
            AppNames = appName,
            StartTime = startTime,
            EndTime = endTime,
            DurationSeconds = durationSeconds,
            ScreenshotPath = screenshotPath
        };
    }

    private void SavePendingRecord(DateTime? endTimeOverride = null)
    {
        string lastWindowTitle;
        DateTime startTime;

        lock (_stateLock)
        {
            lastWindowTitle = _lastWindowTitle;
            startTime = _startTime;
        }

        if (string.IsNullOrWhiteSpace(lastWindowTitle))
        {
            return;
        }

        DateTime endTime = endTimeOverride ?? DateTime.Now;
        string recordType = string.Equals(lastWindowTitle, "Idle", StringComparison.Ordinal)
            ? "Idle"
            : "Activity";

        if (endTime <= startTime)
        {
            return;
        }

        if (string.Equals(recordType, "Idle", StringComparison.Ordinal))
        {
            double idleSeconds = (endTime - startTime).TotalSeconds;
            if (idleSeconds < _settings.IdleThresholdSeconds)
            {
                return;
            }
        }

        _repository.AddRecord(CreateRecord(
            lastWindowTitle,
            startTime,
            endTime,
            (endTime - startTime).TotalSeconds,
            recordType));
    }

    private DateTime CalculateNextScreenshotTime()
    {
        int min = Math.Max(1, _settings.ScreenshotMinMinutes);
        int max = Math.Max(min, _settings.ScreenshotMaxMinutes);
        int randomMinutes = _random.Next(min, max + 1);
        return DateTime.Now.AddMinutes(randomMinutes);
    }

    private bool IsWithinWorkHours(DateTime now)
    {
        if (!TimeSpan.TryParse(_settings.WorkdayStart, out var start) ||
            !TimeSpan.TryParse(_settings.WorkdayEnd, out var end))
        {
            return true;
        }

        TimeSpan current = now.TimeOfDay;
        if (start <= end)
        {
            return current >= start && current <= end;
        }

        return current >= start || current <= end;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}

public readonly struct ActivitySnapshot
{
    public ActivitySnapshot(
        bool isTracking,
        bool isIdle,
        bool isWithinWorkHours,
        DateTime segmentStart,
        string currentWindowTitle,
        string recordType)
    {
        IsTracking = isTracking;
        IsIdle = isIdle;
        IsWithinWorkHours = isWithinWorkHours;
        SegmentStart = segmentStart;
        CurrentWindowTitle = currentWindowTitle ?? string.Empty;
        RecordType = recordType ?? string.Empty;
    }

    public bool IsTracking { get; }
    public bool IsIdle { get; }
    public bool IsWithinWorkHours { get; }
    public DateTime SegmentStart { get; }
    public string CurrentWindowTitle { get; }
    public string RecordType { get; }
}

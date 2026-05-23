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

    private string _lastWindowTitle = string.Empty;
    private DateTime _startTime;
    private DateTime _nextScreenshotCapture;
    private bool _isTracking;
    private bool _isIdle;
    private DateTime? _breakStartTime;

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

    public void Start()
    {
        if (_isTracking)
        {
            return;
        }

        if (!_platformActivityMonitor.HasRequiredPermissions() &&
            !_platformActivityMonitor.RequestPermissions())
        {
            StatusChanged?.Invoke(_platformActivityMonitor.LastError ?? "Required permissions are missing.");
            TrackingStateChanged?.Invoke(false);
            return;
        }

        if (_breakStartTime.HasValue)
        {
            DateTime breakEndTime = DateTime.Now;
            double breakDuration = (breakEndTime - _breakStartTime.Value).TotalSeconds;
            if (breakDuration >= _settings.IdleThresholdSeconds)
            {
                _repository.AddRecord(CreateRecord(
                    "Idle",
                    _breakStartTime.Value,
                    breakEndTime,
                    breakDuration,
                    "Idle"));
            }
            _breakStartTime = null;
        }

        _isTracking = true;
        _lastWindowTitle = string.Empty;
        _startTime = DateTime.Now;
        _nextScreenshotCapture = CalculateNextScreenshotTime();
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
        _lastWindowTitle = string.Empty;
        _isIdle = false;
        _isTracking = false;
        _breakStartTime = DateTime.Now;
        TrackingStateChanged?.Invoke(false);
    }

    private void OnTimerElapsed(object sender, ElapsedEventArgs e)
    {
        if (!_isTracking)
        {
            return;
        }

        if (!IsWithinWorkHours(DateTime.Now))
        {
            SavePendingRecord();
            _lastWindowTitle = string.Empty;
            CurrentWindowChanged?.Invoke("Outside working hours");
            StatusChanged?.Invoke(null);
            return;
        }

        TimeSpan idleTime = _idleTimeProvider.GetIdleTime();
        bool isIdle = idleTime.TotalSeconds >= _settings.IdleThresholdSeconds;

        if (isIdle)
        {
            if (!_isIdle)
            {
                DateTime idleStartTime = DateTime.Now - idleTime;
                SavePendingRecord(idleStartTime);
                _lastWindowTitle = "Idle";
                _startTime = idleStartTime;
                _isIdle = true;
                CurrentWindowChanged?.Invoke("Idle");
            }

            StatusChanged?.Invoke(null);
            return;
        }

        if (_isIdle)
        {
            SavePendingRecord();
            _lastWindowTitle = string.Empty;
            _isIdle = false;
            _startTime = DateTime.Now;
            _nextScreenshotCapture = CalculateNextScreenshotTime();
        }

        string currentWindow = _platformActivityMonitor.GetActiveWindowTitle();

        string lastError = _platformActivityMonitor.LastError;
        StatusChanged?.Invoke(string.IsNullOrWhiteSpace(lastError) ? null : lastError);

        if (!string.Equals(currentWindow, _lastWindowTitle, StringComparison.Ordinal))
        {
            SavePendingRecord();
            _lastWindowTitle = currentWindow;
            _startTime = DateTime.Now;
        }

        CurrentWindowChanged?.Invoke(currentWindow);

        if (_settings.ScreenshotsEnabled && DateTime.Now >= _nextScreenshotCapture)
        {
            string screenshotPath = _platformActivityMonitor.CaptureScreenshot(_screenshotFolder);
            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {
                _repository.AddRecord(CreateRecord(
                    $"Screenshot - {currentWindow}",
                    DateTime.Now,
                    DateTime.Now,
                    0,
                    "Screenshot",
                    screenshotPath));
            }

            if (!string.IsNullOrWhiteSpace(_platformActivityMonitor.LastError))
            {
                StatusChanged?.Invoke(_platformActivityMonitor.LastError);
            }

            _nextScreenshotCapture = CalculateNextScreenshotTime();
        }
        else if (!_settings.ScreenshotsEnabled && DateTime.Now >= _nextScreenshotCapture)
        {
            _nextScreenshotCapture = CalculateNextScreenshotTime();
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
        if (string.IsNullOrWhiteSpace(_lastWindowTitle))
        {
            return;
        }

        DateTime endTime = endTimeOverride ?? DateTime.Now;
        string recordType = string.Equals(_lastWindowTitle, "Idle", StringComparison.Ordinal)
            ? "Idle"
            : "Activity";

        if (endTime <= _startTime)
        {
            return;
        }

        if (string.Equals(recordType, "Idle", StringComparison.Ordinal))
        {
            double idleSeconds = (endTime - _startTime).TotalSeconds;
            if (idleSeconds < _settings.IdleThresholdSeconds)
            {
                return;
            }
        }

        _repository.AddRecord(CreateRecord(
            _lastWindowTitle,
            _startTime,
            endTime,
            (endTime - _startTime).TotalSeconds,
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

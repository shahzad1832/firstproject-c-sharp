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

    public event Action<string>? CurrentWindowChanged;
    public event Action<string?>? StatusChanged;
    public event Action<bool>? TrackingStateChanged;

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
        TrackingStateChanged?.Invoke(false);
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
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
                SavePendingRecord();
                _lastWindowTitle = "Idle";
                _startTime = DateTime.Now;
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

        string? lastError = _platformActivityMonitor.LastError;
        StatusChanged?.Invoke(string.IsNullOrWhiteSpace(lastError) ? null : lastError);

        if (!string.Equals(currentWindow, _lastWindowTitle, StringComparison.Ordinal))
        {
            SavePendingRecord();
            _lastWindowTitle = currentWindow;
            _startTime = DateTime.Now;
        }

        CurrentWindowChanged?.Invoke(currentWindow);

        if (DateTime.Now >= _nextScreenshotCapture)
        {
            string? screenshotPath = _platformActivityMonitor.CaptureScreenshot(_screenshotFolder);
            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {
                _repository.AddRecord(new ActivityRecord
                {
                    AppNames = $"Screenshot - {currentWindow}",
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now,
                    DurationSeconds = 0,
                    ScreenshotPath = screenshotPath
                });
            }

            if (!string.IsNullOrWhiteSpace(_platformActivityMonitor.LastError))
            {
                StatusChanged?.Invoke(_platformActivityMonitor.LastError);
            }

            _nextScreenshotCapture = CalculateNextScreenshotTime();
        }
    }

    private void SavePendingRecord()
    {
        if (string.IsNullOrWhiteSpace(_lastWindowTitle))
        {
            return;
        }

        DateTime endTime = DateTime.Now;
        _repository.AddRecord(new ActivityRecord
        {
            AppNames = _lastWindowTitle,
            StartTime = _startTime,
            EndTime = endTime,
            DurationSeconds = (endTime - _startTime).TotalSeconds
        });
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

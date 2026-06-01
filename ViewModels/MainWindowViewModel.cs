using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using firstProject.Models;
using firstProject.Services.Data;
using firstProject.Services.Platform;
using firstProject.Services.Settings;
using firstProject.Services.Startup;
using firstProject.Services.Tracking;

namespace firstProject.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ActivityRepository _repository;
    private readonly ActivityTracker _tracker;
    private readonly SettingsService _settingsService;
    private readonly AutoStartService _autoStartService;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _summaryTimer;
    private readonly DispatcherTimer _liveTimer;
    private double _todayBaseProductiveSeconds;
    private double _todayBaseIdleSeconds;

    public ObservableCollection<HistoryDayGroup> HistoryItems { get; } = new();
    public ObservableCollection<ActivityRecord> TodayScreenshots { get; } = new();
    public ObservableCollection<ActivityRecord> TodayIdleRecords { get; } = new();

    private string _screenshotCountText = "No screenshots today";
    public string ScreenshotCountText
    {
        get => _screenshotCountText;
        private set => SetProperty(ref _screenshotCountText, value);
    }

    private string _currentWindowTitle = "Not tracking";
    private string _statusMessage = string.Empty;
    private string _todayTotalTime = "0m 0s";
    private string _attendanceClockText = "00:00:00s";
    private string _idleTotalTime = "0m 0s";
    private string _pendingSyncCount = "0";
    private string _settingsStatusMessage = string.Empty;
    private string _todayDateText = DateTime.Today.ToString("MMMM dd, yyyy");
    private string _lastSyncText = "Not synced yet";
    private bool _isTracking;
    private string _trackingButtonText = "Start Tracking";
    private string _userFullName = string.Empty;
    private string _organizationId = string.Empty;
    private string _employeeId = string.Empty;
    private string _deviceId = string.Empty;
    private string _apiBaseUrl = string.Empty;
    private string _workdayStart = "09:00";
    private string _workdayEnd = "18:00";
    private int _idleThresholdSeconds = 120;
    private int _screenshotMinMinutes = 10;
    private int _screenshotMaxMinutes = 20;
    private bool _autoStartEnabled = true;
    private bool _screenshotsEnabled = true;

    public string CurrentWindowTitle
    {
        get => _currentWindowTitle;
        private set => SetProperty(ref _currentWindowTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string TodayTotalTime
    {
        get => _todayTotalTime;
        private set => SetProperty(ref _todayTotalTime, value);
    }

    public string AttendanceClockText
    {
        get => _attendanceClockText;
        private set => SetProperty(ref _attendanceClockText, value);
    }

    public string IdleTotalTime
    {
        get => _idleTotalTime;
        private set => SetProperty(ref _idleTotalTime, value);
    }

    public string PendingSyncCount
    {
        get => _pendingSyncCount;
        private set => SetProperty(ref _pendingSyncCount, value);
    }

    public string SettingsStatusMessage
    {
        get => _settingsStatusMessage;
        private set => SetProperty(ref _settingsStatusMessage, value);
    }

    public string TodayDateText
    {
        get => _todayDateText;
        private set => SetProperty(ref _todayDateText, value);
    }

    public string LastSyncText
    {
        get => _lastSyncText;
        private set => SetProperty(ref _lastSyncText, value);
    }

    public bool IsTracking
    {
        get => _isTracking;
        private set
        {
            if (SetProperty(ref _isTracking, value))
            {
                TrackingButtonText = value ? "Stop Tracking" : "Start Tracking";
            }
        }
    }

    public string TrackingButtonText
    {
        get => _trackingButtonText;
        private set => SetProperty(ref _trackingButtonText, value);
    }

    public string UserFullName
    {
        get => _userFullName;
        set
        {
            if (SetProperty(ref _userFullName, value))
            {
                OnPropertyChanged(nameof(UserDisplayName));
            }
        }
    }

    public string UserDisplayName => string.IsNullOrWhiteSpace(UserFullName) ? "user" : UserFullName;

    public string OrganizationId
    {
        get => _organizationId;
        set => SetProperty(ref _organizationId, value);
    }

    public string EmployeeId
    {
        get => _employeeId;
        set => SetProperty(ref _employeeId, value);
    }

    public string DeviceId
    {
        get => _deviceId;
        private set => SetProperty(ref _deviceId, value);
    }

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => SetProperty(ref _apiBaseUrl, value);
    }

    public string WorkdayStart
    {
        get => _workdayStart;
        set => SetProperty(ref _workdayStart, value);
    }

    public string WorkdayEnd
    {
        get => _workdayEnd;
        set => SetProperty(ref _workdayEnd, value);
    }

    public int IdleThresholdSeconds
    {
        get => _idleThresholdSeconds;
        set => SetProperty(ref _idleThresholdSeconds, value);
    }

    public int ScreenshotMinMinutes
    {
        get => _screenshotMinMinutes;
        set => SetProperty(ref _screenshotMinMinutes, value);
    }

    public int ScreenshotMaxMinutes
    {
        get => _screenshotMaxMinutes;
        set => SetProperty(ref _screenshotMaxMinutes, value);
    }

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set => SetProperty(ref _autoStartEnabled, value);
    }

    public bool ScreenshotsEnabled
    {
        get => _screenshotsEnabled;
        set => SetProperty(ref _screenshotsEnabled, value);
    }

    public IRelayCommand ToggleTrackingCommand { get; }
    public IRelayCommand ClockInCommand { get; }
    public IRelayCommand TakeBreakCommand { get; }
    public IRelayCommand AddNewTaskCommand { get; }
    public IRelayCommand SaveSettingsCommand { get; }
    public IRelayCommand OpenDashboardCommand { get; }
    public IAsyncRelayCommand ShowHistoryCommand { get; }
    public IAsyncRelayCommand ClearHistoryCommand { get; }
    public IAsyncRelayCommand RefreshScreenshotsCommand { get; }

    public MainWindowViewModel()
    {
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        LoadSettingsToProperties();

        _autoStartService = new AutoStartService();
        _autoStartService.Apply(_settings.AutoStartEnabled);

        _repository = new ActivityRepository();

        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataFolder, "firstProject");
        string screenshotFolder = Path.Combine(appFolder, "Screenshots");
        Directory.CreateDirectory(screenshotFolder);

        _tracker = new ActivityTracker(
            PlatformActivityMonitorFactory.Create(),
            PlatformActivityMonitorFactory.CreateIdleTimeProvider(),
            _repository,
            _settings,
            screenshotFolder);

        _tracker.CurrentWindowChanged += title =>
            Dispatcher.UIThread.Post(() => CurrentWindowTitle = title);

        _tracker.StatusChanged += message =>
            Dispatcher.UIThread.Post(() => StatusMessage = message ?? string.Empty);

        _tracker.TrackingStateChanged += tracking =>
            Dispatcher.UIThread.Post(() => IsTracking = tracking);

        ToggleTrackingCommand = new RelayCommand(ToggleTracking);
        ClockInCommand = new RelayCommand(StartTracking);
        TakeBreakCommand = new RelayCommand(StopTracking);
        AddNewTaskCommand = new RelayCommand(AddNewTask);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        OpenDashboardCommand = new RelayCommand(OpenDashboard);
        ShowHistoryCommand = new AsyncRelayCommand(RefreshHistoryAsync);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync);
        RefreshScreenshotsCommand = new AsyncRelayCommand(RefreshScreenshotsAsync);

        _summaryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _summaryTimer.Tick += async (_, _) => await RefreshHistoryAsync();
        _summaryTimer.Start();

        _liveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _liveTimer.Tick += (_, _) => UpdateLiveTotals();
        _liveTimer.Start();

        _tracker.Start();
        _ = RefreshHistoryAsync();
    }

    private void ToggleTracking()
    {
        if (_tracker.IsTracking)
        {
            StopTracking();
        }
        else
        {
            StartTracking();
        }
    }

    private void StartTracking()
    {
        _tracker.Start();
        UpdateLiveTotals();
    }

    private void StopTracking()
    {
        _tracker.Stop();
        _ = UpdateBaseTotalsAsync();
    }

    private void AddNewTask()
    {
        SaveSettings();
        SettingsStatusMessage = "Task setup saved locally";
    }

    private void LoadSettingsToProperties()
    {
        UserFullName = _settings.UserFullName;
        OrganizationId = _settings.OrganizationId;
        EmployeeId = _settings.EmployeeId;
        DeviceId = _settings.DeviceId;
        ApiBaseUrl = _settings.ApiBaseUrl;
        WorkdayStart = _settings.WorkdayStart;
        WorkdayEnd = _settings.WorkdayEnd;
        IdleThresholdSeconds = _settings.IdleThresholdSeconds;
        ScreenshotMinMinutes = _settings.ScreenshotMinMinutes;
        ScreenshotMaxMinutes = _settings.ScreenshotMaxMinutes;
        AutoStartEnabled = _settings.AutoStartEnabled;
        ScreenshotsEnabled = _settings.ScreenshotsEnabled;
    }

    private void SaveSettings()
    {
        _settings.UserFullName = UserFullName.Trim();
        _settings.OrganizationId = OrganizationId.Trim();
        _settings.EmployeeId = EmployeeId.Trim();
        _settings.ApiBaseUrl = ApiBaseUrl.Trim();
        _settings.WorkdayStart = WorkdayStart.Trim();
        _settings.WorkdayEnd = WorkdayEnd.Trim();
        _settings.IdleThresholdSeconds = Math.Max(10, IdleThresholdSeconds);
        _settings.ScreenshotMinMinutes = Math.Max(1, ScreenshotMinMinutes);
        _settings.ScreenshotMaxMinutes = Math.Max(_settings.ScreenshotMinMinutes, ScreenshotMaxMinutes);
        _settings.AutoStartEnabled = AutoStartEnabled;
        _settings.ScreenshotsEnabled = ScreenshotsEnabled;

        IdleThresholdSeconds = _settings.IdleThresholdSeconds;
        ScreenshotMinMinutes = _settings.ScreenshotMinMinutes;
        ScreenshotMaxMinutes = _settings.ScreenshotMaxMinutes;

        _settingsService.Save(_settings);
        _autoStartService.Apply(_settings.AutoStartEnabled);
        SettingsStatusMessage = "Agent settings saved";
    }

    private void OpenDashboard()
    {
        SettingsStatusMessage = string.IsNullOrWhiteSpace(ApiBaseUrl)
            ? "Set API Base URL to open dashboard"
            : $"Dashboard: {ApiBaseUrl}";
    }

    private async Task RefreshHistoryAsync()
    {
        var history = await _repository.GetHistoryAsync();

        HistoryItems.Clear();
        foreach (var dayGroup in history
                     .GroupBy(record => record.StartTime.Date)
                     .OrderByDescending(group => group.Key))
        {
            HistoryItems.Add(new HistoryDayGroup
            {
                Date = dayGroup.Key,
                Records = dayGroup.OrderByDescending(record => record.StartTime).ToList()
            });
        }

        await UpdateBaseTotalsAsync();
        await RefreshIdleRecordsAsync();
        await RefreshScreenshotsAsync();
    }

    private async Task RefreshIdleRecordsAsync()
    {
        var idleRecords = await _repository.GetTodayIdleRecordsAsync();

        TodayIdleRecords.Clear();
        foreach (var record in idleRecords)
        {
            TodayIdleRecords.Add(record);
        }
    }

    private async Task ClearHistoryAsync()
    {
        await _repository.ClearAllAsync();
        HistoryItems.Clear();
        await UpdateBaseTotalsAsync();
    }

    private async Task UpdateBaseTotalsAsync()
    {
        double productiveSeconds = await _repository.GetTodayTotalSecondsAsync();
        double idleSeconds = await _repository.GetTodayIdleSecondsAsync();
        int pendingSync = await _repository.GetPendingSyncCountAsync();

        _todayBaseProductiveSeconds = productiveSeconds;
        _todayBaseIdleSeconds = idleSeconds;

        Dispatcher.UIThread.Post(() =>
        {
            PendingSyncCount = pendingSync.ToString();
            LastSyncText = pendingSync == 0 ? "All records synced" : $"Pending upload: {pendingSync}";
            UpdateLiveTotals();
        });
    }

    private void UpdateLiveTotals()
    {
        ActivitySnapshot snapshot = _tracker.GetLiveSnapshot();
        DateTime now = DateTime.Now;

        double productiveSeconds = _todayBaseProductiveSeconds;
        double idleSeconds = _todayBaseIdleSeconds;

        if (snapshot.IsTracking && snapshot.IsWithinWorkHours)
        {
            DateTime segmentStart = snapshot.SegmentStart < DateTime.Today
                ? DateTime.Today
                : snapshot.SegmentStart;

            double liveSeconds = Math.Max(0, (now - segmentStart).TotalSeconds);

            if (snapshot.IsIdle)
            {
                idleSeconds += liveSeconds;
            }
            else if (!string.IsNullOrWhiteSpace(snapshot.CurrentWindowTitle))
            {
                productiveSeconds += liveSeconds;
            }
        }

        TodayTotalTime = FormatDuration(productiveSeconds);
        AttendanceClockText = FormatClock(productiveSeconds);
        IdleTotalTime = FormatDuration(idleSeconds);
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds < 60)
        {
            return $"{(int)seconds}s";
        }

        int totalSeconds = (int)Math.Round(seconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int remainingSeconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours}h {minutes}m";
        }

        return $"{minutes}m {remainingSeconds}s";
    }

    private static string FormatClock(double seconds)
    {
        int totalSeconds = Math.Max(0, (int)Math.Round(seconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{hours:00}:{minutes:00}:{remainingSeconds:00} h";
    }

    private async Task RefreshScreenshotsAsync()
    {
        var screenshots = await _repository.GetTodayScreenshotsAsync();

        TodayScreenshots.Clear();
        foreach (var s in screenshots)
        {
            TodayScreenshots.Add(s);
        }

        ScreenshotCountText = TodayScreenshots.Count == 0
            ? "No screenshots today"
            : $"{TodayScreenshots.Count} screenshot{(TodayScreenshots.Count == 1 ? "" : "s")} taken today";
    }

    public void Dispose()
    {
        _summaryTimer.Stop();
        _liveTimer.Stop();
        _tracker.Dispose();
    }
}

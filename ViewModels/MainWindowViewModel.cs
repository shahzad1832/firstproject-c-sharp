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
    private readonly DispatcherTimer _summaryTimer;

    public ObservableCollection<HistoryDayGroup> HistoryItems { get; } = new();

    private string _currentWindowTitle = "Not tracking";
    private string _statusMessage = string.Empty;
    private string _todayTotalTime = "0m 0s";
    private string _idleTotalTime = "0m 0s";
    private bool _isTracking;
    private string _trackingButtonText = "Start Tracking";

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

    public string IdleTotalTime
    {
        get => _idleTotalTime;
        private set => SetProperty(ref _idleTotalTime, value);
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

    public IRelayCommand ToggleTrackingCommand { get; }
    public IAsyncRelayCommand ShowHistoryCommand { get; }
    public IAsyncRelayCommand ClearHistoryCommand { get; }

    public MainWindowViewModel()
    {
        var settingsService = new SettingsService();
        var settings = settingsService.Load();

        var autoStartService = new AutoStartService();
        autoStartService.Apply(settings.AutoStartEnabled);

        _repository = new ActivityRepository();

        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataFolder, "firstProject");
        string screenshotFolder = Path.Combine(appFolder, "Screenshots");
        Directory.CreateDirectory(screenshotFolder);

        _tracker = new ActivityTracker(
            PlatformActivityMonitorFactory.Create(),
            PlatformActivityMonitorFactory.CreateIdleTimeProvider(),
            _repository,
            settings,
            screenshotFolder);

        _tracker.CurrentWindowChanged += title =>
            Dispatcher.UIThread.Post(() => CurrentWindowTitle = title);

        _tracker.StatusChanged += message =>
            Dispatcher.UIThread.Post(() => StatusMessage = message ?? string.Empty);

        _tracker.TrackingStateChanged += tracking =>
            Dispatcher.UIThread.Post(() => IsTracking = tracking);

        ToggleTrackingCommand = new RelayCommand(ToggleTracking);
        ShowHistoryCommand = new AsyncRelayCommand(RefreshHistoryAsync);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync);

        _summaryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _summaryTimer.Tick += async (_, _) => await UpdateTodayTotalTimeAsync();
        _summaryTimer.Start();

        _tracker.Start();
        _ = RefreshHistoryAsync();
    }

    private void ToggleTracking()
    {
        if (_tracker.IsTracking)
        {
            _tracker.Stop();
        }
        else
        {
            _tracker.Start();
        }
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

        await UpdateTodayTotalTimeAsync();
    }

    private async Task ClearHistoryAsync()
    {
        await _repository.ClearAllAsync();
        HistoryItems.Clear();
        await UpdateTodayTotalTimeAsync();
    }

    private async Task UpdateTodayTotalTimeAsync()
    {
        double seconds = await _repository.GetTodayTotalSecondsAsync();
        TodayTotalTime = FormatDuration(seconds);

        double idleSeconds = await _repository.GetTodayIdleSecondsAsync();
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

    public void Dispose()
    {
        _summaryTimer.Stop();
        _tracker.Dispose();
    }
}

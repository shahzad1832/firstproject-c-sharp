using Avalonia;
using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Controls;
using Avalonia.Platform;
using firstProject.ViewModels;
using firstProject.Views;

namespace firstProject;

public partial class App : Application
{
    internal static bool IsExitRequested { get; private set; }
    private TrayIcon _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            desktop.MainWindow.ShowInTaskbar = false;
            desktop.MainWindow.Hide();

            InitializeTrayIcon();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeTrayIcon()
    {
        if (_trayIcon != null)
        {
            return;
        }

        var iconUri = new Uri("avares://firstProject/Assets/avalonia-logo.ico");
        var icon = new WindowIcon(AssetLoader.Open(iconUri));

        _trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "Assing In"
        };

        var menu = new NativeMenu();
        var showItem = new NativeMenuItem("Show");
        var startItem = new NativeMenuItem("Start Tracking");
        var stopItem = new NativeMenuItem("Stop Tracking");
        var exitItem = new NativeMenuItem("Exit");

        showItem.Click += TrayShow_Click;
        startItem.Click += TrayStart_Click;
        stopItem.Click += TrayStop_Click;
        exitItem.Click += TrayExit_Click;

        menu.Add(showItem);
        menu.Add(startItem);
        menu.Add(stopItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exitItem);

        _trayIcon.Menu = menu;
    }

    private static MainWindowViewModel GetViewModel()
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.DataContext as MainWindowViewModel;
        }

        return null;
    }

    public void TrayShow_Click(object sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Show();
            desktop.MainWindow?.Activate();
        }
    }

    public void TrayStart_Click(object sender, EventArgs e)
    {
        var viewModel = GetViewModel();
        if (viewModel != null && !viewModel.IsTracking)
        {
            viewModel.ToggleTrackingCommand.Execute(null);
        }
    }

    public void TrayStop_Click(object sender, EventArgs e)
    {
        var viewModel = GetViewModel();
        if (viewModel != null && viewModel.IsTracking)
        {
            viewModel.ToggleTrackingCommand.Execute(null);
        }
    }

    public void TrayExit_Click(object sender, EventArgs e)
    {
        IsExitRequested = true;
        _trayIcon?.Dispose();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using firstProject.Data;
using firstProject.Models;
using firstProject.ViewModels;

namespace firstProject.Views;

public partial class MainWindow : Window
{
    // Windows API Functions (Importing user32.dll)
     [DllImport("user32.dll")]
private static extern IntPtr GetForegroundWindow();

[DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private DispatcherTimer _timer;
  private string _lastWindowTitle = "";
    private DateTime _startTime;
    private DateTime _nextScreenshotCapture;
    private readonly string _screenshotFolder;
    private readonly Random _random = new Random();
    public MainWindow()
    {
        InitializeComponent();

        // Timer setup: Har 1 second baad check karega

        using (var db = new AppDbContext()) { db.Database.EnsureCreated(); }
        EnsureActivitySchema();

        _startTime = DateTime.Now;
        _nextScreenshotCapture = CalculateNextScreenshotTime();
        _screenshotFolder = Path.Combine(AppContext.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(_screenshotFolder);
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _timer.Start();

 
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        string currentWindow = GetActiveWindowTitle();
        ResultTextBlock.Text = currentWindow;
        if (currentWindow != _lastWindowTitle)
        {
            // 1. Purana data save karo (Sirf agar ye pehli baar nahi hai)
            if (!string.IsNullOrEmpty(_lastWindowTitle))
            {
                SaveToDatabase(_lastWindowTitle, _startTime, DateTime.Now);
            }

            
            _lastWindowTitle = currentWindow;
            _startTime = DateTime.Now;
        }

        if (DateTime.Now >= _nextScreenshotCapture)
        {
            if (OperatingSystem.IsWindows())
            {
                CaptureAndStoreScreenshot();
            }
            _nextScreenshotCapture = CalculateNextScreenshotTime();
        }
    }
    

    private DateTime CalculateNextScreenshotTime()
    {
        int randomMinutes = _random.Next(10, 31); // 10 to 30 minutes
        return DateTime.Now.AddMinutes(randomMinutes);
    }

    private string GetActiveWindowTitle()
    {
        const int nChars = 256;
        StringBuilder buff = new StringBuilder(nChars);
        IntPtr handle = GetForegroundWindow();

        if (GetWindowText(handle, buff, nChars) > 0)
        {
            return buff.ToString();
        }
        return "Desktop / Unknown";
    }
    private void SaveToDatabase(string name, DateTime start, DateTime end, string? screenshotPath = null)
{
    using (var db = new AppDbContext())
    {
        var record = new ActivityRecord
        {
            AppNames = name,
            StartTime = start,
            EndTime = end,
            DurationSeconds = (end - start).TotalSeconds,
            ScreenshotPath = screenshotPath
        };
        db.Activities.Add(record); // Table mein add karo
        db.SaveChanges();          // Database file mein save kar do
    }
}

    [SupportedOSPlatform("windows")]
    private void CaptureAndStoreScreenshot()
    {
        try
        {
            int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (width <= 0 || height <= 0)
            {
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(_screenshotFolder, $"screenshot_{timestamp}.png");

            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                bitmap.Save(filePath, ImageFormat.Png);
            }

            SaveToDatabase($"Screenshot - {GetActiveWindowTitle()}", DateTime.Now, DateTime.Now, filePath);
        }
        catch
        {
            // Screenshot capture fail ho jaye to tracking ko stop nahi karna.
        }
    }

    private void EnsureActivitySchema()
    {
        using var connection = new SqliteConnection("Data Source=tracking.db");
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
    }

public async void ShowHistory_Click(object sender, RoutedEventArgs e)
{
    using (var db = new AppDbContext())
    {
        // Database se saari activities nikalna (Latest pehle)
        var history = await db.Activities
                              .OrderByDescending(a => a.StartTime)
                              .ToListAsync<ActivityRecord>();

            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.HistoryItems.Clear();
                foreach (var dayGroup in history
                             .GroupBy(record => record.StartTime.Date)
                             .OrderByDescending(group => group.Key))
                {
                    viewModel.HistoryItems.Add(new HistoryDayGroup
                    {
                        Date = dayGroup.Key,
                        Records = dayGroup.OrderByDescending(record => record.StartTime).ToList()
                    });
                }
            }
    }
}
}
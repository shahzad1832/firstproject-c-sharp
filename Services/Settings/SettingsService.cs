using System;
using System.IO;
using System.Text.Json;

namespace firstProject.Services.Settings;

public sealed class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataFolder, "firstProject");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");
    }

    public AppSettings Load()
    {
        AppSettings settings;

        if (!File.Exists(_settingsPath))
        {
            settings = new AppSettings();
            EnsureDeviceId(settings);
            Save(settings);
            return settings;
        }

        try
        {
            string json = File.ReadAllText(_settingsPath);
            settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            settings = new AppSettings();
        }

        if (settings.IdleThresholdSeconds == 300)
        {
            settings.IdleThresholdSeconds = 120;
        }

        if (settings.ScreenshotMinMinutes == 10 && settings.ScreenshotMaxMinutes == 20)
        {
            settings.ScreenshotMinMinutes = 5;
            settings.ScreenshotMaxMinutes = 10;
        }

        bool shouldSave = EnsureDeviceId(settings);
        if (settings.ScreenshotMinMinutes == 5 && settings.ScreenshotMaxMinutes == 10 &&
            settings.IdleThresholdSeconds == 120 &&
            File.Exists(_settingsPath))
        {
            shouldSave = true;
        }

        if (shouldSave)
        {
            Save(settings);
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(settings, options);
        File.WriteAllText(_settingsPath, json);
    }

    private static bool EnsureDeviceId(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.DeviceId))
        {
            return false;
        }

        settings.DeviceId = Guid.NewGuid().ToString("N");
        return true;
    }
}

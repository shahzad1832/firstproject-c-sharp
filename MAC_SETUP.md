# Mac Setup Guide

This app now supports both Windows and macOS.

## macOS permissions checklist

Before you run the app on Mac, grant these permissions:

1. **Screen Recording**
   - Needed for screenshot capture.
   - Go to **System Settings** -> **Privacy & Security** -> **Screen Recording**.
   - Enable the app, then quit and reopen the app.

2. **Accessibility**
   - Needed if macOS blocks active app/window tracking.
   - Go to **System Settings** -> **Privacy & Security** -> **Accessibility**.
   - Enable the app, then quit and reopen the app.

3. **Automation**
   - If macOS asks for permission when reading the frontmost application through `System Events`, allow it.
   - This can appear the first time the app queries the active app title.

## Expected behavior on Mac

- The app reads the active application name using `osascript`.
- The app captures screenshots using `screencapture`.
- If a permission is missing, the app should still keep running, but window title or screenshots may fall back to `Desktop / Unknown` or not save.

## Troubleshooting

- If screenshots are not saved, re-check **Screen Recording** permission.
- If the active app name always shows `Desktop / Unknown`, re-check **Accessibility** and **Automation** permissions.
- After changing permissions, fully quit the app and start it again.

## Run commands

```bash
dotnet restore
dotnet build
dotnet run
```

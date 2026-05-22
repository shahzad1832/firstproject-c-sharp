import AppKit
import Cocoa
import CoreGraphics

func getActiveWindow() -> String {
    guard let frontmostApp = NSWorkspace.shared.frontmostApplication else {
        return "Desktop / Unknown"
    }

    let frontmostPid = frontmostApp.processIdentifier
    let appName = frontmostApp.localizedName ?? "Unknown"

    let options = CGWindowListOption(arrayLiteral: .excludeDesktopElements, .optionOnScreenOnly)
    guard let windowListInfo = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: AnyObject]] else {
        return appName
    }

    for info in windowListInfo {
        guard let pid = info[kCGWindowOwnerPID as String] as? Int, pid == frontmostPid else {
            continue
        }

        guard let layer = info[kCGWindowLayer as String] as? Int, layer == 0 else {
            continue
        }

        if let windowName = info[kCGWindowName as String] as? String, !windowName.isEmpty {
            return "\(appName) - \(windowName)"
        }
    }

    return appName
}

print(getActiveWindow())

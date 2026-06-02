#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${1:-Release}"
RUNTIME_ID="${2:-osx-arm64}"
APP_NAME="AssignIn"
BUNDLE_ID="com.assignin.app"
VERSION="1.0.0"

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INFO_PLIST_TEMPLATE="$PROJECT_ROOT/Info.plist"
HELPER_SOURCE="$PROJECT_ROOT/scripts/active_window.swift"
HELPER_BUILD_SCRIPT="$PROJECT_ROOT/scripts/build-active-window-helper.sh"
HELPER_BIN="$PROJECT_ROOT/active_window_detector"
ICON_SOURCE="$PROJECT_ROOT/Assets/assign.icns"
PUBLISH_DIR="$PROJECT_ROOT/bin/$CONFIGURATION/net10.0/$RUNTIME_ID/publish"
APP_DIR="$PUBLISH_DIR/$APP_NAME.app"
MACOS_DIR="$APP_DIR/Contents/MacOS"
RESOURCES_DIR="$APP_DIR/Contents/Resources"
ENTITLEMENTS_PATH="$APP_DIR/Contents/entitlements.plist"

if [[ ! -d "$PUBLISH_DIR" ]]; then
  echo "Publish folder not found: $PUBLISH_DIR"
  echo "Run: dotnet publish -c $CONFIGURATION -r $RUNTIME_ID --self-contained true"
  exit 1
fi

if [[ ! -f "$HELPER_BIN" ]]; then
  if [[ -f "$HELPER_SOURCE" ]]; then
    echo "Building active_window_detector..."
    "$HELPER_BUILD_SCRIPT"
  else
    echo "Missing helper source: $HELPER_SOURCE"
  fi
fi

if [[ ! -f "$HELPER_BIN" ]]; then
  echo "active_window_detector is required for production. Build it first:"
  echo "  $HELPER_BUILD_SCRIPT"
  exit 1
fi

rm -rf "$APP_DIR"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"

for item in "$PUBLISH_DIR"/*; do
  base="$(basename "$item")"
  if [[ "$base" == "$APP_NAME.app" ]]; then
    continue
  fi
  ditto "$item" "$MACOS_DIR/$base"
done

ditto "$HELPER_BIN" "$MACOS_DIR/active_window_detector"

if [[ -f "$ICON_SOURCE" ]]; then
  ditto "$ICON_SOURCE" "$RESOURCES_DIR/assign.icns"
else
  echo "Icon not found: $ICON_SOURCE"
fi

if [[ -f "$INFO_PLIST_TEMPLATE" ]]; then
  # Substitute tokens from the template so bundle metadata stays in sync.
  sed \
    -e "s/__APP_NAME__/$APP_NAME/g" \
    -e "s/__BUNDLE_ID__/$BUNDLE_ID/g" \
    -e "s/__VERSION__/$VERSION/g" \
    "$INFO_PLIST_TEMPLATE" > "$APP_DIR/Contents/Info.plist"
else
  cat > "$APP_DIR/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleExecutable</key>
  <string>$APP_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>$BUNDLE_ID</string>
  <key>CFBundleName</key>
  <string>$APP_NAME</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$VERSION</string>
  <key>CFBundleVersion</key>
  <string>$VERSION</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>NSScreenCaptureDescription</key>
  <string>Employee activity tracking requires periodic screenshots.</string>
  <key>NSAppleEventsUsageDescription</key>
  <string>Employee activity tracking needs active window and app detection.</string>
</dict>
</plist>
EOF
fi

cat > "$ENTITLEMENTS_PATH" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>com.apple.security.automation.apple-events</key>
  <true/>
</dict>
</plist>
EOF

chmod +x "$MACOS_DIR/$APP_NAME"

echo "Created: $APP_DIR"

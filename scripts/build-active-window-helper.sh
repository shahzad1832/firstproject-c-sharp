#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SWIFT_SOURCE="$PROJECT_ROOT/scripts/active_window.swift"
OUTPUT="$PROJECT_ROOT/active_window_detector"

if [[ ! -f "$SWIFT_SOURCE" ]]; then
  echo "Swift source not found: $SWIFT_SOURCE"
  exit 1
fi

if ! command -v swiftc >/dev/null 2>&1; then
  echo "swiftc not found. Install Xcode Command Line Tools."
  exit 1
fi

swiftc "$SWIFT_SOURCE" -o "$OUTPUT"
chmod +x "$OUTPUT"

echo "Built helper: $OUTPUT"

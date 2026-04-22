#!/bin/bash
# Build Skua for macOS using Avalonia UI
# Usage: ./build-macos.sh [--rid osx-arm64|osx-x64] [--config Release|Debug]

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

RID="${1:-osx-arm64}"
CONFIG="${2:-Release}"

echo "Building Skua (Avalonia) for macOS..."
echo "  RID: $RID"
echo "  Configuration: $CONFIG"
echo ""

# Restore
dotnet restore Skua.App.Avalonia/Skua.App.Avalonia.csproj

# Build
dotnet build Skua.App.Avalonia/Skua.App.Avalonia.csproj \
    -c "$CONFIG" \
    -r "$RID" \
    --no-restore

# Publish self-contained
dotnet publish Skua.App.Avalonia/Skua.App.Avalonia.csproj \
    -c "$CONFIG" \
    -r "$RID" \
    --self-contained true \
    -o "publish/$RID"

echo ""
echo "Build complete! Output: publish/$RID/"
echo "Run with: ./publish/$RID/Skua.App.Avalonia"

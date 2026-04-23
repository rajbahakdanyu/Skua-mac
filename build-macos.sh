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
echo ""

# Create .app bundle
APP_DIR="publish/Skua.app/Contents"
rm -rf publish/Skua.app
mkdir -p "$APP_DIR/MacOS" "$APP_DIR/Resources"
cp -R "publish/$RID/"* "$APP_DIR/MacOS/"

VERSION=$(grep '<Version>' Directory.Build.props | sed 's/.*<Version>\(.*\)<\/Version>/\1/' | tr -d '[:space:]')
SHORT_VERSION=$(echo "$VERSION" | cut -d. -f1-3)

cat > "$APP_DIR/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Skua</string>
    <key>CFBundleDisplayName</key>
    <string>Skua</string>
    <key>CFBundleIdentifier</key>
    <string>com.auqw.skua</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>CFBundleShortVersionString</key>
    <string>${SHORT_VERSION}</string>
    <key>CFBundleExecutable</key>
    <string>Skua.App.Avalonia</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSSupportsAutomaticGraphicsSwitching</key>
    <true/>
</dict>
</plist>
EOF

chmod +x "$APP_DIR/MacOS/Skua.App.Avalonia"
xattr -cr publish/Skua.app 2>/dev/null || true

echo "Skua.app bundle created: publish/Skua.app"
echo "You can double-click it in Finder or run: open publish/Skua.app"

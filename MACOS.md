# Running Skua on macOS

Skua runs natively on macOS using [Avalonia UI](https://avaloniaui.net/) and [Ruffle](https://ruffle.rs/) (a WebAssembly Flash emulator). No Wine, CrossOver, or virtual machine required.

## Prerequisites

- **macOS 12 (Monterey) or later**
- **.NET 10.0 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **A Chromium-based browser** (Chrome, Brave, Edge, etc.) — used to display the game

Verify .NET is installed:
```bash
dotnet --version
```

## Quick Start

### 1. Build

```bash
git clone https://github.com/auqw/Skua.git
cd Skua
./build-macos.sh
```

This builds a self-contained binary for Apple Silicon (`osx-arm64`) by default.

For Intel Macs:
```bash
./build-macos.sh osx-x64
```

### 2. Run

```bash
./publish/osx-arm64/Skua.App.Avalonia
```

If macOS blocks the app:
```bash
xattr -cr publish/osx-arm64/Skua.App.Avalonia
```

### 3. Game Window

When the app starts, it automatically launches a Chromium browser window with the game. The browser connects to a local HTTPS server (Ruffle + WebSocket bridge) hosted by Skua.

> **Note:** The browser may show a certificate warning on first launch because Skua uses a self-signed certificate. The `--allow-insecure-localhost` flag is passed automatically to Chromium to suppress this.

## Development

For iterative development without publishing:

```bash
dotnet run --project Skua.App.Avalonia/Skua.App.Avalonia.csproj -c Release
```

Or build and run separately:
```bash
dotnet build Skua.App.Avalonia/Skua.App.Avalonia.csproj -c Release
dotnet run --project Skua.App.Avalonia/Skua.App.Avalonia.csproj -c Release --no-build
```

See [BUILD.md](BUILD.md) for more build options and project structure details.

## Architecture

```
┌─────────────────────────────────────────────────┐
│  Skua.App.Avalonia (entry point)                │
│  ┌───────────────────────────────────────────┐  │
│  │ Avalonia UI (27 views, FluentTheme dark)  │  │
│  └──────────┬────────────────────────────────┘  │
│  ┌──────────▼────────────────────────────────┐  │
│  │ Kestrel HTTPS Server (localhost)          │  │
│  │  ├─ /game.html — Ruffle + SWF loader      │  │
│  │  ├─ /bridge   — WebSocket command channel │  │
│  │  ├─ /socket   — SmartFox TCP proxy        │  │
│  │  └─ /proxy/*  — HTTP proxy for game API   │  │
│  └──────────┬────────────────────────────────┘  │
│  ┌──────────▼────────────────────────────────┐  │
│  │ External Chromium Browser                 │  │
│  │  └─ Ruffle (wgpu-webgl Flash emulator)    │  │
│  └───────────────────────────────────────────┘  │
├─────────────────────────────────────────────────┤
│  Skua.Core (ViewModels, Scripts, Services)      │
│  Skua.Core.Models / Interfaces / Utils          │
└─────────────────────────────────────────────────┘
```

## Troubleshooting

### Game doesn't load / browser doesn't open

- Ensure a Chromium-based browser is installed (Chrome, Brave, or Edge)
- Check the console output for the local server URL (e.g. `https://localhost:35921/game.html`)
- Try opening that URL manually in your browser

### Certificate warning in browser

The app uses a self-signed certificate for localhost HTTPS. If the browser blocks it:
- Click "Advanced" → "Proceed to localhost (unsafe)"
- Or launch Chrome manually with: `--allow-insecure-localhost`

### macOS Gatekeeper blocks the app

```bash
xattr -cr publish/osx-arm64/Skua.App.Avalonia
```

### Scripts fail to compile

Ensure `skua.swf` exists in the build output directory. It should be copied automatically from `Skua.AS3/skua/bin/skua.swf` during build if present.

### NuGet restore failures

```bash
dotnet nuget locals all --clear
dotnet restore Skua.App.Avalonia/Skua.App.Avalonia.csproj --verbosity detailed
```

# Skua Build Guide

Instructions for building Skua from source. The project uses Avalonia UI for cross-platform support (macOS, Linux, Windows).

## Prerequisites

1. **.NET 10.0 SDK or later**
   - Download from: [Microsoft](https://dotnet.microsoft.com/download)
   - Verify: `dotnet --version`

2. **Git**
   ```bash
   git clone https://github.com/auqw/Skua.git
   cd Skua
   ```

## Quick Start

### macOS (Recommended)

```bash
# Apple Silicon (M1/M2/M3/M4)
./build-macos.sh

# Intel Mac
./build-macos.sh osx-x64
```

Output: `publish/osx-arm64/` (or `publish/osx-x64/`)

### Using .NET CLI (Any Platform)

```bash
# Build
dotnet build Skua.App.Avalonia/Skua.App.Avalonia.csproj -c Release

# Publish self-contained (pick your RID)
dotnet publish Skua.App.Avalonia/Skua.App.Avalonia.csproj \
    -c Release -r osx-arm64 --self-contained true -o publish/osx-arm64

# Run
./publish/osx-arm64/Skua.App.Avalonia
```

### Supported Runtime Identifiers

| RID | Platform |
|-----|----------|
| `osx-arm64` | macOS Apple Silicon |
| `osx-x64` | macOS Intel |
| `linux-x64` | Linux x64 |
| `win-x64` | Windows x64 |

## Project Structure

The Avalonia port uses a solution filter (`Skua.macOS.slnf`) that includes only the relevant projects:

| Project | Description |
|---------|-------------|
| `Skua.App.Avalonia` | Application entry point |
| `Skua.Avalonia` | Cross-platform UI layer (views, services, Ruffle bridge) |
| `Skua.Core` | Bot engine, scripting, game logic |
| `Skua.Core.Interfaces` | Shared interfaces |
| `Skua.Core.Models` | Data models |
| `Skua.Core.Utils` | Utilities |
| `Skua.Core.Generators` | Source generators |

### Build with Solution Filter

```bash
dotnet build Skua.macOS.slnf -c Release
```

## Build Version Management

Versions are centrally managed in `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <AssemblyVersion>1.4.3.0</AssemblyVersion>
    <FileVersion>1.4.3.0</FileVersion>
    <Version>1.4.3.0</Version>
  </PropertyGroup>
</Project>
```

## Troubleshooting

### NuGet Restore Failures

```bash
dotnet nuget locals all --clear
dotnet restore --verbosity detailed
```

### macOS Security Warning

If macOS blocks the app on first launch:

```bash
xattr -cr publish/osx-arm64/Skua.App.Avalonia
```

### Missing skua.swf

The `skua.swf` file is copied from `Skua.AS3/skua/bin/skua.swf` if it exists. If it's missing, the game client won't load. Ensure this file is present before building.

## Support

For build issues, create a GitHub issue with:
- Build error messages
- OS and .NET SDK version (`dotnet --info`)
- Steps to reproduce

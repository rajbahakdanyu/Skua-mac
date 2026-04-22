# macOS Helper Scripts

This directory contains scripts to help run Skua on macOS using Wine or CrossOver.

## Scripts

### `setup-wine.sh`

Automated Wine prefix setup for Skua. Downloads and installs the .NET Desktop Runtime and configures the Wine prefix.

```bash
chmod +x setup-wine.sh
./setup-wine.sh
```

**What it does:**
1. Detects whether you're on Apple Silicon or Intel
2. Checks for Wine/GPTK installation
3. Creates a Wine prefix at `~/skua-wine`
4. Guides you through installing the .NET Desktop Runtime
5. Configures Wine registry settings for WPF compatibility
6. Sets up Flash trust paths

### `run-skua.sh`

Launcher script that runs Skua with the correct Wine prefix and environment variables.

```bash
chmod +x run-skua.sh
./run-skua.sh [path-to-skua-folder]
```

**Arguments:**
- `path-to-skua-folder` — Optional. Path to the Skua installation inside the Wine prefix. Defaults to `C:\Skua`.

## Requirements

- **Apple Silicon:** Game Porting Toolkit (GPTK) via Homebrew, or CrossOver
- **Intel Mac:** Wine via Homebrew (`brew install --cask wine-stable`), or CrossOver
- A pre-built Skua Windows x64 release (MSI or portable folder)
- CleanFlash 34.0.0.175 installer

## See Also

- [MACOS.md](../MACOS.md) — Full macOS compatibility guide
- [BUILD.md](../BUILD.md) — Windows build instructions

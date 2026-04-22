# Running Skua on macOS

Skua is a Windows WPF application that embeds Adobe Flash Player via ActiveX COM. It cannot be natively compiled for macOS. This guide covers the practical ways to run Skua on a Mac.

## Table of Contents

- [Option 1: CrossOver (Recommended)](#option-1-crossover-recommended)
- [Option 2: Wine + GPTK (Free)](#option-2-wine--gptk-free)
- [Option 3: Virtual Machine](#option-3-virtual-machine)
- [Building a Self-Contained Package](#building-a-self-contained-package)
- [Troubleshooting](#troubleshooting)
- [Technical Notes](#technical-notes)

---

## Option 1: CrossOver (Recommended)

[CrossOver](https://www.codeweavers.com/crossover) is a commercial Wine-based compatibility layer with the best out-of-box support for Windows .NET applications on macOS. It costs ~$74 but has a 14-day free trial.

### Prerequisites

- macOS 12 (Monterey) or later
- CrossOver 24.0 or later (for .NET Desktop Runtime support)
- A pre-built Skua release (x64 MSI or portable folder)

### Setup Steps

1. **Install CrossOver** from [codeweavers.com](https://www.codeweavers.com/crossover)

2. **Create a new Windows 10 bottle:**
   - Open CrossOver → *Bottle* → *New Bottle*
   - Name: `Skua`
   - Type: **Windows 10 (64-bit)**

3. **Install .NET Desktop Runtime 10.0 (Windows x64):**
   - Download the **.NET Desktop Runtime 10.0 installer** (not SDK) for Windows x64 from [Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0)
   - In CrossOver, right-click the `Skua` bottle → *Install Software into Bottle* → *Install an unlisted application*
   - Browse to the downloaded `windowsdesktop-runtime-10.0.x-win-x64.exe` and install it

4. **Install CleanFlash:**
   - Download CleanFlash 34.0.0.175 (the Flash ActiveX installer)
   - Install it into the `Skua` bottle using the same method as step 3
   - This registers the Flash ActiveX OCX control in the Wine registry

5. **Install Skua:**
   - **Option A (MSI):** Install the `Skua-x64.msi` into the `Skua` bottle
   - **Option B (Portable):** Build a self-contained package (see [below](#building-a-self-contained-package)) and copy it into the bottle's `C:\` drive
     - The bottle's drive is at `~/Library/Application Support/CrossOver/Bottles/Skua/drive_c/`

6. **Run Skua:**
   - In CrossOver, open the `Skua` bottle → find `Skua.exe` → *Run*
   - Or create a launcher: right-click `Skua.exe` → *Create Launcher*

### CrossOver Bottle Configuration

If Skua doesn't start, try these bottle settings:

- **Wine Configuration** → *Libraries* tab → Add overrides:
  - `mscoree` → Native then Builtin
  - `flash` → Native (first)
- **Wine Configuration** → *Applications* tab → Windows Version: **Windows 10**

---

## Option 2: Wine + GPTK (Free)

Apple's [Game Porting Toolkit (GPTK)](https://developer.apple.com/games/game-porting-toolkit/) is a Wine-based translation layer optimized for Apple Silicon. For Intel Macs, standard Wine/Homebrew Wine works.

### Apple Silicon Mac (M1/M2/M3/M4)

#### Prerequisites

- macOS 14 (Sonoma) or later
- Xcode Command Line Tools
- Homebrew
- Rosetta 2 (for x64 translation)

#### Setup

```bash
# 1. Install Rosetta 2 (if not already installed)
softwareupdate --install-rosetta --agree-to-license

# 2. Install Homebrew (if not already)
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# 3. Install GPTK via Homebrew
brew tap apple/apple http://github.com/apple/homebrew-apple
brew install apple/apple/game-porting-toolkit

# 4. Create a Wine prefix for Skua
WINEPREFIX=~/skua-wine WINEARCH=win64 $(brew --prefix game-porting-toolkit)/bin/wine64 wineboot

# 5. Install .NET Desktop Runtime 10.0 (Windows x64)
# Download windowsdesktop-runtime-10.0.x-win-x64.exe from Microsoft
WINEPREFIX=~/skua-wine $(brew --prefix game-porting-toolkit)/bin/wine64 ~/Downloads/windowsdesktop-runtime-10.0.x-win-x64.exe

# 6. Install CleanFlash 34 into the prefix
WINEPREFIX=~/skua-wine $(brew --prefix game-porting-toolkit)/bin/wine64 ~/Downloads/cleanflash3400267installer1.exe

# 7. Run Skua
WINEPREFIX=~/skua-wine $(brew --prefix game-porting-toolkit)/bin/wine64 "C:\\Program Files\\Skua\\Skua.exe"
```

### Intel Mac

```bash
# 1. Install Homebrew Wine
brew install --cask wine-stable
# Or: brew install --cask --no-quarantine wine-stable

# 2. Create Wine prefix
WINEPREFIX=~/skua-wine WINEARCH=win64 wine64 wineboot

# 3. Install .NET Desktop Runtime 10.0 (same as above)
WINEPREFIX=~/skua-wine wine64 ~/Downloads/windowsdesktop-runtime-10.0.x-win-x64.exe

# 4. Install CleanFlash
WINEPREFIX=~/skua-wine wine64 ~/Downloads/cleanflash3400267installer1.exe

# 5. Run Skua
WINEPREFIX=~/skua-wine wine64 "C:\\Program Files\\Skua\\Skua.exe"
```

### Automated Setup Script

A helper script is provided at `macOS/setup-wine.sh` to automate the Wine prefix setup. See [macOS/README.md](macOS/README.md) for details.

---

## Option 3: Virtual Machine

For the most reliable experience, run Windows in a virtual machine. This guarantees full compatibility.

### Apple Silicon (M1/M2/M3/M4)

| VM Software | Cost | Notes |
|-------------|------|-------|
| [UTM](https://mac.getutm.app/) | Free | Uses QEMU; runs Windows 11 ARM with x64 emulation |
| [Parallels Desktop](https://www.parallels.com/) | $99/yr | Best performance; runs Windows ARM natively |
| [VMware Fusion](https://www.vmware.com/products/fusion.html) | Free (Personal) | Good performance; Windows ARM |

> **Note:** On Apple Silicon, you must install **Windows 11 ARM**. Windows ARM includes built-in x64 emulation that runs x64 .NET apps. Skua's x64 build works through this emulation layer.

### Intel Mac

| VM Software | Cost | Notes |
|-------------|------|-------|
| [UTM](https://mac.getutm.app/) | Free | Slower; hardware virtualization |
| [Parallels Desktop](https://www.parallels.com/) | $99/yr | Best performance |
| [VMware Fusion](https://www.vmware.com/products/fusion.html) | Free (Personal) | Solid alternative |
| Boot Camp | Free (built-in) | Dual-boot; native performance, macOS 12 and earlier only |

### VM Setup

1. Install the VM software of your choice
2. Create a Windows 10/11 VM (allocate at least 4 GB RAM, 2 CPU cores)
3. Inside Windows, install:
   - .NET Desktop Runtime 10.0
   - CleanFlash 34.0.0.175
   - Skua (via MSI installer or portable folder)
4. Run Skua normally

---

## Building a Self-Contained Package

A self-contained build bundles the .NET runtime so you don't need to install it separately under Wine. This is the recommended way to prepare Skua for macOS/Wine use.

### On Windows (or in a Windows VM)

```powershell
# Build a self-contained portable package for Wine/CrossOver
.\Build-Skua-Portable.ps1
```

This produces a `build/Portable/x64/` folder containing everything needed — no .NET runtime installation required in Wine.

### Manual self-contained publish

```powershell
dotnet publish Skua.App.WPF/Skua.App.WPF.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o ./build/Portable/x64
```

Then copy the `skua.swf`, `QuestData.json`, `AdvancedSkills.json`, and `Assemblies/` folder (with the Flash interop DLLs and CoreHook DLLs) into the output.

---

## Troubleshooting

### WPF rendering issues

WPF under Wine may have rendering glitches. Try:

```bash
# Force software rendering (slower but more compatible)
WINEPREFIX=~/skua-wine LIBGL_ALWAYS_SOFTWARE=1 wine64 Skua.exe

# Disable hardware acceleration
WINEPREFIX=~/skua-wine wine64 reg add "HKCU\Software\Microsoft\Avalon.Graphics" /v DisableHWAcceleration /t REG_DWORD /d 1 /f
```

### Flash ActiveX not loading

1. Verify CleanFlash is registered:
   ```bash
   WINEPREFIX=~/skua-wine wine64 reg query "HKCR\CLSID\{D27CDB6E-AE6D-11cf-96B8-444553540000}"
   ```
   This should return the ShockwaveFlash class registration.

2. Check that the Flash OCX DLL exists:
   ```bash
   ls ~/skua-wine/drive_c/windows/system32/Macromed/Flash/
   ```

3. If missing, manually register:
   ```bash
   WINEPREFIX=~/skua-wine wine64 regsvr32 "C:\\windows\\system32\\Macromed\\Flash\\Flash32_34_0_0_175.ocx"
   ```

### CoreHook / EoL Hook failures

The EoL hook (which patches `GetSystemTime` to bypass Flash's end-of-life date check) uses CoreHook for API detouring. If this fails under Wine:

1. **Try setting the system date to before January 2021** in the Wine prefix:
   ```bash
   # This is a workaround — fake the date at the Wine level
   WINEPREFIX=~/skua-wine faketime '2020-12-01' wine64 Skua.exe
   ```
   Install `faketime` via Homebrew: `brew install libfaketime`

2. **Patch Flash directly:** Use a hex-patched version of the Flash OCX that has the date check removed. CleanFlash 34 typically already has this patch applied.

### .NET runtime not found

If Skua fails to start with a .NET runtime error:
- Use the self-contained build (see [above](#building-a-self-contained-package))
- Or verify the runtime is installed: `WINEPREFIX=~/skua-wine wine64 dotnet --list-runtimes`

### App crashes on startup

Check Wine output for errors:
```bash
WINEPREFIX=~/skua-wine WINEDEBUG=+all wine64 Skua.exe 2>&1 | tee skua-wine.log
```

For less verbose but still useful output:
```bash
WINEPREFIX=~/skua-wine WINEDEBUG=warn+all wine64 Skua.exe 2>&1 | tee skua-wine.log
```

### macOS Gatekeeper blocks Wine

```bash
# Remove quarantine attribute
xattr -cr ~/skua-wine
xattr -cr /usr/local/Cellar/game-porting-toolkit/  # or wherever GPTK is installed
```

---

## Technical Notes

### Why can't Skua be natively compiled for macOS?

Skua depends on three Windows-only technologies:

1. **WPF (Windows Presentation Foundation):** The UI framework. 46+ XAML views would need to be rewritten for a cross-platform framework like Avalonia UI.

2. **Flash ActiveX (COM/OCX):** The game (AdventureQuest Worlds) is a Flash SWF. Skua embeds it via the Windows ActiveX Flash Player control. No macOS-native equivalent exists — Flash was discontinued on all platforms.

3. **CoreHook (API Hooking):** Skua hooks Windows' `GetSystemTime` to make Flash think it's still 2020 (pre-EOL). This uses native Windows DLL injection (`corehook64.dll`).

### Architecture overview

```
┌─────────────────────────────────────────────┐
│  Skua.App.WPF (WinExe)                     │  ← Windows-only (WPF + WinForms)
│  ┌─────────────────────────────────────┐    │
│  │ WPF UI (46+ XAML views)             │    │
│  │ MaterialDesign theme                │    │
│  └──────────┬──────────────────────────┘    │
│  ┌──────────▼──────────────────────────┐    │
│  │ Flash ActiveX (AxShockwaveFlash)    │    │  ← COM/ActiveX (Windows-only)
│  │ + EoL Hook (CoreHook → kernel32)    │    │
│  └──────────┬──────────────────────────┘    │
├─────────────┼───────────────────────────────┤
│  ┌──────────▼──────────────────────────┐    │
│  │ Skua.Core (ViewModels, Services)    │    │  ← Portable logic
│  │ CaptureProxy (TCP game proxy)       │    │
│  │ Script Compiler (Roslyn)            │    │
│  │ Plugin System                       │    │
│  └──────────┬──────────────────────────┘    │
│  ┌──────────▼──────────────────────────┐    │
│  │ Skua.Core.Models / Utils            │    │  ← Fully cross-platform (net10.0)
│  └─────────────────────────────────────┘    │
└─────────────────────────────────────────────┘
```

### What works under Wine/CrossOver

| Component | Expected Compatibility |
|-----------|----------------------|
| .NET 10 Desktop Runtime | Good (Wine 9.0+) |
| WPF rendering | Partial (may have visual glitches) |
| WinForms (Flash host) | Good |
| Flash ActiveX OCX | Good (if properly registered) |
| CoreHook API hooking | Uncertain (depends on Wine's ntdll/kernel32) |
| TCP proxy (game networking) | Excellent |
| File I/O / AppData paths | Excellent (Wine maps paths automatically) |
| System tray icon | Partial (may not show) |
| Hotkeys (P/Invoke user32) | Partial |

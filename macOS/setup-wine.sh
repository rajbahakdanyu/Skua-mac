#!/usr/bin/env bash
# setup-wine.sh — Automated Wine prefix setup for running Skua on macOS
# Usage: ./setup-wine.sh [--prefix PATH] [--skip-dotnet]
set -euo pipefail

# ─── Configuration ────────────────────────────────────────────────────────────
WINE_PREFIX="${WINE_PREFIX:-$HOME/skua-wine}"
DOTNET_RUNTIME_URL_X64="https://dotnet.microsoft.com/download/dotnet/10.0"
SKIP_DOTNET=false

# ─── Colors ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

info()    { echo -e "${CYAN}[INFO]${NC} $*"; }
success() { echo -e "${GREEN}[OK]${NC} $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC} $*"; }
error()   { echo -e "${RED}[ERROR]${NC} $*" >&2; }

# ─── Parse Arguments ──────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --prefix)   WINE_PREFIX="$2"; shift 2 ;;
        --skip-dotnet) SKIP_DOTNET=true; shift ;;
        -h|--help)
            echo "Usage: $0 [--prefix PATH] [--skip-dotnet]"
            echo "  --prefix PATH    Wine prefix path (default: ~/skua-wine)"
            echo "  --skip-dotnet    Skip .NET runtime installation prompt"
            exit 0 ;;
        *) error "Unknown option: $1"; exit 1 ;;
    esac
done

# ─── Detect Architecture ─────────────────────────────────────────────────────
ARCH=$(uname -m)
info "Detected architecture: $ARCH"

if [[ "$ARCH" == "arm64" ]]; then
    info "Apple Silicon detected — looking for Game Porting Toolkit (GPTK) or CrossOver"
    IS_APPLE_SILICON=true
else
    info "Intel Mac detected — looking for Wine"
    IS_APPLE_SILICON=false
fi

# ─── Find Wine Binary ────────────────────────────────────────────────────────
WINE_BIN=""

find_wine() {
    # Check for GPTK first (Apple Silicon)
    if [[ "$IS_APPLE_SILICON" == true ]]; then
        local gptk_wine
        gptk_wine="$(brew --prefix game-porting-toolkit 2>/dev/null || true)/bin/wine64"
        if [[ -x "$gptk_wine" ]]; then
            WINE_BIN="$gptk_wine"
            success "Found GPTK Wine: $WINE_BIN"
            return
        fi
    fi

    # Check for standard wine64
    if command -v wine64 &>/dev/null; then
        WINE_BIN="$(command -v wine64)"
        success "Found Wine: $WINE_BIN"
        return
    fi

    # Check for wine (32/64 combined)
    if command -v wine &>/dev/null; then
        WINE_BIN="$(command -v wine)"
        success "Found Wine: $WINE_BIN"
        return
    fi

    # Check common Homebrew paths
    for candidate in /usr/local/bin/wine64 /opt/homebrew/bin/wine64; do
        if [[ -x "$candidate" ]]; then
            WINE_BIN="$candidate"
            success "Found Wine: $WINE_BIN"
            return
        fi
    done

    error "Wine not found!"
    echo ""
    if [[ "$IS_APPLE_SILICON" == true ]]; then
        echo "Install Game Porting Toolkit:"
        echo "  brew tap apple/apple http://github.com/apple/homebrew-apple"
        echo "  brew install apple/apple/game-porting-toolkit"
    else
        echo "Install Wine via Homebrew:"
        echo "  brew install --cask wine-stable"
    fi
    echo ""
    echo "Or install CrossOver: https://www.codeweavers.com/crossover"
    exit 1
}

find_wine

# ─── Check Rosetta 2 (Apple Silicon) ─────────────────────────────────────────
if [[ "$IS_APPLE_SILICON" == true ]]; then
    if ! /usr/bin/pgrep -q oahd 2>/dev/null; then
        warn "Rosetta 2 may not be installed. Installing..."
        softwareupdate --install-rosetta --agree-to-license 2>/dev/null || true
    fi
    success "Rosetta 2 is available"
fi

# ─── Create Wine Prefix ──────────────────────────────────────────────────────
info "Setting up Wine prefix at: $WINE_PREFIX"

if [[ -d "$WINE_PREFIX" ]]; then
    warn "Wine prefix already exists at $WINE_PREFIX"
    read -rp "Recreate it? (y/N): " recreate
    if [[ "$recreate" =~ ^[Yy]$ ]]; then
        rm -rf "$WINE_PREFIX"
    else
        info "Using existing prefix"
    fi
fi

WINEPREFIX="$WINE_PREFIX" WINEARCH=win64 "$WINE_BIN" wineboot --init 2>/dev/null
success "Wine prefix created"

# ─── Configure Wine Registry ─────────────────────────────────────────────────
info "Configuring Wine registry for WPF compatibility..."

# Set Windows version to Windows 10
WINEPREFIX="$WINE_PREFIX" "$WINE_BIN" reg add "HKLM\\Software\\Microsoft\\Windows NT\\CurrentVersion" \
    /v CurrentBuildNumber /t REG_SZ /d "19041" /f 2>/dev/null
WINEPREFIX="$WINE_PREFIX" "$WINE_BIN" reg add "HKLM\\Software\\Microsoft\\Windows NT\\CurrentVersion" \
    /v ProductName /t REG_SZ /d "Windows 10 Pro" /f 2>/dev/null

# Disable WPF hardware acceleration (more compatible under Wine)
WINEPREFIX="$WINE_PREFIX" "$WINE_BIN" reg add "HKCU\\Software\\Microsoft\\Avalon.Graphics" \
    /v DisableHWAcceleration /t REG_DWORD /d 1 /f 2>/dev/null

# Set DLL overrides for .NET
WINEPREFIX="$WINE_PREFIX" "$WINE_BIN" reg add "HKCU\\Software\\Wine\\DllOverrides" \
    /v mscoree /t REG_SZ /d "native,builtin" /f 2>/dev/null

success "Wine registry configured"

# ─── Install .NET Desktop Runtime ─────────────────────────────────────────────
if [[ "$SKIP_DOTNET" == false ]]; then
    echo ""
    echo "════════════════════════════════════════════════════════════════"
    echo "  .NET Desktop Runtime 10.0 Installation"
    echo "════════════════════════════════════════════════════════════════"
    echo ""
    echo "Skua requires the .NET Desktop Runtime 10.0 (Windows x64)."
    echo ""
    echo "If you have a SELF-CONTAINED build of Skua, you can skip this."
    echo ""
    echo "Download the Windows x64 installer from:"
    echo "  $DOTNET_RUNTIME_URL_X64"
    echo ""
    read -rp "Path to the downloaded .NET runtime installer (or 'skip'): " dotnet_installer

    if [[ "$dotnet_installer" != "skip" && -f "$dotnet_installer" ]]; then
        info "Installing .NET Desktop Runtime..."
        WINEPREFIX="$WINE_PREFIX" "$WINE_BIN" "$dotnet_installer" /install /quiet /norestart 2>/dev/null || {
            warn "Silent install may have failed. Trying interactive install..."
            WINEPREFIX="$WINE_PREFIX" "$WINE_BIN" "$dotnet_installer" 2>/dev/null || true
        }
        success ".NET Desktop Runtime installation attempted"
    else
        info "Skipping .NET runtime installation"
    fi
fi

# ─── Install CleanFlash ───────────────────────────────────────────────────────
echo ""
echo "════════════════════════════════════════════════════════════════"
echo "  CleanFlash Installation"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "Skua requires CleanFlash 34.0.0.175 for the Flash ActiveX control."
echo ""
read -rp "Path to CleanFlash installer (or 'skip'): " flash_installer

if [[ "$flash_installer" != "skip" && -f "$flash_installer" ]]; then
    info "Installing CleanFlash..."
    WINEPREFIX="$WINE_PREFIX" "$WINE_BIN" "$flash_installer" 2>/dev/null || true
    success "CleanFlash installation attempted"

    # Verify Flash registration
    if WINEPREFIX="$WINE_PREFIX" "$WINE_BIN" reg query "HKCR\\CLSID\\{D27CDB6E-AE6D-11cf-96B8-444553540000}" &>/dev/null; then
        success "Flash ActiveX is registered in Wine registry"
    else
        warn "Flash ActiveX registration not detected. You may need to manually register the OCX."
        echo "  WINEPREFIX=$WINE_PREFIX $WINE_BIN regsvr32 'C:\\windows\\system32\\Macromed\\Flash\\Flash32_34_0_0_175.ocx'"
    fi
else
    info "Skipping CleanFlash installation"
fi

# ─── Setup Flash Trust ────────────────────────────────────────────────────────
info "Setting up Flash trust configuration..."
FLASH_TRUST_DIR="$WINE_PREFIX/drive_c/users/$(whoami)/AppData/Roaming/Macromedia/Flash Player/#Security/FlashPlayerTrust"
mkdir -p "$FLASH_TRUST_DIR"
echo "C:\\Skua" > "$FLASH_TRUST_DIR/Skua.cfg"
echo "C:\\users\\$(whoami)\\AppData\\Roaming\\Skua" >> "$FLASH_TRUST_DIR/Skua.cfg"
success "Flash trust file created"

# ─── Create Skua directory ────────────────────────────────────────────────────
SKUA_DIR="$WINE_PREFIX/drive_c/Skua"
mkdir -p "$SKUA_DIR"
info "Skua directory created at: $SKUA_DIR"
echo "  Copy your Skua build files into: $SKUA_DIR"

# ─── Summary ──────────────────────────────────────────────────────────────────
echo ""
echo "════════════════════════════════════════════════════════════════"
echo "  Setup Complete!"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "Wine prefix:  $WINE_PREFIX"
echo "Skua folder:  $SKUA_DIR"
echo "Wine binary:  $WINE_BIN"
echo ""
echo "Next steps:"
echo "  1. Copy your Skua build into: $SKUA_DIR"
echo "  2. Run Skua with: ./run-skua.sh"
echo "     Or manually: WINEPREFIX=$WINE_PREFIX $WINE_BIN 'C:\\Skua\\Skua.exe'"
echo ""
echo "If you encounter issues, see MACOS.md for troubleshooting."

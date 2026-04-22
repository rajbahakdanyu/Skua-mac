#!/usr/bin/env bash
# run-skua.sh — Launch Skua under Wine on macOS
# Usage: ./run-skua.sh [path-to-skua-exe]
set -euo pipefail

# ─── Configuration ────────────────────────────────────────────────────────────
WINE_PREFIX="${WINE_PREFIX:-$HOME/skua-wine}"
SKUA_EXE="${1:-C:\\Skua\\Skua.exe}"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

info()    { echo -e "${CYAN}[INFO]${NC} $*"; }
success() { echo -e "${GREEN}[OK]${NC} $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC} $*"; }
error()   { echo -e "${RED}[ERROR]${NC} $*" >&2; }

# ─── Find Wine Binary ────────────────────────────────────────────────────────
WINE_BIN=""
ARCH=$(uname -m)

if [[ "$ARCH" == "arm64" ]]; then
    # Apple Silicon — prefer GPTK
    gptk_wine="$(brew --prefix game-porting-toolkit 2>/dev/null || true)/bin/wine64"
    if [[ -x "$gptk_wine" ]]; then
        WINE_BIN="$gptk_wine"
    fi
fi

if [[ -z "$WINE_BIN" ]]; then
    for candidate in wine64 wine /usr/local/bin/wine64 /opt/homebrew/bin/wine64; do
        if command -v "$candidate" &>/dev/null || [[ -x "$candidate" ]]; then
            WINE_BIN="$candidate"
            break
        fi
    done
fi

if [[ -z "$WINE_BIN" ]]; then
    error "Wine not found. Run setup-wine.sh first or install Wine/GPTK."
    exit 1
fi

# ─── Verify Prefix ───────────────────────────────────────────────────────────
if [[ ! -d "$WINE_PREFIX" ]]; then
    error "Wine prefix not found at $WINE_PREFIX"
    echo "Run setup-wine.sh first to create the Wine prefix."
    exit 1
fi

# ─── Check Skua Exists ───────────────────────────────────────────────────────
# Convert Windows path to Unix path for existence check
UNIX_PATH="${SKUA_EXE//\\//}"
UNIX_PATH="${UNIX_PATH/C:/$WINE_PREFIX/drive_c}"
UNIX_PATH="${UNIX_PATH/c:/$WINE_PREFIX/drive_c}"

if [[ ! -f "$UNIX_PATH" ]]; then
    error "Skua.exe not found at: $UNIX_PATH"
    echo ""
    echo "Copy your Skua build files into: $WINE_PREFIX/drive_c/Skua/"
    echo "Expected file: $UNIX_PATH"
    exit 1
fi

# ─── Launch ───────────────────────────────────────────────────────────────────
info "Launching Skua..."
info "  Wine prefix: $WINE_PREFIX"
info "  Wine binary: $WINE_BIN"
info "  Executable:  $SKUA_EXE"
echo ""

# Environment for better WPF/Flash compatibility
export WINEPREFIX="$WINE_PREFIX"
export WINEDEBUG="${WINEDEBUG:--all}"  # Suppress Wine debug output by default

# Use software rendering if requested
if [[ "${SKUA_SOFTWARE_RENDER:-0}" == "1" ]]; then
    export LIBGL_ALWAYS_SOFTWARE=1
    warn "Software rendering enabled (SKUA_SOFTWARE_RENDER=1)"
fi

# Use faketime if available and requested
if [[ "${SKUA_FAKETIME:-0}" == "1" ]]; then
    if command -v faketime &>/dev/null; then
        info "Using faketime to set date to 2020-12-01 (Flash EoL bypass)"
        exec faketime '2020-12-01' "$WINE_BIN" "$SKUA_EXE"
    else
        warn "faketime not found. Install with: brew install libfaketime"
        warn "Launching without faketime — CoreHook EoL bypass will need to work under Wine"
    fi
fi

exec "$WINE_BIN" "$SKUA_EXE"

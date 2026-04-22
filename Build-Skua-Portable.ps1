<#
.SYNOPSIS
    Builds a self-contained portable Skua package for Wine/CrossOver on macOS.
    Must be run on Windows (or in a Windows VM/CI).

.PARAMETER Platform
    Target platform. Default: x64

.PARAMETER Configuration
    Build configuration. Default: Release

.PARAMETER OutputPath
    Output directory. Default: .\build\Portable

.EXAMPLE
    .\Build-Skua-Portable.ps1
    .\Build-Skua-Portable.ps1 -Platform x64 -Configuration Release
#>

param(
    [ValidateSet("x64", "x86")][string]$Platform = "x64",
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [string]$OutputPath = ".\build\Portable"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Header([string]$Message) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}
function Write-Success([string]$Message) { Write-Host "[SUCCESS] $Message" -ForegroundColor Green }
function Write-BuildError([string]$Message) { Write-Host "[ERROR] $Message" -ForegroundColor Red }
function Write-Info([string]$Message) { Write-Host "[INFO] $Message" -ForegroundColor Yellow }

Write-Header "Skua Portable Build (Self-Contained for Wine/macOS)"
Write-Info "Platform: $Platform | Configuration: $Configuration"

# ─── Prerequisites ────────────────────────────────────────────────────────────
$dotnetVersion = dotnet --version 2>$null
if (-not $dotnetVersion -or -not ($dotnetVersion -match "^10\.")) {
    Write-BuildError ".NET 10 SDK not found. Install from https://dotnet.microsoft.com/download/dotnet/10.0"
    exit 1
}
Write-Success ".NET SDK: $dotnetVersion"

# ─── Clean ────────────────────────────────────────────────────────────────────
$targetDir = Join-Path $OutputPath $Platform
if (Test-Path $targetDir) {
    Write-Info "Cleaning previous portable build..."
    Remove-Item -Path $targetDir -Recurse -Force
}

# ─── Build Source Generators First ────────────────────────────────────────────
Write-Info "Building source generators..."
$genResult = dotnet build ".\Skua.Core.Generators\Skua.Core.Generators.csproj" -c $Configuration -p:Platform=AnyCPU --verbosity minimal 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-BuildError "Source generator build failed"
    Write-Host $genResult -ForegroundColor Red
    exit 1
}
Write-Success "Source generators built"

# ─── Publish Self-Contained ───────────────────────────────────────────────────
Write-Header "Publishing self-contained Skua ($Platform)"

$rid = "win-$Platform"
$publishArgs = @(
    "publish"
    ".\Skua.App.WPF\Skua.App.WPF.csproj"
    "--configuration", $Configuration
    "--runtime", $rid
    "--self-contained", "true"
    "--output", $targetDir
    "-p:Platform=$Platform"
    "-p:PublishSingleFile=false"
    "-p:IncludeNativeLibrariesForSelfExtract=false"
    "-p:WarningLevel=0"
    "--verbosity", "minimal"
)

$result = & dotnet $publishArgs 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-BuildError "Publish failed"
    Write-Host $result -ForegroundColor Red
    exit 1
}
Write-Success "Self-contained publish completed"

# ─── Post-Publish Cleanup ────────────────────────────────────────────────────
Write-Info "Organizing output..."

# Rename exe
$sourceExe = Join-Path $targetDir "Skua.App.WPF.exe"
$destExe = Join-Path $targetDir "Skua.exe"
if (Test-Path $sourceExe) {
    Move-Item -Path $sourceExe -Destination $destExe -Force
    Write-Info "Renamed Skua.App.WPF.exe -> Skua.exe"
}

# Remove PDB files
Get-ChildItem -Path $targetDir -Filter "*.pdb" -Recurse | Remove-Item -Force
Write-Info "Removed PDB files"

# ─── Copy FFDec (Release only) ────────────────────────────────────────────────
if ($Configuration -eq "Release" -and (Test-Path ".\FFDec")) {
    $ffDecDest = Join-Path $targetDir "FFDec"
    if (-not (Test-Path $ffDecDest)) {
        Copy-Item -Path ".\FFDec" -Destination $ffDecDest -Recurse
        Write-Info "Copied FFDec tools"
    }
}

# ─── Summary ──────────────────────────────────────────────────────────────────
$totalSize = (Get-ChildItem -Path $targetDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
$totalSizeMB = [math]::Round($totalSize / 1MB, 2)
$fileCount = (Get-ChildItem -Path $targetDir -Recurse -File).Count

Write-Header "Portable Build Complete"
Write-Success "Output: $(Resolve-Path $targetDir)"
Write-Info "Files: $fileCount | Size: ${totalSizeMB} MB"
Write-Info "Runtime: $rid (self-contained)"
Write-Host ""
Write-Info "This build includes the .NET runtime and can be run under Wine/CrossOver"
Write-Info "without installing .NET separately."
Write-Host ""
Write-Info "To use on macOS:"
Write-Info "  1. Copy the '$Platform' folder to your Mac"
Write-Info "  2. Place it in the Wine prefix: ~/skua-wine/drive_c/Skua/"
Write-Info "  3. Run: ./macOS/run-skua.sh"

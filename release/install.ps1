# SNES SPC VST3 Plugin - Easy Installer
# Run this script as Administrator for system-wide install

param(
	[switch]$UserInstall  # Install to user folder instead of system
)

$ScriptDir = $PSScriptRoot
$PluginDir = Join-Path $ScriptDir "plugin"
$vst3Bundle = Get-ChildItem -Path $PluginDir -Filter "*.vst3" -Directory | Select-Object -First 1

if (-not $vst3Bundle) {
	Write-Host "ERROR: VST3 plugin not found in release package!" -ForegroundColor Red
	exit 1
}

# Determine install location
if ($UserInstall) {
	$InstallDir = "$env:LOCALAPPDATA\Programs\Common\VST3"
} else {
	$InstallDir = "$env:CommonProgramFiles\VST3"
}

# Create install directory if needed
if (-not (Test-Path $InstallDir)) {
	New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

$DestPath = Join-Path $InstallDir $vst3Bundle.Name

Write-Host ""
Write-Host "SNES SPC VST3 Plugin Installer" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Source:      $($vst3Bundle.FullName)" -ForegroundColor White
Write-Host "Destination: $DestPath" -ForegroundColor White
Write-Host ""

# Remove existing installation
if (Test-Path $DestPath) {
	Write-Host "Removing existing installation..." -ForegroundColor Yellow
	Remove-Item -Recurse -Force $DestPath
}

# Copy plugin
Write-Host "Installing plugin..." -ForegroundColor Yellow
Copy-Item -Recurse -Force $vst3Bundle.FullName $InstallDir

Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Open your DAW (Ableton Live, etc.)" -ForegroundColor White
Write-Host "  2. Rescan VST3 plugins" -ForegroundColor White
Write-Host "  3. Find 'SNES SPC Player' in your plugin list" -ForegroundColor White
Write-Host ""
Write-Host "For testing instructions, see: TESTING_QUICKSTART.md" -ForegroundColor Cyan

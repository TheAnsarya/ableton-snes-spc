# Create Release Package for SNES SPC VST3 Plugin
# Creates a ready-to-install package for manual testing

param(
	[string]$OutputDir = "release",
	[switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ReleaseDir = Join-Path $ProjectRoot $OutputDir
$Version = "0.4.0-beta"

function Write-Header($text) {
	Write-Host ""
	Write-Host "========================================" -ForegroundColor Cyan
	Write-Host " $text" -ForegroundColor Cyan
	Write-Host "========================================" -ForegroundColor Cyan
}

# Clean/create release directory
Write-Header "Preparing Release Directory"
if (Test-Path $ReleaseDir) {
	Remove-Item -Recurse -Force $ReleaseDir
}
New-Item -ItemType Directory -Path $ReleaseDir | Out-Null
Write-Host "Created: $ReleaseDir" -ForegroundColor Green

# Build if not skipped
if (-not $SkipBuild) {
	Write-Header "Building Plugin (Release)"
	& "$ProjectRoot\build-vst3.ps1" -Release
	if ($LASTEXITCODE -ne 0) {
		Write-Host "ERROR: Build failed!" -ForegroundColor Red
		exit 1
	}
}

# Find built VST3 bundle
Write-Header "Locating Built Plugin"
$BuildDir = Join-Path $ProjectRoot "build"
$vst3Bundle = Get-ChildItem -Path $BuildDir -Recurse -Filter "*.vst3" -Directory | Select-Object -First 1

if (-not $vst3Bundle) {
	Write-Host "ERROR: Could not find built VST3 bundle!" -ForegroundColor Red
	Write-Host "Please run: .\build-vst3.ps1 -Release" -ForegroundColor Yellow
	exit 1
}

Write-Host "Found: $($vst3Bundle.FullName)" -ForegroundColor Green

# Copy VST3 bundle
Write-Header "Copying Plugin Files"
$pluginDir = Join-Path $ReleaseDir "plugin"
New-Item -ItemType Directory -Path $pluginDir | Out-Null
Copy-Item -Recurse -Force $vst3Bundle.FullName $pluginDir
Write-Host "Copied VST3 bundle" -ForegroundColor Green

# Copy .NET assembly if not using Native AOT
$dotnetDir = Join-Path $ProjectRoot "src\SpcPlugin.Core\bin\Release\net10.0"
if (Test-Path $dotnetDir) {
	$dllPath = Join-Path $dotnetDir "SpcPlugin.Core.dll"
	if (Test-Path $dllPath) {
		$targetDir = Join-Path $pluginDir "$($vst3Bundle.Name)\Contents\x86_64-win"
		Copy-Item -Force $dllPath $targetDir -ErrorAction SilentlyContinue
		Write-Host "Copied .NET assembly" -ForegroundColor Green
	}
}

# Create sample-spc folder for test files
$sampleDir = Join-Path $ReleaseDir "sample-spc"
New-Item -ItemType Directory -Path $sampleDir | Out-Null
@"
# Sample SPC Files

Place your test SPC files here for easy access during testing.

You can find SPC files at:
- https://www.zophar.net/music/nintendo-snes-spc
- https://snesmusic.org/

Note: SPC files are typically copyrighted game music.
For testing, use files you have the right to use.
"@ | Set-Content (Join-Path $sampleDir "README.txt")
Write-Host "Created sample-spc folder" -ForegroundColor Green

# Create install script
Write-Header "Creating Install Script"
@'
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
'@ | Set-Content (Join-Path $ReleaseDir "install.ps1")
Write-Host "Created install.ps1" -ForegroundColor Green

# Create uninstall script
@'
# SNES SPC VST3 Plugin - Uninstaller

$PluginName = "SnesSpcVst3.vst3"

$SystemPath = "$env:CommonProgramFiles\VST3\$PluginName"
$UserPath = "$env:LOCALAPPDATA\Programs\Common\VST3\$PluginName"

$removed = $false

if (Test-Path $SystemPath) {
	Write-Host "Removing system installation: $SystemPath" -ForegroundColor Yellow
	Remove-Item -Recurse -Force $SystemPath
	$removed = $true
}

if (Test-Path $UserPath) {
	Write-Host "Removing user installation: $UserPath" -ForegroundColor Yellow
	Remove-Item -Recurse -Force $UserPath
	$removed = $true
}

if ($removed) {
	Write-Host "Uninstallation complete!" -ForegroundColor Green
} else {
	Write-Host "No installation found." -ForegroundColor Yellow
}
'@ | Set-Content (Join-Path $ReleaseDir "uninstall.ps1")
Write-Host "Created uninstall.ps1" -ForegroundColor Green

# Copy documentation
Write-Header "Copying Documentation"
$docsToInclude = @(
	"docs\USER_GUIDE.md",
	"docs\SPC_FORMAT.md",
	"~manual-testing\VST3_TESTING_GUIDE.md"
)

$docsDir = Join-Path $ReleaseDir "docs"
New-Item -ItemType Directory -Path $docsDir | Out-Null

foreach ($doc in $docsToInclude) {
	$srcPath = Join-Path $ProjectRoot $doc
	if (Test-Path $srcPath) {
		Copy-Item -Force $srcPath $docsDir
		Write-Host "Copied: $doc" -ForegroundColor Green
	}
}

# Create README
Write-Header "Creating Package README"
@"
# SNES SPC VST3 Plugin v$Version

## Quick Install

### Windows (PowerShell)

**Option 1: System-wide install (requires Admin)**
``````powershell
.\install.ps1
``````

**Option 2: User install (no Admin required)**
``````powershell
.\install.ps1 -UserInstall
``````

### Manual Install

1. Copy the `plugin\SnesSpcVst3.vst3` folder to:
   - System: ``C:\Program Files\Common Files\VST3\``
   - User: ``%LOCALAPPDATA%\Programs\Common\VST3\``

2. Rescan plugins in your DAW

## Quick Start

1. Open your DAW (Ableton Live, etc.)
2. Add the "SNES SPC Player" plugin to a track
3. Load an SPC file using the File Browser or drag & drop
4. Click Play to hear the music!

## Testing Guide

See ``TESTING_QUICKSTART.md`` for step-by-step testing instructions.

## Documentation

- [USER_GUIDE.md](docs/USER_GUIDE.md) - Complete usage guide
- [SPC_FORMAT.md](docs/SPC_FORMAT.md) - SPC file format reference
- [VST3_TESTING_GUIDE.md](docs/VST3_TESTING_GUIDE.md) - Detailed test cases

## Uninstall

``````powershell
.\uninstall.ps1
``````

## System Requirements

- Windows 10/11 (64-bit)
- .NET 10.0 Runtime (usually bundled)
- VST3-compatible DAW (Ableton Live 10+, FL Studio 20+, etc.)

## Known Issues

- Native AOT build may require Visual C++ Redistributable
- Some DAWs may require manual plugin rescan

## Support

Report issues at: https://github.com/TheAnsarya/ableton-snes-spc/issues
"@ | Set-Content (Join-Path $ReleaseDir "README.md")
Write-Host "Created README.md" -ForegroundColor Green

# Create ZIP archive
Write-Header "Creating ZIP Archive"
$ZipName = "SnesSpcVst3-$Version-win64.zip"
$ZipPath = Join-Path $ProjectRoot $ZipName

if (Test-Path $ZipPath) {
	Remove-Item -Force $ZipPath
}

Compress-Archive -Path "$ReleaseDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
Write-Host "Created: $ZipPath" -ForegroundColor Green

# Summary
Write-Header "Release Package Complete"
Write-Host ""
Write-Host "Package contents:" -ForegroundColor Yellow
Get-ChildItem -Path $ReleaseDir -Recurse | ForEach-Object {
	$rel = $_.FullName.Replace($ReleaseDir, "").TrimStart("\")
	if ($_.PSIsContainer) {
		Write-Host "  [DIR]  $rel" -ForegroundColor Cyan
	} else {
		Write-Host "  [FILE] $rel" -ForegroundColor White
	}
}
Write-Host ""
Write-Host "Release archive: $ZipPath" -ForegroundColor Green
Write-Host ""
Write-Host "To test locally:" -ForegroundColor Yellow
Write-Host "  cd release" -ForegroundColor White
Write-Host "  .\install.ps1 -UserInstall" -ForegroundColor White
Write-Host ""

# Setup VST3 SDK for SNES SPC VST3 Plugin Development
# Downloads and extracts the VST3 SDK from Steinberg's GitHub repository

param(
	[string]$InstallPath = "C:\vst3sdk",
	[string]$Version = "v3.7.12_build_20",
	[switch]$SetEnvironmentVariable
)

$ErrorActionPreference = "Stop"

function Write-Header($text) {
	Write-Host ""
	Write-Host "========================================" -ForegroundColor Cyan
	Write-Host " $text" -ForegroundColor Cyan
	Write-Host "========================================" -ForegroundColor Cyan
}

Write-Header "VST3 SDK Setup"

# Check if already installed
if (Test-Path "$InstallPath\CMakeLists.txt") {
	Write-Host "VST3 SDK already installed at: $InstallPath" -ForegroundColor Green
	Write-Host "To reinstall, delete the folder first." -ForegroundColor Yellow

	if ($SetEnvironmentVariable) {
		[System.Environment]::SetEnvironmentVariable("VST3_SDK_ROOT", $InstallPath, [System.EnvironmentVariableTarget]::User)
		$env:VST3_SDK_ROOT = $InstallPath
		Write-Host "Environment variable VST3_SDK_ROOT set to: $InstallPath" -ForegroundColor Green
	}
	exit 0
}

# Download URL - using Steinberg's official GitHub releases
$GithubApiUrl = "https://api.github.com/repos/steinbergmedia/vst3sdk/releases"
$DownloadUrl = "https://github.com/steinbergmedia/vst3sdk/archive/refs/tags/$Version.zip"

Write-Host "Download URL: $DownloadUrl" -ForegroundColor Gray

# Create temp directory
$TempDir = Join-Path $env:TEMP "vst3sdk_setup"
$ZipPath = Join-Path $TempDir "vst3sdk.zip"

if (-not (Test-Path $TempDir)) {
	New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
}

# Download
Write-Header "Downloading VST3 SDK $Version"
Write-Host "This may take a few minutes..." -ForegroundColor Gray

try {
	# Use .NET WebClient for better progress
	$webClient = New-Object System.Net.WebClient
	$webClient.Headers.Add("User-Agent", "PowerShell VST3 SDK Installer")

	# Progress handler
	$downloadComplete = $false
	Register-ObjectEvent -InputObject $webClient -EventName DownloadProgressChanged -Action {
		$percent = $EventArgs.ProgressPercentage
		Write-Progress -Activity "Downloading VST3 SDK" -Status "$percent% Complete" -PercentComplete $percent
	} | Out-Null

	Register-ObjectEvent -InputObject $webClient -EventName DownloadFileCompleted -Action {
		$script:downloadComplete = $true
		Write-Progress -Activity "Downloading VST3 SDK" -Completed
	} | Out-Null

	$webClient.DownloadFileAsync([Uri]$DownloadUrl, $ZipPath)

	# Wait for download
	$timeout = [DateTime]::Now.AddMinutes(10)
	while (-not $downloadComplete -and [DateTime]::Now -lt $timeout) {
		Start-Sleep -Milliseconds 500
	}

	if (-not $downloadComplete) {
		$webClient.CancelAsync()
		throw "Download timed out"
	}

	Write-Host "Download complete!" -ForegroundColor Green
}
catch {
	Write-Host "Error downloading: $_" -ForegroundColor Red
	Write-Host ""
	Write-Host "Manual installation:" -ForegroundColor Yellow
	Write-Host "1. Go to: https://github.com/steinbergmedia/vst3sdk/releases" -ForegroundColor White
	Write-Host "2. Download the latest release ZIP" -ForegroundColor White
	Write-Host "3. Extract to: $InstallPath" -ForegroundColor White
	Write-Host "4. Run: [System.Environment]::SetEnvironmentVariable('VST3_SDK_ROOT', '$InstallPath', 'User')" -ForegroundColor White
	exit 1
}

# Extract
Write-Header "Extracting VST3 SDK"
Write-Host "Extracting to: $InstallPath" -ForegroundColor Gray

try {
	# Ensure parent directory exists
	$ParentDir = Split-Path $InstallPath -Parent
	if (-not (Test-Path $ParentDir)) {
		New-Item -ItemType Directory -Path $ParentDir -Force | Out-Null
	}

	# Extract to temp location first
	$ExtractTemp = Join-Path $TempDir "extracted"
	if (Test-Path $ExtractTemp) {
		Remove-Item $ExtractTemp -Recurse -Force
	}

	Expand-Archive -Path $ZipPath -DestinationPath $ExtractTemp -Force

	# Find the extracted folder (it will be named vst3sdk-{version})
	$ExtractedFolder = Get-ChildItem $ExtractTemp -Directory | Select-Object -First 1

	if (-not $ExtractedFolder) {
		throw "Could not find extracted VST3 SDK folder"
	}

	# Move to final location
	if (Test-Path $InstallPath) {
		Remove-Item $InstallPath -Recurse -Force
	}
	Move-Item $ExtractedFolder.FullName $InstallPath

	Write-Host "Extraction complete!" -ForegroundColor Green
}
catch {
	Write-Host "Error extracting: $_" -ForegroundColor Red
	exit 1
}

# Initialize submodules (VST3 SDK uses git submodules)
Write-Header "Initializing Submodules"
Write-Host "The VST3 SDK uses git submodules for dependencies..." -ForegroundColor Gray

$currentDir = Get-Location
try {
	Set-Location $InstallPath

	# Check if it's a git repo (it won't be from ZIP, so we need to clone properly)
	if (-not (Test-Path ".git")) {
		Write-Host "Converting to git repository to fetch submodules..." -ForegroundColor Yellow

		# Initialize git
		git init
		git remote add origin https://github.com/steinbergmedia/vst3sdk.git
		git fetch origin $Version --depth=1
		git checkout FETCH_HEAD

		# Get submodules
		git submodule update --init --recursive
	}

	Write-Host "Submodules initialized!" -ForegroundColor Green
}
catch {
	Write-Host "Warning: Could not initialize submodules: $_" -ForegroundColor Yellow
	Write-Host "You may need to manually clone the repository:" -ForegroundColor Yellow
	Write-Host "  git clone --recursive https://github.com/steinbergmedia/vst3sdk.git $InstallPath" -ForegroundColor White
}
finally {
	Set-Location $currentDir
}

# Clean up
Write-Header "Cleaning Up"
Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Temporary files removed." -ForegroundColor Green

# Set environment variable
if ($SetEnvironmentVariable) {
	Write-Header "Setting Environment Variable"
	[System.Environment]::SetEnvironmentVariable("VST3_SDK_ROOT", $InstallPath, [System.EnvironmentVariableTarget]::User)
	$env:VST3_SDK_ROOT = $InstallPath
	Write-Host "VST3_SDK_ROOT set to: $InstallPath" -ForegroundColor Green
	Write-Host "Note: You may need to restart your terminal for this to take effect." -ForegroundColor Yellow
}

# Verify installation
Write-Header "Verifying Installation"
$requiredFiles = @(
	"CMakeLists.txt",
	"pluginterfaces",
	"base",
	"public.sdk"
)

$allPresent = $true
foreach ($file in $requiredFiles) {
	$path = Join-Path $InstallPath $file
	if (Test-Path $path) {
		Write-Host "  [OK] $file" -ForegroundColor Green
	} else {
		Write-Host "  [MISSING] $file" -ForegroundColor Red
		$allPresent = $false
	}
}

if ($allPresent) {
	Write-Host ""
	Write-Host "VST3 SDK installed successfully!" -ForegroundColor Green
	Write-Host ""
	Write-Host "Next steps:" -ForegroundColor Cyan
	Write-Host "  1. Build the VST3 plugin: .\build-vst3.ps1" -ForegroundColor White
	Write-Host "  2. Or with Native AOT: .\build-vst3.ps1 -NativeAot" -ForegroundColor White
} else {
	Write-Host ""
	Write-Host "Installation incomplete. Some files are missing." -ForegroundColor Red
	Write-Host "Try cloning manually:" -ForegroundColor Yellow
	Write-Host "  git clone --recursive https://github.com/steinbergmedia/vst3sdk.git $InstallPath" -ForegroundColor White
}

Write-Host ""

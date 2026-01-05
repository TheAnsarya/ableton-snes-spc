# Build script for SNES SPC VST3 Plugin
# Requires: CMake 3.21+, Visual Studio 2022 (or Build Tools), VST3 SDK

param(
	[switch]$Clean,
	[switch]$Release,
	[switch]$NativeAot,
	[switch]$Install,
	[switch]$OpenAbleton,
	[string]$VST3_SDK_ROOT = $env:VST3_SDK_ROOT
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$BuildDir = Join-Path $ProjectRoot "build"
$Vst3Dir = Join-Path $ProjectRoot "vst3"
$SrcDir = Join-Path $ProjectRoot "src"

# Default VST3 SDK location if not set
$DefaultVst3Paths = @(
	"C:\vst3sdk",
	"C:\SDK\vst3sdk",
	"C:\dev\vst3sdk",
	"$env:USERPROFILE\vst3sdk",
	"$env:USERPROFILE\source\vst3sdk"
)

# Default Ableton installation paths
$AbletonPaths = @(
	"C:\ProgramData\Ableton\Live 10 Suite\Program\Ableton Live 10 Suite.exe",
	"C:\ProgramData\Ableton\Live 11 Suite\Program\Ableton Live 11 Suite.exe",
	"C:\ProgramData\Ableton\Live 12 Suite\Program\Ableton Live 12 Suite.exe",
	"C:\Program Files\Ableton\Live 10 Suite\Program\Ableton Live 10 Suite.exe",
	"C:\Program Files\Ableton\Live 11 Suite\Program\Ableton Live 11 Suite.exe",
	"C:\Program Files\Ableton\Live 12 Suite\Program\Ableton Live 12 Suite.exe"
)

# VST3 installation directory
$Vst3InstallDir = "$env:CommonProgramFiles\VST3"

function Write-Header($text) {
	Write-Host ""
	Write-Host "========================================" -ForegroundColor Cyan
	Write-Host " $text" -ForegroundColor Cyan
	Write-Host "========================================" -ForegroundColor Cyan
}

function Find-VST3SDK {
	if ($VST3_SDK_ROOT -and (Test-Path $VST3_SDK_ROOT)) {
		return $VST3_SDK_ROOT
	}

	foreach ($path in $DefaultVst3Paths) {
		if (Test-Path $path) {
			return $path
		}
	}

	return $null
}

function Find-Ableton {
	foreach ($path in $AbletonPaths) {
		if (Test-Path $path) {
			return $path
		}
	}
	return $null
}

# Check for VST3 SDK
Write-Header "Checking VST3 SDK"
$Vst3SdkPath = Find-VST3SDK
if (-not $Vst3SdkPath) {
	Write-Host "ERROR: VST3 SDK not found!" -ForegroundColor Red
	Write-Host ""
	Write-Host "Please download the VST3 SDK from:" -ForegroundColor Yellow
	Write-Host "  https://www.steinberg.net/developers/" -ForegroundColor White
	Write-Host ""
	Write-Host "Then either:" -ForegroundColor Yellow
	Write-Host "  1. Set VST3_SDK_ROOT environment variable" -ForegroundColor White
	Write-Host "  2. Extract to one of these locations:" -ForegroundColor White
	foreach ($path in $DefaultVst3Paths) {
		Write-Host "     - $path" -ForegroundColor Gray
	}
	Write-Host ""
	Write-Host "Or run: .\build-vst3.ps1 -VST3_SDK_ROOT 'C:\path\to\vst3sdk'" -ForegroundColor Yellow
	exit 1
}
Write-Host "Found VST3 SDK at: $Vst3SdkPath" -ForegroundColor Green

# Check for CMake
Write-Header "Checking CMake"
$cmake = Get-Command cmake -ErrorAction SilentlyContinue
if (-not $cmake) {
	Write-Host "ERROR: CMake not found!" -ForegroundColor Red
	Write-Host "Please install CMake 3.21+ from https://cmake.org/download/" -ForegroundColor Yellow
	exit 1
}
$cmakeVersion = (cmake --version | Select-Object -First 1)
Write-Host "Found: $cmakeVersion" -ForegroundColor Green

# Clean build directory if requested
if ($Clean) {
	Write-Header "Cleaning Build Directory"
	if (Test-Path $BuildDir) {
		Remove-Item -Recurse -Force $BuildDir
		Write-Host "Cleaned: $BuildDir" -ForegroundColor Green
	}
}

# Build .NET library first
Write-Header "Building .NET Library"
$DotNetProj = Join-Path $SrcDir "SpcPlugin.Core\SpcPlugin.Core.csproj"
$Config = if ($Release) { "Release" } else { "Debug" }

if ($NativeAot) {
	Write-Host "Building with Native AOT..." -ForegroundColor Yellow
	dotnet publish $DotNetProj -c $Config -r win-x64 --self-contained
	if ($LASTEXITCODE -ne 0) {
		Write-Host "ERROR: .NET Native AOT build failed!" -ForegroundColor Red
		exit 1
	}
} else {
	dotnet build $DotNetProj -c $Config
	if ($LASTEXITCODE -ne 0) {
		Write-Host "ERROR: .NET build failed!" -ForegroundColor Red
		exit 1
	}
}
Write-Host ".NET library built successfully" -ForegroundColor Green

# Create build directory
if (-not (Test-Path $BuildDir)) {
	New-Item -ItemType Directory -Path $BuildDir | Out-Null
}

# Configure CMake
Write-Header "Configuring CMake"
Push-Location $BuildDir
try {
	$cmakeArgs = @(
		"-G", "Visual Studio 17 2022",
		"-A", "x64",
		"-DVST3_SDK_ROOT=$Vst3SdkPath",
		"-DCMAKE_BUILD_TYPE=$Config"
	)

	if ($NativeAot) {
		$cmakeArgs += "-DUSE_NATIVE_AOT=ON"
	}

	$cmakeArgs += $Vst3Dir

	Write-Host "cmake $($cmakeArgs -join ' ')" -ForegroundColor Gray
	& cmake @cmakeArgs

	if ($LASTEXITCODE -ne 0) {
		Write-Host "ERROR: CMake configuration failed!" -ForegroundColor Red
		exit 1
	}

	# Build
	Write-Header "Building VST3 Plugin"
	cmake --build . --config $Config --parallel

	if ($LASTEXITCODE -ne 0) {
		Write-Host "ERROR: Build failed!" -ForegroundColor Red
		exit 1
	}

	Write-Host "VST3 plugin built successfully!" -ForegroundColor Green

	# Find the built VST3 bundle
	$vst3Bundle = Get-ChildItem -Path $BuildDir -Recurse -Filter "*.vst3" -Directory | Select-Object -First 1
	if ($vst3Bundle) {
		Write-Host "Built: $($vst3Bundle.FullName)" -ForegroundColor Green
	}

	# Install if requested
	if ($Install) {
		Write-Header "Installing VST3 Plugin"
		if ($vst3Bundle) {
			$destPath = Join-Path $Vst3InstallDir $vst3Bundle.Name
			if (Test-Path $destPath) {
				Remove-Item -Recurse -Force $destPath
			}
			Copy-Item -Recurse -Force $vst3Bundle.FullName $Vst3InstallDir
			Write-Host "Installed to: $destPath" -ForegroundColor Green
		} else {
			Write-Host "WARNING: Could not find built VST3 bundle" -ForegroundColor Yellow
		}
	}

} finally {
	Pop-Location
}

# Open Ableton if requested
if ($OpenAbleton) {
	Write-Header "Launching Ableton Live"
	$abletonPath = Find-Ableton
	if ($abletonPath) {
		Write-Host "Starting: $abletonPath" -ForegroundColor Green
		Start-Process $abletonPath
	} else {
		Write-Host "Ableton Live not found in standard locations" -ForegroundColor Yellow
		Write-Host "Please start Ableton Live manually" -ForegroundColor Yellow
	}
}

Write-Header "Build Complete"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Install plugin:  .\build-vst3.ps1 -Install" -ForegroundColor White
Write-Host "  2. Launch Ableton:  .\build-vst3.ps1 -OpenAbleton" -ForegroundColor White
Write-Host "  3. Or both:         .\build-vst3.ps1 -Install -OpenAbleton" -ForegroundColor White
Write-Host ""

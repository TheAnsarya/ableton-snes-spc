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

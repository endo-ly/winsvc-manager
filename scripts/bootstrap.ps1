<#
.SYNOPSIS
    Bootstrap development dependencies for winsvc-manager.
.DESCRIPTION
    Downloads WinSW binary into tools/winsw/.
#>

param(
    [string]$WinSwVersion = "v3.0.0-alpha.11",
    [string]$ToolsDir = "$PSScriptRoot\..\tools\winsw",
    [string]$WinSwAssetName = "WinSW-net461.exe"
)

$ErrorActionPreference = "Stop"

$winswExe = Join-Path $ToolsDir "WinSW.exe"

if (Test-Path -LiteralPath $winswExe) {
    Write-Host "[bootstrap] WinSW already exists at $winswExe" -ForegroundColor Green
}
else {
    $url = "https://github.com/winsw/winsw/releases/download/$WinSwVersion/$WinSwAssetName"
    Write-Host "[bootstrap] Downloading WinSW $WinSwVersion ..." -ForegroundColor Cyan

    New-Item -ItemType Directory -Path $ToolsDir -Force | Out-Null

    try {
        Invoke-WebRequest -Uri $url -OutFile $winswExe -UseBasicParsing
        Write-Host "[bootstrap] WinSW downloaded to $winswExe" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to download WinSW: $_"
        exit 1
    }
}

Write-Host ""
Write-Host "[bootstrap] Setup complete!" -ForegroundColor Green
Write-Host "  WinSW: $winswExe"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  dotnet run --project src\Winsvc.Cli -- --help"

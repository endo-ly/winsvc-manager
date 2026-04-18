[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime,

    [string]$Version = "dev",

    [string]$Configuration = "Release",

    [string]$OutputRoot = (Join-Path $PSScriptRoot "..\artifacts")
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "src\Winsvc.Cli\Winsvc.Cli.csproj"
$resolvedOutputRoot = (New-Item -ItemType Directory -Force -Path $OutputRoot).FullName
$publishDir = Join-Path $resolvedOutputRoot "publish\$Runtime"
$packageBaseName = "winsvc-$Version-$Runtime"
$zipPath = Join-Path $resolvedOutputRoot "$packageBaseName.zip"
$hashPath = "$zipPath.sha256"
$assemblyVersion = if ($Version.StartsWith("v")) { $Version.Substring(1) } else { $Version }
$publishedExePath = Join-Path $publishDir "Winsvc.Cli.exe"
$renamedExePath = Join-Path $publishDir "winsvc.exe"
$bundledWinSwPath = Join-Path $publishDir "winsw.exe"
$winswSourcePath = Join-Path $repoRoot "tools\winsw\WinSW.exe"
$manifestSourceDir = Join-Path $repoRoot "manifests"
$manifestTargetDir = Join-Path $publishDir "manifests"

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path $hashPath) {
    Remove-Item -LiteralPath $hashPath -Force
}

$publishArgs = @(
    "publish"
    $projectPath
    "--configuration"
    $Configuration
    "--runtime"
    $Runtime
    "--self-contained"
    "true"
    "/p:PublishSingleFile=true"
    "/p:IncludeNativeLibrariesForSelfExtract=true"
    "/p:DebugType=None"
    "/p:DebugSymbols=false"
    "/p:Version=$assemblyVersion"
    "/p:InformationalVersion=$Version"
    "--output"
    $publishDir
)

& dotnet @publishArgs

if (-not (Test-Path -LiteralPath $publishDir)) {
    throw "Publish output directory was not created: $publishDir"
}

if (Test-Path -LiteralPath $publishedExePath) {
    Move-Item -LiteralPath $publishedExePath -Destination $renamedExePath -Force
}

if (-not (Test-Path -LiteralPath $renamedExePath)) {
    throw "Expected executable was not found: $renamedExePath"
}

if (-not (Test-Path -LiteralPath $winswSourcePath)) {
    throw "WinSW binary not found: $winswSourcePath. Run .\scripts\bootstrap.ps1 first."
}

Copy-Item -LiteralPath $winswSourcePath -Destination $bundledWinSwPath -Force

New-Item -ItemType Directory -Path $manifestTargetDir -Force | Out-Null
Get-ChildItem -LiteralPath $manifestSourceDir -Filter "*.template.y*ml" -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $manifestTargetDir $_.Name) -Force
}

$publishedFiles = Get-ChildItem -LiteralPath $publishDir
if ($publishedFiles.Count -eq 0) {
    throw "Publish output directory is empty: $publishDir"
}

Compress-Archive -Path $publishedFiles.FullName -DestinationPath $zipPath -CompressionLevel Optimal

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $zipPath)" | Set-Content -LiteralPath $hashPath -Encoding ascii

Write-Host "Created package: $zipPath"
Write-Host "Created checksum: $hashPath"

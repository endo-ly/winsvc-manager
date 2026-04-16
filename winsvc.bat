@echo off
set WINSVC_MANIFEST_DIR=%~dp0manifests
dotnet run --project "%~dp0src\Winsvc.Cli\Winsvc.Cli.csproj" -- %*

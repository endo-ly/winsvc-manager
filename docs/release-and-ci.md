# Release and CI

## Overview

このドキュメントは保守者向けです。
`winsvc-manager` の CI、GitHub Releases、ローカル配布物生成手順をまとめています。

## CI

GitHub Actions で、`main` への push と pull request ごとに以下を実行します。

- `dotnet restore`
- `dotnet build --configuration Release`
- `dotnet test --configuration Release`

Workflow:

- `.github/workflows/ci.yml`

## Release

タグ `v*` を push すると release workflow が実行され、`Winsvc.Cli` の自己完結型バイナリが GitHub Releases に添付されます。

生成対象:

- `win-x64`
- `win-arm64`

生成物:

- `winsvc-<tag>-win-x64.zip`
- `winsvc-<tag>-win-x64.zip.sha256`
- `winsvc-<tag>-win-arm64.zip`
- `winsvc-<tag>-win-arm64.zip.sha256`

Workflow:

- `.github/workflows/release.yml`

## Local Build

前提:

- Windows
- `.NET SDK 10.0.201`
- PowerShell

基本コマンド:

```powershell
dotnet restore winsvc-manager.sln
dotnet build winsvc-manager.sln --configuration Release -m:1
dotnet test winsvc-manager.sln --configuration Release -m:1
```

ローカルで release と同形式の配布物を生成する場合:

```powershell
.\scripts\publish-release.ps1 -Runtime win-x64 -Version v0.1.0
.\scripts\publish-release.ps1 -Runtime win-arm64 -Version v0.1.0
```

Script:

- `scripts/publish-release.ps1`

## Notes

- 配布 ZIP の中の実行ファイル名は `winsvc.exe`
- ZIP には `appsettings.json`（API 用既定設定）も含まれます
- Git tag は `v0.1.0` 形式を想定

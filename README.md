# winsvc-manager

Windows サービスおよび常駐プロセスを manifest 駆動で一元管理する C# CLI ツール。

## 概要

WinSW を実行器として利用し、YAML manifest からサービス定義を生成・管理します。
特定アプリ専用ではなく、manifest を追加することで任意のサービスを同じ方式で扱います。

## アーキテクチャ

```
manifests/*.yaml  →  winsvc render  →  WinSW XML + exe  →  Windows Service
                     winsvc install/start/stop/status/health
```

- **manifest** (`manifests/`): 人間が編集する真実のサービス定義
- **WinSW XML** (`C:\svc\services\`): manifest から生成されるデプロイ生成物
- **CLI** (`winsvc`): manifest を読み、WinSW を制御するインターフェース

## ディレクトリ構成

```
winsvc-manager/                  ← このリポジトリ (Git 管理)
  src/Winsvc.Cli/         ← C# CLI 本体
  manifests/                     ← ローカル manifest / template
  scripts/                       ← セットアップスクリプト
  docs/                          ← ドキュメント

C:\svc\                          ← 実運用デプロイ先 (Git 管理外)
  runtimes/                      ← Python ランタイム・venv
  services/                      ← WinSW exe + XML + ログ
  state/                         ← 状態ファイル
```

## クイックスタート

```powershell
# 事前準備: WinSW をダウンロード
.\scripts\bootstrap.ps1

# template をコピーして manifest を作成
Copy-Item manifests\service.template.yaml manifests\<service-id>.yaml

# CLI をビルド & ヘルプ表示
dotnet run --project src\Winsvc.Cli -- --help

# サービスを管理
dotnet run --project src\Winsvc.Cli -- render <service-id>
dotnet run --project src\Winsvc.Cli -- install <service-id>
dotnet run --project src\Winsvc.Cli -- start <service-id>
dotnet run --project src\Winsvc.Cli -- status <service-id>
dotnet run --project src\Winsvc.Cli -- health <service-id>
```

## 設計方針

- WinSW は実行器、C# CLI は制御面
- Git で管理するのは source と template。実 manifest はローカル管理、WinSW XML は生成物
- localhost バインドで始め、Tailscale Serve は後段で追加
- manifest ディレクトリと API bind は設定または環境変数で上書きできる

manifest の書式は [docs/manifest.md](docs/manifest.md) を参照。

# Architecture

## 概要

winsvc-manager は、manifest（YAML 設定ファイル）を唯一の設定源として Windows Service を管理するツールです。

2つの役割を持ちます。

1. **CLI からの操作** - WinSW（Windows Service をラップして管理しやすくする OSS ツール）と Windows SCM（Service Control Manager）を直接操作する
2. **API 経由の操作** - CLI の `api serve` からローカル API を起動し、HTTP 経由でサービスの状態参照と制御を提供する

プロジェクト全体は、クリーンアーキテクチャ風のレイヤー構成になっています。ビジネスロジックの抽象（インターフェース）を Core レイヤーに置き、副作用のある実装を Infrastructure レイヤーに閉じ込めることで、テスト可能性と保守性を高めています。

## データフロー

### 状態参照の流れ

```text
CLI または API
  → manifest を読み込む
  → Windows SCM からサービス状態を取得
  → manifest に定義された health URL を確認
  → 結果を呼び出し元に返す
```

`status` コマンドや `GET /services/{id}` は Windows SCM への問い合わせ結果を DTO（データを運ぶためのオブジェクト）として返します。`health` コマンドや `GET /services/{id}/health` は、manifest の `health.url` に HTTP リクエストを送り、応答があれば Healthy、タイムアウトやエラーなら Unhealthy と判定します。

### 制御の流れ（start / stop / restart）

```text
CLI または API
  → manifest を読み込む
  → WinSW XML を生成する
  → WinSW 実行ファイルを呼び出す
  → Windows SCM 上のサービスを操作する
```

CLI でも API でも、manifest を読み込んで WinSW の XML 設定を生成するところまでは共通です。その後、WinSW の実行ファイルをプロセスとして呼び出し、Windows SCM を通じてサービスの開始・停止・再起動を行います。

### install の流れ

install は CLI からのみ実行可能です。API には公開されていません。

```text
CLI
  → manifest を読み込む
  → WinSW XML を生成する
  → WinSW ラッパー実行ファイル（<id>-service.exe）が無ければ同梱の winsw.exe からコピー
  → WinSW を使ってサービスを登録する
```

install 時、manifest の `service.wrapperDir` に `<id>-service.exe` が存在しない場合、配布 ZIP に同梱されている `winsw.exe` をコピーしてラッパー実行ファイルを自動生成します。これにより、利用者は WinSW の配置を意識する必要がありません。

## レイヤー構成

```text
┌─────────────────────────────────────────────────────┐
│                   Winsvc.Cli                        │
│                 （エントリポイント）                    │
├─────────────────────────────────────────────────────┤
│                   Winsvc.Hosting                     │
│              （API エンドポイント、DI）                │
├──────────────────────┬──────────────────────────────┤
│     Winsvc.Core      │                              │
│  （抽象・検証ロジック） │   Winsvc.Infrastructure       │
│                      │   （具体実装・副作用）           │
├──────────────────────┴──────────────────────────────┤
│                Winsvc.Contracts                      │
│           （共有データ型・DTO）                        │
└─────────────────────────────────────────────────────┘
```

| レイヤー | プロジェクト | 役割 |
|---|---|---|
| エントリポイント | Winsvc.Cli | CLI コマンドの定義と実行。`api serve` による API 起動も担当 |
| ホスティング | Winsvc.Hosting | API エンドポイント定義、DI 登録、ASP.NET Core host 構築 |
| アプリケーション | Winsvc.Core | ビジネスロジックの抽象（インターフェース）と manifest 検証 |
| 契約 | Winsvc.Contracts | 共通データ型（manifest 型、API レスポンス型、DTO） |
| インフラストラクチャ | Winsvc.Infrastructure | 副作用のある実装（YAML 読み込み、WinSW 連携、サービス操作、HTTP ヘルスチェック） |

### 依存関係の方向

依存関係は内側に向かって注ぎます。

```text
Cli → Hosting → Core ← Infrastructure
                      ↑
         Contracts ← （全レイヤーから参照）
```

- **Cli** は Hosting に依存し、Hosting を通じて Core の抽象にアクセスする
- **Hosting** は Core の抽象に依存して DI に登録する
- **Infrastructure** は Core のインターフェースを実装する
- **Contracts** はどのレイヤーからも参照されるが、自身は何も依存しない

この方針により、Core は副作用を持たない純粋な抽象層として保たれます。

## コンポーネント詳細

### Contracts

Winsvc.Contracts は、レイヤー間で共有されるデータ型のみを含みます。ビジネスロジックや副作用は持ちません。

主な型:

| 型 | 分類 | 役割 |
|---|---|---|
| ServiceManifest | Manifest | 1つのサービス定義全体を表す |
| HealthConfig | Manifest | ヘルスチェックの URL とタイムアウト設定 |
| WindowsServiceInfo | ServiceState | Windows SCM から取得したサービス情報 |
| ServiceState | ServiceState | サービスの状態を表す列挙型（Stopped, Running など） |
| HealthState | ServiceState | ヘルスチェック結果を表す列挙型（Healthy, Unhealthy） |
| ApiInfoResponse | API | API の疎通確認用レスポンス |
| ManagedServiceResponse | API | 管理対象サービス一覧の各項目 |

### Core

Winsvc.Core は、アプリケーションの境界を定めるレイヤーです。インターフェースの定義と、manifest の検証ロジックを持ちます。実装の詳細は Infrastructure に委ねます。

主なインターフェース:

| インターフェース | 役割 |
|---|---|
| IManifestReader | manifest ファイルの読み込み |
| IManifestProvider | API が参照する manifest 一覧・詳細の取得とキャッシュ境界 |
| IManifestValidator | manifest の妥当性検証 |
| IServiceConfigGenerator | WinSW 用 XML の生成 |
| IServiceManager | サービスの操作（install, uninstall, start, stop, restart） |
| IWindowsServiceMonitor | Windows サービス状態の監視 |
| IHealthChecker | HTTP ヘルスチェック |

ManifestValidator は、必須フィールドの有無や値の妥当性を検証します。API の `GET /services/managed` は、この検証を通過した manifest のみを返します。

### Infrastructure

Winsvc.Infrastructure は、Core のインターフェースに対する具体実装です。ファイル I/O、プロセス実行、Windows API 呼び出し、HTTP 通信といった副作用をすべてここに閉じ込めます。

実装一覧:

| クラス | 対応インターフェース | 副作用の内容 |
|---|---|---|
| YamlManifestReader | IManifestReader | YAML ファイルの読み込み（YamlDotNet 使用） |
| FileSystemWatcherManifestProvider | IManifestProvider / IHostedService | 起動時に manifest を読み込み、FileSystemWatcher で追加・変更・削除を検知してキャッシュを更新 |
| ManifestValidator | IManifestValidator | manifest のバリデーション |
| WinSwXmlGenerator | IServiceConfigGenerator | WinSW 用 XML 文字列の生成 |
| WinSwServiceManager | IServiceManager | WinSW 実行ファイルのプロセス起動と操作 |
| WindowsServiceMonitor | IWindowsServiceMonitor | Windows SCM への問い合わせ |
| HttpClientHealthChecker | IHealthChecker | HTTP リクエストによるヘルスチェック |

### Hosting

Winsvc.Hosting は、ASP.NET Core Minimal APIs でエンドポイントを定義し、DI コンテナへのサービス登録を行います。
API の manifest 参照は `IManifestProvider` に集約されます。既定では `FileSystemWatcherManifestProvider` が `IHostedService` として起動し、manifest ディレクトリを監視してメモリ上のキャッシュを更新します。

現在のエンドポイント:

| Method | Path | 説明 |
|---|---|---|
| GET | `/` | API の疎通確認 |
| GET | `/services/windows` | Windows 登録済みサービス一覧 |
| GET | `/services/managed` | 管理対象サービス一覧 |
| GET | `/services/{id}` | サービス詳細 |
| GET | `/services/{id}/health` | ヘルスチェック |
| POST | `/services/{id}/start` | サービス起動 |
| POST | `/services/{id}/stop` | サービス停止 |
| POST | `/services/{id}/restart` | サービス再起動 |

**注意:** install, uninstall, render は API に公開されていません。これらの操作は CLI からのみ実行できます。

### Cli

Winsvc.Cli は、System.CommandLine を使った CLI の定義と、全コマンドのハンドラー実装を持ちます。

`api serve` サブコマンドは、Winsvc.Hosting を利用して ASP.NET Core の WebHost を構築し、ローカル API を起動します。これにより、CLI と API で同じビジネスロジックとインフラ実装を共有しています。

## リポジトリとランタイムの分離

Git 管理対象とランタイム生成物は明確に分離しています。

**Git 管理対象:**
- ソースコード（`src/`）
- manifest テンプレート（`manifests/service.template.yaml`）
- スクリプト（`scripts/`）
- ドキュメント（`docs/`）

**ランタイム生成物（Git 管理外）:**
- WinSW XML（manifest から自動生成）
- WinSW ラッパー実行ファイル（`winsw.exe` からコピー）
- ログファイル

これらの生成物はすべて manifest から自動生成されるため、リポジトリにコミットする必要がありません。実 manifest（`.yaml` ファイル）も `.gitignore` で除外されており、テンプレートのみがコミットされます。

## 設定の優先順位

設定値は、CLI 引数、環境変数、appsettings.json の順で上書きされます。上位の設定源が優先されます。

```text
CLI 引数 > 環境変数 > appsettings.json
```

主要な設定キー:

| 設定項目 | CLI 引数 | 環境変数 | appsettings.json |
|---|---|---|---|
| API URL | `--urls` | `Winsvc__Api__Urls` | `Winsvc:Api:Urls` |
| Manifest ディレクトリ | `--manifest-dir` | `Winsvc__ManifestDirectory` | `Winsvc:ManifestDirectory` |
| Manifest hot reload | - | `Winsvc__ManifestHotReload` | `Winsvc:ManifestHotReload` |

環境変数の区切り文字に `__`（二重アンダースコア）を使っているのは、ASP.NET Core の規約です。`appsettings.json` ではコロン区切り（`Winsvc:Api:Urls`）で同じ設定を表します。

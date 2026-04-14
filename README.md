# winsvc-manager

## Overview

`winsvc-manager` は、YAML manifest をもとに Windows Service を管理するための .NET ツールです。
manifest から WinSW 用設定を生成し、`winsvc` コマンドで service の `install` / `uninstall` / `start` / `stop` / `restart` / `status` / `health` を扱えます。

このリポジトリには次が含まれます。

- 利用者向け CLI（ローカル API 起動機能を含む）
- manifest テンプレート
- 開発用スクリプト

配布物は GitHub Releases から取得できます。

## Quick Start

1. GitHub Releases から利用環境に合う ZIP を取得して展開します
2. 展開先の `winsvc.exe` を使える場所に置きます
3. `manifests/service.template.yaml` をコピーして manifest を作成します
4. `winsvc render <service-id>` で内容を確認します

例:

```powershell
Copy-Item manifests\service.template.yaml manifests\sample-service.yaml
winsvc --help
winsvc render sample-service
winsvc install sample-service
winsvc start sample-service
winsvc status sample-service
```

`winsvc` コマンドとして実行するには、`winsvc.exe` が `PATH` の通った場所にある必要があります。
まだ `PATH` に入れていない場合は、そのディレクトリで `.\winsvc.exe` を使ってください。

## Distribution

GitHub Releases では次の配布物を提供します。

- `winsvc-<tag>-win-x64.zip`
- `winsvc-<tag>-win-x64.zip.sha256`
- `winsvc-<tag>-win-arm64.zip`
- `winsvc-<tag>-win-arm64.zip.sha256`

ZIP を展開すると `winsvc.exe` と `appsettings.json` が入っています。

必要であれば SHA256 を照合してください。

```powershell
Get-FileHash .\winsvc-v0.1.0-win-x64.zip -Algorithm SHA256
Get-Content .\winsvc-v0.1.0-win-x64.zip.sha256
```

## Commands

`winsvc` のコマンド一覧です。

| Command | 引数 | 説明 |
| --- | --- | --- |
| `winsvc --help` | なし | 利用可能なコマンドと引数を表示します。 |
| `winsvc render <service-id>` | `service-id`: manifest の ID | 指定 service の manifest を読み込み、生成される WinSW XML を標準出力に表示します。実際の install は行いません。 |
| `winsvc install <service-id>` | `service-id`: manifest の ID | manifest を読み込み、WinSW XML を生成したうえで対象 service を install します。 |
| `winsvc uninstall <service-id>` | `service-id`: manifest の ID | 指定 service を uninstall します。manifest が必要です。 |
| `winsvc start <service-id>` | `service-id`: manifest の ID | 指定 service を起動します。 |
| `winsvc stop <service-id>` | `service-id`: manifest の ID | 指定 service を停止します。 |
| `winsvc restart <service-id>` | `service-id`: manifest の ID | 指定 service を再起動します。 |
| `winsvc status <service-id>` | `service-id`: Windows Service 名 | Windows Service Control Manager 上の状態を確認します。manifest がなくても問い合わせできます。 |
| `winsvc health <service-id>` | `service-id`: manifest の ID | manifest に定義された health URL に対して HTTP ヘルスチェックを実行します。 |
| `winsvc show <service-id>` | `service-id`: manifest の ID | 読み込まれる manifest ファイルの生内容をそのまま表示します。 |
| `winsvc list managed` | なし | `manifests/` 配下の manifest を列挙し、各 service の状態を表示します。 |
| `winsvc list windows` | なし | Windows に登録されている全 service を列挙します。 |
| `winsvc api serve` | `--urls`: リッスン URL<br>`--manifest-dir`: マニフェストディレクトリ | ローカル API サーバーを起動します。既定では `http://127.0.0.1:8011` でリッスンします。 |

補足:

- `install` / `uninstall` / `start` / `stop` / `restart` / `render` / `health` / `show` は manifest を参照します
- manifest が見つからない場合はエラーを標準エラー出力に表示します
- `list managed` は読み込み失敗した manifest に対して `failed to read <path>` を表示します

ソースコードから直接実行する場合:

```powershell
dotnet run --project src\Winsvc.Cli -- --help
```

## API

`winsvc api serve` でローカル API を起動します。

```powershell
winsvc api serve
winsvc api serve --urls http://localhost:9011
winsvc api serve --manifest-dir ./my-services
```

URL や manifest ディレクトリは `appsettings.json` の `Winsvc:Api:Urls` / `Winsvc:ManifestDirectory`、または環境変数 `Winsvc__Api__Urls` / `Winsvc__ManifestDirectory` でも設定できます。
CLI 引数はこれらを上書きします。

`appsettings.json` は `winsvc.exe` と同じディレクトリに配置してください。配布 ZIP に含まれているものをそのまま使えます。
独自の設定にする場合は内容を書き換えるか、環境変数・CLI 引数で上書きしてください。

API 一覧:

| Method | Path | 説明 |
| --- | --- | --- |
| `GET` | `/` | API の疎通確認です。 |
| `GET` | `/services/windows` | Windows に登録されている service 一覧を返します。 |
| `GET` | `/services/managed` | manifest と Windows Service 状態を突き合わせて、管理対象 service 一覧を返します。 |
| `GET` | `/services/{id}` | 指定 service の詳細情報を返します。manifest が見つからない場合は `404` です。 |
| `GET` | `/services/{id}/health` | 指定 service の health URL に対してヘルスチェックを行います。manifest が見つからない場合は `404` です。 |
| `POST` | `/services/{id}/start` | 指定 service の起動を要求します。manifest が見つからない場合は `404` です。 |
| `POST` | `/services/{id}/stop` | 指定 service の停止を要求します。manifest が見つからない場合は `404` です。 |
| `POST` | `/services/{id}/restart` | 指定 service の再起動を要求します。manifest が見つからない場合は `404` です。 |

補足:

- `GET /services/managed` と `GET /services/{id}` は、manifest の妥当性検証に失敗した service を返しません
- `404` のレスポンスは `{ "error": "..." }` 形式です
- `POST` 系 endpoint は要求受理時点で `status: "queued"` を返します

## Manifest

manifest は `manifests/` 配下に配置します。

```powershell
Copy-Item manifests\service.template.yaml manifests\<service-id>.yaml
```

manifest の仕様は [docs/manifest.md](docs/manifest.md) を参照してください。

## Components

- `src/Winsvc.Cli`
  manifest の読み込み、WinSW 設定生成、サービス操作を行う CLI。`api serve` でローカル API も起動可能。
- `src/Winsvc.Hosting`
  API endpoint 定義、DI 登録、host 構築の共有ロジック。Cli から利用されます。
- `src/Winsvc.Core`
  アプリケーション層の抽象と検証ロジック
- `src/Winsvc.Contracts`
  共有 DTO、manifest 型、API レスポンス型
- `src/Winsvc.Infrastructure`
  YAML 読み込み、WinSW 連携、Windows Service 操作、HTTP ヘルスチェック
- `manifests/`
  manifest のテンプレートと定義ファイル

## Development

開発者向けの build / test / CI / release 情報は [docs/release-and-ci.md](docs/release-and-ci.md) を参照してください。

# Manifest

## Overview

manifest は `winsvc-manager` の正本です。

- 管理対象サービスの起動方法
- WinSW 配置先
- 環境変数
- health check
- Tailscale Serve 公開設定

実運用の manifest はローカル管理とし、Git には入れません。
repository には template だけを置きます。

## Location

- 実 manifest: `manifests/<service-id>.yaml`
- template: `manifests/*.template.yaml`

manifest ディレクトリは次の順で解決します。

1. `Winsvc:ManifestDirectory`
2. 環境変数 `WINSVC_MANIFEST_DIR`
3. `./manifests`

`.template.yaml` / `.template.yml` は CLI / API の読み込み対象外です。

## Workflow

1. `manifests/service.template.yaml` をコピーする
2. ファイル名を `<service-id>.yaml` にする
3. 各項目を実環境に合わせて埋める
4. `dotnet run --project src\Winsvc.Cli -- render <service-id>` で確認する

## Schema

### Root

- `id`: サービス識別子。Windows Service 名にも使う
- `type`: 現在は `managed` を想定
- `displayName`: 表示名
- `description`: 説明
- `runtime`: 起動対象の実体
- `service`: WinSW 側の設定
- `env`: 実行時環境変数
- `health`: HTTP health check 設定
- `exposure`: 外部公開設定

### runtime

- `workDir`: プロセスの作業ディレクトリ
- `executable`: 実行ファイルの絶対パス
- `arguments`: 引数配列

### service

- `wrapperDir`: WinSW exe / XML / log を置くディレクトリ
- `startMode`: 例 `auto`, `delayed-auto`, `manual`
- `onFailure`: 障害時動作
- `resetFailure`: 障害カウントのリセット間隔

### env

キーと値の辞書です。API key や bind address を含めてよいですが、実 manifest は Git に入れません。

### health

- `url`: health endpoint
- `timeoutSec`: タイムアウト秒

### exposure.tailscaleServe

- `enabled`: `true` のとき公開設定を有効化
- `httpsPort`: 公開する HTTPS port
- `target`: Serve の転送先

`enabled: true` の場合は `httpsPort` と `target` が必須です。

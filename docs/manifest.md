# Manifest 仕様

## 概要

manifest は winsvc-manager の唯一の設定源（Single Source of Truth）です。管理したい Windows Service ごとに 1 つの YAML ファイルを用意します。

実運用の manifest は Git にコミットしません。リポジトリには template だけを置きます。API キーやパスワードなどの機密情報が含まれる可能性があるためです。

## ファイルの配置

### 配置先

manifest は `manifests/` ディレクトリに配置します。

### 命名規則

ファイル名は `<service-id>.yaml` です。ファイル名（拡張子を除く）がそのまま service-id になります。

```
manifests/
  my-app.yaml          # service-id: my-app
  api-gateway.yaml     # service-id: api-gateway
  batch-worker.yaml    # service-id: batch-worker
```

### テンプレート

`*.template.yaml` / `*.template.yml` は CLI / API の読み込み対象外です。テンプレートとしてだけ利用され、サービス一覧には表示されません。

### ディレクトリの解決順序

manifest ディレクトリは次の順序で探します。上位の設定が見つかれば、それを使います。

| 優先度 | 設定方法 | 具体例 |
|---|---|---|
| 1 | CLI 引数（`--manifest-dir`） | `--manifest-dir C:\svc\manifests` |
| 2 | 設定ファイル（appsettings.json）の `Winsvc:ManifestDirectory` | `"C:\\svc\\manifests"` |
| 3 | 環境変数 `WINSVC_MANIFEST_DIR` | `C:\svc\manifests` |
| 4 | カレントディレクトリの `./manifests` | 実行位置からの相対パス |

## 作成手順

1. `manifests/service.template.yaml` をコピーする
2. ファイル名を `<service-id>.yaml` に変更する
3. 各項目を実環境に合わせて編集する
4. `winsvc render <service-id>` で生成される内容を確認する

```powershell
# 例: my-app というサービスを追加する
Copy-Item manifests\service.template.yaml manifests\my-app.yaml
# エディタで manifests\my-app.yaml を編集
winsvc render my-app
```

## スキーマ

### ルートフィールド

| フィールド | 型 | 必須 | デフォルト | 説明 |
|---|---|---|---|---|
| `id` | string | **必須** | `""` | サービス識別子。Windows Service 名としても使用される |
| `type` | string | 任意 | `"managed"` | サービスタイプ（現在は `managed` のみ） |
| `displayName` | string | **必須** | `""` | Windows Service Manager に表示される名前 |
| `description` | string | 任意 | `""` | サービスの説明 |
| `runtime` | object | **必須** | - | 実行するプロセスの定義（[runtime](#runtime) 参照） |
| `service` | object | **必須** | - | WinSW の設定（[service](#service) 参照） |
| `env` | map | 任意 | `{}` | プロセスに渡す環境変数（キー: 値） |
| `health` | object | **必須** | - | ヘルスチェック設定（[health](#health) 参照） |
| `exposure` | object | 任意 | - | 外部公開設定（[exposure](#exposure) 参照） |

### runtime

起動するプロセス（アプリケーション本体）の定義です。`executable` には実際のアプリケーションを指定します。WinSW ラッパー実行ファイル（`<id>-service.exe`）はインストール時に自動生成されるため、ここには書きません。

| フィールド | 型 | 必須 | デフォルト | 説明 |
|---|---|---|---|---|
| `workDir` | string | **必須** | `""` | プロセスの作業ディレクトリ |
| `executable` | string | **必須** | `""` | 実行ファイルのパス（WinSW ラッパーではない） |
| `arguments` | string[] | 任意 | `[]` | コマンドライン引数 |

```yaml
runtime:
  workDir: C:\svc\runtimes\my-app\current
  executable: C:\svc\runtimes\my-app\current\run.cmd
  arguments:
    - "--port"
    - "9000"
    - "--verbose"
```

### service

WinSW の配置と動作に関する設定です。`wrapperDir` には WinSW が生成するファイル群をまとめるディレクトリを指定します。

| フィールド | 型 | 必須 | デフォルト | 説明 |
|---|---|---|---|---|
| `wrapperDir` | string | **必須** | `""` | WinSW ラッパーファイル（exe, xml, log）の配置先 |
| `startMode` | string | 任意 | `"delayed-auto"` | 起動モード（下記参照） |
| `onFailure` | string | 任意 | `"restart"` | 障害時の動作（現在は `restart` のみ対応） |
| `resetFailure` | string | 任意 | `"1 hour"` | 障害カウントのリセット間隔 |

**startMode の値と意味:**

| 値 | 意味 |
|---|---|
| `auto` | OS 起動時に自動開始 |
| `delayed-auto` | OS 起動後に遅延して自動開始（推奨） |
| `manual` | 手動起動のみ |

### env

プロセスに渡す環境変数をキーと値のペアで指定します。

- 値はすべて文字列として渡されるため、数値でも引用符で囲んでください（例: `"9000"`）
- API キーやバインドアドレスなど、実行時に必要な変数を設定する
- 実運用の manifest は Git にコミットしないため、機密情報を含めてもよい

```yaml
env:
  MY_APP_HOST: 127.0.0.1
  MY_APP_PORT: "9000"
  MY_APP_API_KEY: "sk-xxxxxxxxxxxxxxxx"
```

### health

プロセスの稼働確認用 HTTP エンドポイントの設定です。`winsvc health <service-id>` コマンドや API の `/services/{id}/health` で使用されます。

| フィールド | 型 | 必須 | デフォルト | 説明 |
|---|---|---|---|---|
| `url` | string | **必須** | `""` | ヘルスチェック URL |
| `timeoutSec` | int | 任意 | `5` | タイムアウト秒（0 より大きい値） |

```yaml
health:
  url: http://127.0.0.1:9000/health
  timeoutSec: 5
```

### exposure

Tailscale Serve（Tailscale ネットワーク経由でローカルサービスを外部に公開する機能）を使った外部公開設定です。

#### exposure.tailscaleServe

| フィールド | 型 | 必須 | デフォルト | 説明 |
|---|---|---|---|---|
| `enabled` | bool | 任意 | `false` | Tailscale Serve による外部公開を有効にする |
| `httpsPort` | int | 条件付き | `0` | 公開する HTTPS ポート（`enabled: true` のとき必須、0 より大きい値） |
| `target` | string | 条件付き | `""` | Serve の転送先（`enabled: true` のとき必須） |

`enabled: true` のとき、`httpsPort` と `target` が必須になります。

```yaml
# 公開しない場合（デフォルト）
exposure:
  tailscaleServe:
    enabled: false
    httpsPort: 0
    target: http://127.0.0.1:9000

# 公開する場合
exposure:
  tailscaleServe:
    enabled: true
    httpsPort: 443
    target: http://127.0.0.1:9000
```

## 完全な例

テンプレート（`manifests/service.template.yaml`）をベースにした、コメント付きの完全な manifest です。

```yaml
# ファイル名は "<service-id>.yaml" にする
# 例: "my-app.yaml" なら service-id は "my-app"

id: my-app
type: managed
displayName: My Application
description: サンプルアプリケーション

runtime:
  # アプリケーションの作業ディレクトリ
  workDir: C:\svc\runtimes\my-app\current
  # アプリケーション本体のパス（WinSW ラッパーではない）
  executable: C:\svc\runtimes\my-app\current\run.cmd
  # コマンドライン引数
  arguments: []

service:
  # WinSW ラッパー（exe, xml, log）の配置先
  # インストール時に <id>-service.exe が自動生成される
  wrapperDir: C:\svc\services\my-app
  # OS 起動時の動作: auto / delayed-auto（推奨） / manual
  startMode: delayed-auto
  # プロセス異常終了時の動作
  onFailure: restart
  # 障害カウントをリセットする間隔
  resetFailure: 1 hour

env:
  MY_APP_HOST: 127.0.0.1
  # 数値は引用符で囲む
  MY_APP_PORT: "9000"
  # 機密情報を含めてもよい（manifest は Git にコミットしない）
  MY_APP_API_KEY: "<set-later>"

health:
  # ヘルスチェック用エンドポイント
  url: http://127.0.0.1:9000/health
  timeoutSec: 5

exposure:
  tailscaleServe:
    # false のままなら httpsPort と target は使用されない
    enabled: false
    httpsPort: 0
    target: http://127.0.0.1:9000
```

## よくある質問

### env の値で数値を渡したい場合は？

値はすべて文字列としてプロセスに渡されます。ポート番号など数値を渡す場合は引用符で囲んでください。

```yaml
# OK
MY_APP_PORT: "9000"

# NG（YAML パーサーが数値として解釈する可能性がある）
MY_APP_PORT: 9000
```

### manifest の変更を反映するには？

`winsvc api serve` の起動中は、manifest ファイルの追加・変更・削除が自動検出されます。API の `/services/managed`、`/services/{id}`、`/services/{id}/health`、start / stop / restart は、再起動なしで更新後の manifest キャッシュを参照します。`*.template.yaml` / `*.template.yml` は監視対象から除外されます。

この自動反映は API の読み取り・操作に使う manifest キャッシュの更新です。すでに Windows Service としてインストール済みの WinSW XML は自動では書き換えません。`runtime`、`env`、`service.startMode` など、インストール時に生成されるサービス設定を Windows Service 側へ反映する場合は、従来どおり `winsvc reinstall <service-id>` を実行してください。変更前に `winsvc render <service-id>` で生成される内容を確認できます。

hot reload は既定で有効です。無効化する場合は `appsettings.json` の `Winsvc:ManifestHotReload`、または環境変数 `Winsvc__ManifestHotReload=false` を設定します。

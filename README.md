# winsvc-manager

YAML で書いた設定ファイル（manifest）をもとに、Windows Service をコマンド一つでインストール・起動・監視できる CLI / APIツール。

## Why winsvc-manager

自作ツールや外部リポジトリのプログラムをWindows PC起動時に自動起動させたいとき、主に３つの手段があるが、それぞれ以下の課題がある。

**タスクスケジューラ**
- GUI 操作が中心で、設定変更のたびに画面を開いて手順を踏む必要があり面倒

**services.msc / `sc.exe`**
- `ServiceMain` を持たないプログラム（Node.js、Python スクリプト、バッチファイルなど）はサービス化できない

**[WinSW](https://github.com/winsw/winsw)（直接使用）**
- 任意の実行ファイルを Windows Service として動かせるが、サービスごとに XML を手書きし、WinSW バイナリを手動で配置する必要がある
- CLI はあるが、API 経由で外部システムやAIエージェントから扱うための標準導線はない

winsvc-manager は manifest（YAML）でサービス定義を管理し、WinSW のサービス化を自動化しつつ、  API で運用可能にする。  

- **manifest（YAML）駆動** - 実行ファイル、環境変数、ヘルスチェックURL などを宣言的に管理
- **何でもサービス化** - ServiceMain を持たない任意の実行ファイルを Windows Service として動かす
- **コマンド一発で登録** - `winsvc install`でサービス登録を実行（startMode が auto / delayed-auto    なら起動時自動起動）
- **AI エージェント運用に適合** - API利用方法をSkill化することで、「何のWindows Service が稼働中か」「管理対象サービスが正常応答しているか」などのチェックをAIが可能になる

## Getting Started

前提: Windows / 管理者権限

### 1. ダウンロード・展開

[GitHub Releases](https://github.com/endo-ly/winsvc-manager/releases) から環境に合う ZIP をダウンロードして展開。（一般的なWindowsPCなら `winsvc-v0.1.0-win-x64.zip`）

展開後のファイル:

| ファイル | 説明 |
|---|---|
| `winsvc.exe` | CLI ツール本体 |
| `winsw.exe` | WinSW（Windows Service をラップして管理しやすくする OSS ツール） |
| `appsettings.json` | API サーバーの設定ファイル |
| `manifests/service.template.yaml` | manifest テンプレート |

PATH の通った場所に置くとどこからでも `winsvc` を使える。PATH に入っていなければ、同じディレクトリで `.\winsvc.exe` とする。

### 2. manifest を作る

テンプレートをコピーして、サービスの定義を書く。

```powershell
Copy-Item manifests\service.template.yaml manifests\my-app.yaml
```

`my-app.yaml` のサービス名、実行ファイルのパス、ヘルスチェック URL などを編集する。設定できる項目の詳細は [Manifest 仕様](docs/manifest.md) を参照。

`my-app.yaml` のファイル名（拡張子を除く）が **service-id** になる。以降のコマンドはこの service-id で対象を指定する。

### 3. 確認・インストール・起動

```powershell
# インストール
winsvc install my-app

# 起動
winsvc start my-app

# 状態確認
winsvc status my-app

# ヘルスチェック（manifest に health.url を設定している場合）
winsvc health my-app
```

`install` 時、WinSW ラッパー実行ファイル（`<id>-service.exe`）が無ければ同梱の `winsw.exe` から自動コピーされる。

## コマンドリファレンス

| コマンド | 説明 |
|---|---|
| `winsvc --help` | ヘルプを表示 |
| `winsvc render <service-id>` | WinSW XML を生成して表示（インストールはしない） |
| `winsvc install <service-id>` | サービスをインストール |
| `winsvc uninstall <service-id>` | サービスをアンインストール |
| `winsvc start <service-id>` | サービスを起動 |
| `winsvc stop <service-id>` | サービスを停止 |
| `winsvc restart <service-id>` | サービスを再起動 |
| `winsvc status <service-id>` | サービスの OS 上の状態を確認（manifest 不要） |
| `winsvc health <service-id>` | HTTP ヘルスチェックを実行 |
| `winsvc show <service-id>` | manifest の内容をそのまま表示 |
| `winsvc list managed` | manifest 配下のサービスを一覧表示 |
| `winsvc list windows` | Windows に登録されている全サービスを一覧表示 |
| `winsvc api serve` | ローカル API サーバーを起動 |

**`status` は manifest 不要:** Windows Service 名で直接問い合わせるため、winsvc-manager 経由でインストールしていないサービスも確認できる。

**ソースコードから実行:**

```powershell
dotnet run --project src\Winsvc.Cli -- --help
```

## API リファレンス

`winsvc api serve` でローカル API を起動。既定は `http://127.0.0.1:8011`。

```powershell
# 既定のポートで起動
winsvc api serve

# ポート・manifest ディレクトリを指定
winsvc api serve --urls http://localhost:9011 --manifest-dir ./my-services
```

設定は CLI 引数 > 環境変数 > `appsettings.json` の順で優先される。
API 起動中は manifest の追加・変更・削除が自動検出され、`/services/managed` や `/services/{id}` の応答に反映されます。既定では有効です。無効化する場合は `appsettings.json` または環境変数で `Winsvc:ManifestHotReload` / `Winsvc__ManifestHotReload` を `false` にしてください。

### エンドポイント一覧

| メソッド | パス | 説明 |
|---|---|---|
| `GET` | `/` | 疎通確認 |
| `GET` | `/services/windows` | Windows に登録されているサービス一覧 |
| `GET` | `/services/managed` | 管理対象サービス一覧（manifest + 状態） |
| `GET` | `/services/{id}` | サービス詳細 |
| `GET` | `/services/{id}/health` | ヘルスチェック |
| `POST` | `/services/{id}/start` | サービス起動 |
| `POST` | `/services/{id}/stop` | サービス停止 |
| `POST` | `/services/{id}/restart` | サービス再起動 |

- 404 の場合は `{ "error": "..." }` を返す
- POST 系は要求受理時点で `{ "id": "...", "action": "start", "status": "queued" }` を返す（操作の完了を保証するものではない）
- manifest の検証に失敗したサービスは `/services/managed` と `/services/{id}` に含まれない

## プロジェクト構成

```text
src/
  Winsvc.Cli/           CLI エントリポイント
  Winsvc.Hosting/        API エンドポイント・DI
  Winsvc.Core/           インターフェースと検証
  Winsvc.Contracts/      共通データ型
  Winsvc.Infrastructure/ 具体実装
manifests/               manifest テンプレート
scripts/                 開発用スクリプト
```

## ドキュメント一覧

| ドキュメント | 内容 |
|---|---|
| [Manifest 仕様](docs/manifest.md) | manifest の全フィールド、型、必須/任意、デフォルト値 |
| [Architecture](docs/architecture.md) | アーキテクチャ、データフロー、コンポーネント構成 |
| [Development](docs/development.md) | ビルド、テスト、CI、リリース手順 |

## Agent Skills Support

`.claude/skills/winsvc-manager-api/` にAIエージェント向けのSkillを同封。AIが winsvc-managerのAPIを使ってWindowsサービスの状態確認・起動・停止などの操作を実行できる。

## ライセンス

MIT License. Copyright (c) 2026 endo-ly

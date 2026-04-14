# Architecture

## Overview

`winsvc-manager` は、manifest を唯一の設定源として Windows Service を管理する repository です。

実装済みの役割は大きく 2 つ。
- CLI から WinSW と Windows SCM を操作する
- CLI の `api serve` サブコマンドからローカル API を起動し、状態参照と最小限の制御を提供する

## Data Flow

基本フローは次のとおり。

```text
manifests/*.yaml
  -> manifest reader / validator
  -> WinSW XML generator
  -> WinSW service wrapper
  -> Windows Service Control Manager

CLI（api serve 経由）
  -> Core abstractions
  -> Infrastructure implementations
  -> status / health / start / stop / restart
```

状態参照系の流れ:
1. CLI または API が service-id を受け取る
2. manifest を読む
3. Windows SCM からサービス状態を取る
4. manifest 定義の health URL を叩く
5. 呼び出し元へ DTO として返す

制御系の流れ:
1. CLI または API が service-id を受け取る
2. manifest を読む
3. WinSW 実行ファイルを呼び出す
4. Windows SCM 上のサービスを開始・停止・再起動する

## Repository Layout

現在 solution に入っている主なプロジェクト:
- `Winsvc.Contracts`
  共有 DTO、manifest 型、API レスポンス型
- `Winsvc.Core`
  抽象インターフェースと validation
- `Winsvc.Infrastructure`
  YAML 読み込み、WinSW XML 生成、WinSW 実行、Windows Service 参照、HTTP health check
- `Winsvc.Hosting`
  API endpoint 定義、DI 登録、ASP.NET Core host 構築の共有ロジック
- `Winsvc.Cli`
  オペレーター向け CLI。CLI 操作と `api serve` によるローカル API 起動の両方を担う。
- `Winsvc.Core.Tests`
  現在の単体テスト

## Runtime Layout

Git 管理対象と runtime 配置先は分離しています。

repository 側:
- source code
- manifest
- template
- setup script
- docs

runtime 側:
- `runtimes/<service-id>/`
- `services/<service-id>/`
- `state/<service-id>/`

この方針により、WinSW XML や wrapper 配置物は生成物として扱います。

## Component Roles

### Contracts

`Winsvc.Contracts` は共有モデルだけを持ちます。

代表例:
- `ServiceManifest`
- `HealthConfig`
- `WindowsServiceInfo`
- `ServiceState`
- `HealthState`
- `ApiInfoResponse`、`ManagedServiceResponse` など API レスポンス型

### Core

`Winsvc.Core` はアプリケーション境界です。

主な抽象:
- `IManifestReader`
- `IManifestValidator`
- `IServiceConfigGenerator`
- `IServiceManager`
- `IWindowsServiceMonitor`
- `IHealthChecker`

CLI と Hosting はこの抽象に依存し、具体実装は Infrastructure へ寄せています。

### Infrastructure

`Winsvc.Infrastructure` は副作用を持つ実装層です。

現在の実装:
- `YamlManifestReader`
- `WinSwXmlGenerator`
- `WinSwServiceManager`
- `WindowsServiceMonitor`
- `HttpClientHealthChecker`

ここがファイル I/O、プロセス実行、Windows Service 参照、HTTP 呼び出しを引き受けます。

### CLI

`Winsvc.Cli` は operator-facing な制御面です。

現在のコマンド:
- `render`
- `install`
- `uninstall`
- `start`
- `stop`
- `restart`
- `status`
- `health`
- `show`
- `list windows`
- `list managed`
- `api serve`

### Hosting

`Winsvc.Hosting` は API endpoint 定義と ASP.NET Core host 構築の共有ロジックです。

`Winsvc.Cli` の `api serve` サブコマンドから利用されます。

bind 先は `appsettings.json` の `Winsvc:Api:Urls`、環境変数 `Winsvc__Api__Urls`、または CLI 引数 `--urls` で設定します。

現在のエンドポイント:
- `GET /services/windows`
- `GET /services/managed`
- `GET /services/{id}`
- `GET /services/{id}/health`
- `POST /services/{id}/start`
- `POST /services/{id}/stop`
- `POST /services/{id}/restart`

現在 `install`、`uninstall`、`render` は API に出していません。

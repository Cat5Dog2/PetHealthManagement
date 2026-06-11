# うちの子健康カルテ

ペットの健康記録、予定、通院履歴を管理するWebアプリです。ASP.NET Core MVC、Identity、EF Core、SQL Server を使用しています。

## 前提環境

- `.NET SDK 10.0.103` 以上（`global.json` に追従）
- SQL Server LocalDB（Windows の開発環境で使用）

## クイックスタート

```bash
# build
./scripts/build.sh

# test
./scripts/test.sh

# critical CI subset
./scripts/test-critical.sh

# format check
./scripts/format.sh
```

Windows PowerShell では `./scripts/*.ps1` を使います。必要に応じて `-ExecutionPolicy Bypass` を付けて実行してください。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-critical.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/format.ps1
```

## 起動方法

```bash
dotnet run --project src/PetHealthManagement.Web --launch-profile https
```

HTTPS 開発証明書が未準備の場合は、先に次を実行してください。

```bash
dotnet dev-certs https --trust
```

PowerShell では、次の補助スクリプトも使えます。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev-certs.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev-certs.ps1 -Trust
```

## 環境別設定

- `appsettings.json` は共通の既定値だけを持ちます
- `appsettings.Development.json` は LocalDB 用の `DefaultConnection` と `StorageRoot/Development` を持ちます
- `appsettings.Staging.json` は Staging 用のストレージ・ログ既定値を持ち、`ConnectionStrings__DefaultConnection` は環境変数や秘密情報ストアで与える前提です
- `appsettings.Production.json` は Production 用のストレージ・ログ既定値を持ち、`ConnectionStrings__DefaultConnection` は環境変数や秘密情報ストアで与える前提です
- `AzureMonitor:ServiceName` は既定で `PetHealthManagement.Web` を使います
- Application Insights の接続文字列は `APPLICATIONINSIGHTS_CONNECTION_STRING` または `AzureMonitor__ConnectionString` で与えます
- 起動時に `Storage:RootPath` が未設定なら fail fast します
- `Staging` / `Production` で Development の LocalDB 接続文字列を使おうとした場合も fail fast します
- `ConnectionStrings__DefaultConnection` や `Storage__RootPath` のような標準 ASP.NET Core キーで上書きできます

## Azure App Service プラットフォーム決定

- 本番 Web ホストは **Azure App Service on Linux** を採用します
- ポートフォリオ公開の初期構成では、まず **Free F1** を使います
  - 無料枠で運用できる一方、SLA、スケールアウト、独自ドメインなどは前提にしません
  - 独自ドメイン、常時安定稼働、より大きな保存容量が必要になったら Basic 以上へ上げます
- 理由は、アプリが ASP.NET Core / .NET 10 で Windows 固有依存を持たず、現在の CI も Linux 系ランナーに寄っているためです
- デプロイ前に App Service の Linux built-in stack が `.NET 10` を選べることを確認します。未対応の場合は self-contained publish か、対応済み LTS へのターゲット変更を別タスクで判断します
- `Storage__RootPath` は Linux App Service の永続領域である `/home` 配下の絶対パスを使います
  - 例: `Storage__RootPath=/home/pethealth-storage`
- この決定は App Service の OS を固定するもので、画像ストレージを長期的に App Service ファイルシステムで持つか Blob へ移すかは次の運用タスクで継続検討します

## Azure SQL Database 決定

- 本番 DB は **Azure SQL Database single database** を採用します
- ポートフォリオ公開の初期構成では **Azure SQL Database free offer** を第一候補にします
  - 月 100,000 vCore 秒、32GB data、32GB backup の無料枠内に収めます
  - 無料枠超過時の挙動は **Auto-pause the database until next month** を選び、課金継続を避けます
- 商用本番や常時安定稼働へ移す場合は、購入モデル **vCore-based**、サービス階層 **General Purpose**、compute model **Provisioned** を再評価します
- このアプリは単一 DB の ASP.NET Core / EF Core アプリなので、Managed Instance や elastic pool を前提にしません
- LocalDB と同じ SQL Server 系のため、既存の `UseSqlServer` と migration 運用をそのまま本番へ寄せやすい構成です
- free offer の自動停止や再開待ちがポートフォリオ閲覧に影響する場合は、デモ公開時間帯だけ手動確認するか、有料の安定構成へ切り替えます

## Azure コスト管理方針

- Azure リソースを作る前に **Cost Management の Budget Alert** を必ず作ります
  - 初期値の目安: 月 500 円から 1,000 円
  - 50%、80%、100% で通知します
- App Service Free F1 と Azure SQL Database free offer を使っても、Key Vault、Storage Account、Application Insights、通信量などは小額の従量課金が発生し得ます
- ポートフォリオ目的では、初期状態で Application Insights の接続文字列を設定せず、必要になった時だけ低サンプリングで有効化します

## 機密情報の管理方式決定

- 本番の機密情報は **Azure Key Vault** を正とします
- App Service には機密値を直接置かず、**Key Vault reference** を使った app settings / connection strings を置きます
- App Service では **system-assigned managed identity** を有効化し、Key Vault には secret 読み取り権限だけを付与します
- このアプリでは次を機密値として扱います
  - `ConnectionStrings__DefaultConnection`
  - 将来追加する外部サービス API key / secret
- このアプリでは次を非機密設定として App Service 構成に直接置きます
  - `Storage__RootPath`
  - `ASPNETCORE_ENVIRONMENT`
- 環境ごとに Key Vault を分け、production 用 secret は production 用 vault に閉じます

## 画像ストレージ方針決定

- 初期リリースの画像保存先は **Azure App Service on Linux の `/home` 配下**を正とします
- `Storage__RootPath` にはデプロイ成果物と分離した専用ディレクトリを設定します
  - 例: `Storage__RootPath=/home/pethealth-storage`
- 当面は既存の `FileSystemImageStorageService` をそのまま使い、App Service の Azure Storage mount は採用しません
- 特に **Azure Blob mount は read-only** のため、このアプリのアップロード・削除処理の保存先には使いません
- 将来 Blob へ移行する場合は mount ではなく、`IImageStorageService` の実装を Azure Blob Storage 向けに差し替える前提で進めます
- 次の条件が見えたら Blob への移行を再評価します
  - App Service の保存容量やバックアップ時間が制約になる
  - CDN 配信や画像ライフサイクル管理が必要になる
  - 画像を App Service から分離して別サービスと共有したくなる

## DataProtection キー永続化決定

- Staging / Production の DataProtection キーは **Azure Blob Storage** に永続化し、**Azure Key Vault key** で暗号化します
- App Service 既定の `%HOME%/ASP.NET/DataProtection-Keys` は slot をまたいで共有されず、at-rest 保護もないため、本番の正にはしません
- アプリでは `SetApplicationName("PetHealthManagement.Web")` を固定し、全デプロイ先で揃えます
- App Service では **system-assigned managed identity** を使い、Blob と Key Vault key にアクセスします
- 本番で最低限必要な設定は次です
  - `DataProtection__ApplicationName=PetHealthManagement.Web`
  - `DataProtection__BlobUri=https://<storage-account>.blob.core.windows.net/<container>/keys.xml`
  - `DataProtection__KeyVaultKeyIdentifier=https://<vault-name>.vault.azure.net/keys/data-protection`
- `DataProtection__ManagedIdentityClientId` は将来 user-assigned managed identity を使う場合だけ任意で設定します
- Key Vault key を自動ローテーションする場合は、`DataProtection__KeyVaultKeyIdentifier` に **versionless** な key identifier を使い、過去 key を削除しないでください

## 開発環境セットアップ

- `Species` は `SpeciesCatalog` の固定コードなので、DB へのマスタ seed は不要です
- 初回のローカルセットアップは `Migration 適用 -> Admin seed -> ログイン確認` の順で進められます
- 開発用 Admin ユーザーは `Development` 環境でのみ seed され、設定値はコミットせず `user-secrets` または環境変数で与えます

PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/setup-dev.ps1 -AdminPassword 'Admin123!'
```

Bash:

```bash
./scripts/setup-dev.sh --admin-password 'Admin123!'
```

- 必要なら `DevelopmentSetup:AdminEmail` / `DevelopmentSetup__AdminEmail` でメールアドレスを上書きできます
- 必要なら `DevelopmentSetup:AdminDisplayName` / `DevelopmentSetup__AdminDisplayName` で表示名を上書きできます
- セットアップ後は次のコマンドで起動し、`/Identity/Account/Login` からログインします

```bash
dotnet run --project src/PetHealthManagement.Web --launch-profile https
```

- 単発実行用に次のコマンドも使えます

```bash
dotnet run --project src/PetHealthManagement.Web --no-launch-profile -- --apply-migrations
dotnet run --project src/PetHealthManagement.Web --no-launch-profile -- --seed-development
dotnet run --project src/PetHealthManagement.Web --no-launch-profile -- --setup-development
```

## 本番 Migration 運用手順

- 本番では `--apply-migrations` のみを使います。`--seed-development` と `--setup-development` は `Development` 専用です
- 実行前に、対象 DB のバックアップ取得、`ConnectionStrings__DefaultConnection` と `Storage__RootPath` の確認、適用対象の migration 差分レビューを済ませます
- migration 実行は 1 回だけにします。App Service の複数インスタンスを同時に立ち上げたまま自動実行するのではなく、デプロイジョブやメンテナンス手順の中で明示的に 1 回だけ流します

### GitHub Actions での実行方式

- `.github/workflows/production-migrate.yml` を `workflow_dispatch` で手動実行し、**GitHub Actions runner から Azure SQL に対して 1 回だけ migration を適用**します
- workflow は `main` 専用で、GitHub Environment `production` の値を使って Azure へ OIDC ログインします
- 接続文字列は App Service から逆読みせず、`AZURE_KEY_VAULT_NAME` と `AZURE_SQL_CONNECTION_SECRET_NAME` を使って Key Vault から直接取得します
- migration 実行時はアプリ本体を `Production` 環境で起動し、`ConnectionStrings__DefaultConnection`、`Storage__RootPath`、DataProtection の本番設定をそのまま注入します
- 実行順は次を正とします
  1. `main` に対象コミットがあることを確認する
  2. `Production Migrations` workflow を実行する
  3. 成功後に `CD` workflow の deploy job を承認または手動実行する
- GitHub Environment `production` に最低限必要な値は次です
  - Variable: `AZURE_KEY_VAULT_NAME=<key-vault-name>`
  - Variable: `AZURE_SQL_CONNECTION_SECRET_NAME=<default-connection-secret-name>`
  - Variable: `STORAGE_ROOT_PATH=/home/pethealth-storage`
  - Variable: `DATA_PROTECTION_BLOB_URI=https://<storage-account>.blob.core.windows.net/<container>/keys.xml`
  - Variable: `DATA_PROTECTION_KEY_VAULT_KEY_IDENTIFIER=https://<vault-name>.vault.azure.net/keys/data-protection`
  - Variable: `DATA_PROTECTION_APPLICATION_NAME=PetHealthManagement.Web`（省略可）
  - Variable: `DATA_PROTECTION_MANAGED_IDENTITY_CLIENT_ID=<client-id>`（user-assigned managed identity を使う場合のみ）
  - Secret: `AZURE_CLIENT_ID=<federated-credential-client-id>`
  - Secret: `AZURE_TENANT_ID=<tenant-id>`
  - Secret: `AZURE_SUBSCRIPTION_ID=<subscription-id>`
- この workflow を実行する Azure principal には、少なくとも次の権限が必要です
  - Key Vault の接続文字列 secret を読む権限
  - DataProtection 用 blob への読み書き権限
  - DataProtection 用 Key Vault key の `Get` / `Wrap Key` / `Unwrap Key`

### 適用順

1. 新しいアプリ版をデプロイ可能な状態にし、まだ本番トラフィックは切り替えない
2. 本番用の環境変数を確認する
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ConnectionStrings__DefaultConnection=<production-db>`
   - `Storage__RootPath=<production-storage-root>`
3. migration を 1 回だけ実行する
4. migration 成功後に新しいアプリ版へ切り替える
5. ログイン、一覧表示、画像 GET などの smoke 確認を行う

ソース checkout から実行する場合:

```bash
dotnet run --project src/PetHealthManagement.Web --no-launch-profile -- --apply-migrations
```

公開済み成果物から実行する場合:

```bash
dotnet PetHealthManagement.Web.dll --apply-migrations
```

PowerShell 例:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ConnectionStrings__DefaultConnection = '<production-db>'
$env:Storage__RootPath = '<production-storage-root>'
dotnet .\PetHealthManagement.Web.dll --apply-migrations
```

### ロールバック方針

- migration 実行前に失敗した場合は、アプリ切り替えを行わず原因を修正して再実行します
- migration 実行後にアプリ不具合が見つかった場合は、まず「前進修正」で短時間に戻せるかを判断します
- 即時復旧が必要で、かつ schema 変更が後方互換でない場合は「アプリを旧版へ戻す + migration 前バックアップから DB を復元」を基本方針にします
- 本番で安易に `database update <oldMigration>` のような Down migration を直接流す運用は採りません。人間レビュー済みで安全性が確認できた場合だけ例外扱いにします
- 復旧後は `__EFMigrationsHistory`、アプリログ、デプロイ記録を確認し、どの migration まで適用されたかを運用メモへ残します

### アプリの戻し方

- 現行構成では deployment slot をまだ使っていないため、**production への戻し方は「既知の良品 ref を再デプロイする」**を正とします
- GitHub Actions の `.github/workflows/rollback-production.yml` を `workflow_dispatch` で手動実行し、`target_ref` に戻したい commit SHA / tag / branch を指定します
- rollback workflow は対象 ref を checkout し、**Release build → full test → publish → Azure App Service deploy → post-rollback smoke** を 1 本で実行します
- rollback 後の smoke は `CD` と同じ `APP_BASE_URL` / `SMOKE_TEST_EMAIL` / `SMOKE_TEST_PASSWORD` を使います。`SMOKE_TEST_IMAGE_URL` がある場合だけ認可付き画像 GET も確認します
- schema 変更が後方互換でない場合は、アプリの rollback だけでは不整合が残るため、**migration 前バックアップから DB を復元する判断**まで含めて実施します
- 将来 App Service の Standard 以上で slot 運用へ切り替える場合は、staging slot への deploy と swap-back を第一候補として再評価します

### 戻す判断基準

- **即時 rollback**
  - post-deploy smoke が失敗した
  - ログイン / `/Pets` / 設定済みの認可付き画像 GET のどれかが壊れた
  - 本番 5xx が継続して増加し、5 分で **5% 以上** または **10 件以上** を確認した
- **15 分以内に rollback 判断**
  - 未処理例外が 5 分で **10 件以上** 継続し、前進修正の見込みが立たない
  - 平均サーバ応答時間がデプロイ前の **2 倍以上** または **5 秒超** で継続する
  - Application Insights availability test が連続失敗する
- **前進修正を優先**
  - 監視上は健全で、表示崩れや軽微な UX 不具合などユーザー影響が限定的
  - schema rollback を伴わず短時間で修正デプロイできる
- rollback を実施したら、Application Insights、smoke 実行結果、対象 ref、DB 復元の有無を運用メモへ残します

## GitHub Actions デプロイ

- `.github/workflows/cd.yml` は `main` への push と `workflow_dispatch` を契機に、Release の `build -> full test -> publish -> deploy` を実行します
- deploy job は GitHub Environment `production` に紐づけています。承認を入れたい場合は environment protection rules で制御します
- デプロイ先は Azure App Service on Linux を前提とし、Azure への認証は GitHub Actions の OIDC (`azure/login`) を使います
- この workflow はアプリ成果物のデプロイまでを担当します。schema 変更を含むリリースでは、先に `Production Migrations` workflow を流してから deploy job を進めます
- deploy の最後に `scripts/local-smoke.sh --use-existing-app` を実行し、**ログイン / 一覧表示** を post-deploy smoke として必須化しています。`SMOKE_TEST_IMAGE_URL` が設定されている場合は **画像 GET** も確認します
- `production` environment に最低限必要な設定は次です
  - Variable: `APP_BASE_URL=https://<app-host>`
  - Variable: `AZURE_WEBAPP_NAME=<app-service-name>`
  - Secret: `AZURE_CLIENT_ID=<federated-credential-client-id>`
  - Secret: `AZURE_TENANT_ID=<tenant-id>`
  - Secret: `AZURE_SUBSCRIPTION_ID=<subscription-id>`
  - Secret: `SMOKE_TEST_EMAIL=<smoke-user-email>`
  - Secret: `SMOKE_TEST_PASSWORD=<smoke-user-password>`
- 任意で次を設定します
  - Variable: `SMOKE_TEST_IMAGE_URL=/images/<smoke-image-id>` または `https://<app-host>/images/<smoke-image-id>`
- Azure 側では、GitHub の `main` ブランチからこの workflow を信頼する federated credential を作成し、対象 App Service へデプロイ可能な権限を付与します
- App Service のアプリ設定は workflow では変更しません。`ConnectionStrings__DefaultConnection` は Key Vault reference、`Storage__RootPath` は `/home/...` を事前に構成しておきます
- 手動で再デプロイしたい場合は、Actions の `CD` workflow を `main` で `Run workflow` します
- smoke 用には、MyPage / Pets にアクセスできる専用ユーザーを運用で用意しておきます。画像 smoke を有効化する場合は、そのユーザーが参照できる維持画像も作成します
- rollback が必要な場合は、Actions の `Rollback Production` workflow を `main` から手動実行し、`target_ref` に既知の良品 ref を指定します

## Application Insights monitoring

- アプリは `Azure.Monitor.OpenTelemetry.AspNetCore` を使って Azure Monitor Application Insights へ request / dependency / exception / `ILogger` ログを送ります
- 接続文字列が未設定なら監視パイプラインは有効化されず、ローカル開発とテストは従来どおり動きます
- ポートフォリオ初期公開では接続文字列を未設定にし、Azure コストとノイズを抑えることを既定運用にします
- 有効化するには App Service 構成に次のいずれかを設定します
  - `APPLICATIONINSIGHTS_CONNECTION_STRING=<application-insights-connection-string>`
  - `AzureMonitor__ConnectionString=<application-insights-connection-string>`
- 既定の Cloud Role Name は `PetHealthManagement.Web` です。別名にしたい場合は `AzureMonitor__ServiceName` で上書きできます
- 任意で次の設定も使えます
  - `AzureMonitor__EnableLiveMetrics=false`（既定）
  - `AzureMonitor__SamplingRatio=0.05..0.2`
  - `AzureMonitor__StorageDirectory=/home/pethealth-monitoring`
- 初期リリースでは次を最低限の監視対象にします
  - 未処理例外
  - 画像アップロード拒否・削除失敗
  - アカウント削除 / Admin 削除の監査寄りログ
  - HTTP request / dependency の失敗率と応答時間
  - availability test による外形監視
- Application Insights 側では少なくとも次のアラートを作る前提です
  - 5xx エラー率の急増
  - 例外件数の急増
  - サーバ応答時間の悪化
  - availability test の失敗

## 画像運用 Runbook

- 本番画像は `Storage__RootPath` 配下で運用します。現行の `FileSystemImageStorageService` は `images/` に本体、`tmp/` に一時ファイルを置きます
- 初期リリースでは Blob mount を使わず、App Service の `/home` 配下を正として扱います。保存先の例は `Storage__RootPath=/home/pethealth-storage` です

### バックアップとライフサイクル

- デプロイ後運用では `Storage__RootPath` 配下をバックアップ対象に含めます。App Service Backup が使えるプランではそれを優先し、未対応プランでは同等の定期コピーを別手段で確保します
- 少なくとも 1 日 1 回の定期バックアップを取り、さらに**画像アップロード/削除実装を変更するリリース前**、**Admin 削除やアカウント削除を伴う保守前**にはオンデマンドのバックアップを追加します
- 画像を戻すときは、いきなり production の `/home/pethealth-storage` を丸ごと上書きせず、まず隔離した場所へ復元して対象の `images/...` だけを確認します。その後に必要なファイルだけを production へ戻し、認可付き `GET /images/{imageId}` の smoke で確認します
- `tmp/` は通常リクエスト完了時に片付く想定ですが、異常終了時の残骸があり得ます。週次運用で `tmp/` に 24 時間超の古いファイルが残っていないか確認し、アプリ停止やメンテナンス時間帯に削除します
- 保存容量、バックアップ時間、CDN 配信、画像ライフサイクル管理がボトルネックになった時点で、`IImageStorageService` 差し替えによる Blob 移行タスクを起票します

### 削除失敗時の運用再試行

- 初期リリースでは画像削除失敗専用の retry queue や定期バッチはまだ持ちません。**Application Insights のログを起点にした運用再試行**を正とします
- 削除失敗は `Failed to delete image file` という warning ログで記録され、`imageCategory`、`ownerId`、`resourceType`、`resourceId`、`phase`、`imageId`、`storageKey` を付けて追跡できます
- 監視画面では次の KQL を起点に、削除失敗の未処理件数を確認します

```kusto
traces
| where message has "Failed to delete image file"
| project
    timestamp,
    severityLevel,
    message,
    imageCategory = tostring(customDimensions.ImageCategory),
    ownerId = tostring(customDimensions.OwnerId),
    resourceType = tostring(customDimensions.ResourceType),
    resourceId = tostring(customDimensions.ResourceId),
    phase = tostring(customDimensions.Phase),
    imageId = tostring(customDimensions.ImageId),
    storageKey = tostring(customDimensions.StorageKey)
| order by timestamp desc
```

- 運用で削除失敗を見つけたら、まず対象画像がまだアプリから参照されていないかを確認します。まだ使われている画像なら削除せず、参照切れなのにファイルが残っている場合だけ `storageKey` の実体を手動削除します
- 手動削除の結果は、`timestamp`、`imageId`、`storageKey`、対応者、結果を運用メモへ残します。ファイルが既に無い場合は「解消済み」としてクローズします
- 同じ `phase` や同じ機能で削除失敗が継続する場合は、運用で吸収し続けず、retry queue / background job / 定期バッチの実装タスクを別 PR で切ります

## 依存関係更新の運用

- `.github/dependabot.yml` で Dependabot version updates を有効にしています
- `NuGet` は `src/**` と `tests/**` を毎週月曜 09:00 JST に確認します
- `GitHub Actions` は毎週月曜 09:30 JST に確認します
- `.NET SDK` は `global.json` を毎月 10:00 JST に確認します
- Dependabot PR は `main` 向けに作成される前提です。通常の CI が通ることを確認してからマージします
- 現在の自動更新対象は `.csproj`、GitHub Actions、`global.json` です。`dotnet-tools.json` の更新は必要に応じて手動で確認します

## ローカル smoke 確認

- HTTPS プロファイルでアプリを起動し、トップページ、ログイン画面、保護ページのリダイレクト、共通エラーページの応答を確認できます
- `-Email` と `-Password` を渡すと、ログイン後の `MyPage` と `Pets` まで確認します
- `-ImageUrl` / `--image-url` を渡すと、ログイン後に認可付き画像 GET も確認します
- Admin ユーザーで確認する場合は `-ExpectAdmin` を付けると `/Admin/Users` も確認します
- 既に起動済みの環境を叩く場合は `-UseExistingApp` / `--use-existing-app` を使います

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/local-smoke.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/local-smoke.ps1 -Email 'admin@example.com' -Password 'Admin123!' -ExpectAdmin
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/local-smoke.ps1 -UseExistingApp -BaseUrl 'https://pethealth.example.com' -Email 'smoke@example.com' -Password 'Smoke123!' -ImageUrl '/images/<image-id>'
```

```bash
bash ./scripts/local-smoke.sh
bash ./scripts/local-smoke.sh --email 'admin@example.com' --password 'Admin123!' --expect-admin
bash ./scripts/local-smoke.sh --use-existing-app --base-url 'https://pethealth.example.com' --email 'smoke@example.com' --password 'Smoke123!' --image-url '/images/<image-id>'
```

## セキュリティ既定値

- 認証 Cookie は `__Host-PetHealthManagement.Auth`、`Secure`、`HttpOnly`、`SameSite=Lax` です
- Anti-forgery Cookie は `__Host-PetHealthManagement.AntiForgery`、`Secure`、`HttpOnly`、`SameSite=Strict` です
- HSTS は Development 以外で有効、`Max-Age` は 180 日です
- セキュリティヘッダとして `Content-Security-Policy`、`Referrer-Policy`、`Permissions-Policy`、`X-Content-Type-Options`、`X-Frame-Options` を付与します
- 現在の CSP は、既存 Razor に inline handler が残っているため、最小構成として `'unsafe-inline'` を許可しています

## ログ既定値

- 画像アップロード拒否、保存失敗、ファイル削除失敗は `imageCategory`、`ownerId`、`resourceType`、`resourceId`、`reason`、`storageKey` などの structured fields 付きで記録します
- 高リスクな削除処理は `operation`、`ownerId`、`targetType`、`targetId`、件数情報付きで開始・完了・失敗を記録します
- アカウント削除や Admin によるユーザー削除は `actorUserId` と対象情報を含む監査寄りログを出します
- 未処理例外は `method`、`path`、`traceId`、`userId` を付けて記録します
- Staging / Production では、上記ログは Application Insights に送る前提です
- 永続的な監査ログ保管や高度な運用アラートの詳細化は今後の運用タスクです

## テスト方針

- 単体テストと controller テストは、基本的に `TestDbContextFactory.CreateInMemoryDbContext(...)` による EF Core InMemory を使います
- SQL 変換確認やクエリ数確認は `TestDbContextFactory.CreateSqliteInMemoryContextAsync(...)` による SQLite in-memory を使います
- integration テストは `IntegrationTestWebApplicationFactory` を使い、アプリ DB を EF Core InMemory に差し替えつつ、テストごとの一時 `StorageRoot` を割り当てます
- Playwright E2E テストは `PetHealthManagement.Web.E2ETests` に分離し、テスト用 Kestrel プロキシを実ポートで起動しながら、アプリ DB は EF Core InMemory、画像ストレージは一時 `StorageRoot` に差し替えます
- Playwright E2E テストは既定ではスキップされます。実ブラウザで実行する場合だけ `RUN_PLAYWRIGHT_E2E=1` を設定します
- ファイルベースの画像ストレージテストは `TestFileBackedImageStorageService` を使い、一時ディレクトリへ書き込んで後始末します
- テスト用の一時ストレージは OS の temp 配下に作られ、本番や通常開発の保存先を指さない前提です
- リレーショナル挙動、SQL 変換、クエリ数に依存するテストでは EF Core InMemory ではなく SQLite in-memory を優先します
- GitHub Actions の `minimum-required-checks` は `CiTier=Critical` のテストだけを回し、認証 / 存在秘匿 / 画像の回帰を先に検知します
- `full-regression` は全テストと format を回し、段階導入の間も広い回帰シグナルを維持します

### Playwright E2E テスト

Playwright E2E テストは、専用スクリプトでテストプロジェクトをビルドしてから `RUN_PLAYWRIGHT_E2E=1` を付けて実行します。ブラウザは既定で `chromium` です。

PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-e2e.ps1 -InstallBrowsers
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-e2e.ps1
```

Bash:

```bash
bash ./scripts/test-e2e.sh --install-browsers
bash ./scripts/test-e2e.sh
```

別ブラウザで実行する場合は、PowerShell では `-Browser firefox`、Bash では `--browser firefox` を指定します。`dotnet test` へ追加引数を渡す場合は、PowerShell では `-DotnetArgs '--filter','FullyQualifiedName~MyTests'`、Bash では `--` 以降に続けます。

## 参照ドキュメント

- 開発ルール: `AGENTS.md`
- PR と作業ガイド: `CONTRIBUTING.md`
- タスク分割テンプレ: `docs/task-splitting-template.md`
- PR テンプレ: `.github/pull_request_template.md`
- 設計資料: `docs/`
- 実装タスク一覧: `todo.md`

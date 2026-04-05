# PetHealthManagement

ペット健康管理アプリです。ASP.NET Core MVC、Identity、EF Core、SQL Server を使用しています。

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
- 起動時に `Storage:RootPath` が未設定なら fail fast します
- `Staging` / `Production` で Development の LocalDB 接続文字列を使おうとした場合も fail fast します
- `ConnectionStrings__DefaultConnection` や `Storage__RootPath` のような標準 ASP.NET Core キーで上書きできます

## Azure App Service プラットフォーム決定

- 本番 Web ホストは **Azure App Service on Linux** を採用します
- 理由は、アプリが ASP.NET Core / .NET 10 で Windows 固有依存を持たず、現在の CI も Linux 系ランナーに寄っているためです
- `Storage__RootPath` は Linux App Service の永続領域である `/home` 配下の絶対パスを使います
  - 例: `Storage__RootPath=/home/pethealth-storage`
- この決定は App Service の OS を固定するもので、画像ストレージを長期的に App Service ファイルシステムで持つか Blob へ移すかは次の運用タスクで継続検討します

## Azure SQL Database 決定

- 本番 DB は **Azure SQL Database single database** を採用します
- 購入モデルは **vCore-based**、サービス階層は **General Purpose**、compute model は **Provisioned** を正とします
- このアプリは単一 DB の ASP.NET Core / EF Core アプリなので、Managed Instance や elastic pool を前提にしません
- LocalDB と同じ SQL Server 系のため、既存の `UseSqlServer` と migration 運用をそのまま本番へ寄せやすい構成です
- serverless auto-pause は初回接続時の再開待ちや接続失敗リトライを招きうるため、ログイン操作やリリース後 smoke を安定させる目的で当面は採用しません
- 接続文字列の具体的な渡し方と機密情報の保管方法は次タスクで決めます

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
- Admin ユーザーで確認する場合は `-ExpectAdmin` を付けると `/Admin/Users` も確認します

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/local-smoke.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/local-smoke.ps1 -Email 'admin@example.com' -Password 'Admin123!' -ExpectAdmin
```

```bash
bash ./scripts/local-smoke.sh
bash ./scripts/local-smoke.sh --email 'admin@example.com' --password 'Admin123!' --expect-admin
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
- 永続的な監査ログ保管や外部監視は今後の運用タスクです

## テスト方針

- 単体テストと controller テストは、基本的に `TestDbContextFactory.CreateInMemoryDbContext(...)` による EF Core InMemory を使います
- SQL 変換確認やクエリ数確認は `TestDbContextFactory.CreateSqliteInMemoryContextAsync(...)` による SQLite in-memory を使います
- integration テストは `IntegrationTestWebApplicationFactory` を使い、アプリ DB を EF Core InMemory に差し替えつつ、テストごとの一時 `StorageRoot` を割り当てます
- ファイルベースの画像ストレージテストは `TestFileBackedImageStorageService` を使い、一時ディレクトリへ書き込んで後始末します
- テスト用の一時ストレージは OS の temp 配下に作られ、本番や通常開発の保存先を指さない前提です
- リレーショナル挙動、SQL 変換、クエリ数に依存するテストでは EF Core InMemory ではなく SQLite in-memory を優先します
- GitHub Actions の `minimum-required-checks` は `CiTier=Critical` のテストだけを回し、認証 / 存在秘匿 / 画像の回帰を先に検知します
- `full-regression` は全テストと format を回し、段階導入の間も広い回帰シグナルを維持します

## 参照ドキュメント

- 開発ルール: `AGENTS.md`
- PR と作業ガイド: `CONTRIBUTING.md`
- タスク分割テンプレ: `docs/task-splitting-template.md`
- PR テンプレ: `.github/pull_request_template.md`
- 設計資料: `docs/`
- 実装タスク一覧: `todo.md`

# CONTRIBUTING

このリポジトリは「ペット健康管理アプリ（ASP.NET Core MVC + Identity / EF Core / SQL Server）」です。

変更を始める前に、まず [AGENTS.md] を読んでください。  
このファイルは、日常的な開発フローと PR 前のチェックポイントを簡潔にまとめたものです。

## 1. 参照順

仕様の正は次の順です。

1. [todo.md]
2. [docs/requirements.md]
3. [docs/basic-design.md]
4. [docs/api-spec.md]
5. [docs/test-cases-by-screen.md]
6. [docs/er-diagram.md]
7. [docs/screen-transition-diagram.md]
8. [docs/ui-wireframe.md]
9. [docs/screen-item-definition.md]

矛盾を見つけた場合は、先に `todo.md` に決定事項を反映してから進めてください。

## 2. セットアップと確認コマンド

PR 前の基本コマンド:

- Build: `./scripts/build.sh`
- Test: `./scripts/test.sh`
- Critical CI subset: `./scripts/test-critical.sh`
- Format: `./scripts/format.sh`

Windows の場合:

- Build: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1`
- Test: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1`
- Critical CI subset: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-critical.ps1`
- Format: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/format.ps1`

最低限、変更の最後に `scripts/test` は必ず通してください。  
通常の PR では `build` と `format` も合わせて確認します。

## 3. PR ルール

- 1PR = 1目的で、小さくまとめる
- 変更に合わせて `todo.md` の完了状態を同期する
- 仕様変更や判断を入れた場合は、根拠となる資料を合わせて更新する
- コード変更には、対応するテスト追加か、テスト不要の理由を添える
- 認可、存在秘匿、returnUrl、画像処理、物理削除は特にレビュー観点を明示する

## 4. 実装時の必須ルール

### 4.1 認可と存在秘匿

- 未ログインで保護 URL にアクセスした場合は、既定どおりログインへ 302
- ログイン済みで所有者不一致や非公開により見せてはいけない場合は 404
- 存在秘匿の対象は `Pet` / `HealthLog` / `ScheduleItem` / `Visit` / `Image`
- Admin エリアは、Admin 以外 403

### 4.2 returnUrl と Open Redirect 対策

- `returnUrl` は必ず `Url.IsLocalUrl(returnUrl)` または同等処理で検証する
- 画面遷移保持は `Query -> hidden -> POST` で統一する
- `returnUrl` が未指定または非ローカルの場合は無視し、仕様上のデフォルト遷移先を使う

### 4.3 HTTP ステータスと PRG

- GET 成功は 200
- POST 成功は原則 302 Redirect
- Create/Edit のバリデーションエラーは 200 で同画面再表示
- 一覧トグルや削除など画面を持たない POST の不正入力は 400 で扱い、`/Error/400` に統一する

### 4.4 画像アップロードと画像配信

- 許可形式は `.jpg` `.jpeg` `.png` `.webp` と対応 Content-Type のみ
- 実データをデコードして画像かどうか判定する
- 1ファイル 2MB、HealthLog/Visit の添付は最大10枚、ユーザー合計100MB
- 最大辺 4096px、総画素数 16,777,216px 以下
- EXIF は除去し、向き正規化後に再エンコードする
- アップロード画像は `wwwroot` 外へ保存し、一時保存は `StorageRoot/tmp` を使う
- `GET /images/{imageId}` は参照元を辿って認可し、非許可・不在・Pending は 404

### 4.5 所有者判定

- クライアント送信の `petId` は信頼しない
- 所有者認可は常に DB の参照関係から判定する
- 例: 完了トグルや削除は `scheduleItemId` や `visitId` から親を復元して判定する

### 4.6 ドメインルール

- `RecordedAt` は `DateTimeOffset (+09:00)`
- `IsPublic` の既定値は `true`

## 5. 命名規約チェックリスト

- Route placeholder と Query key は lowerCamelCase を使う
- 代表例: `petId`, `healthLogId`, `scheduleItemId`, `visitId`, `returnUrl`, `page`, `nameKeyword`, `speciesFilter`
- 画面遷移保持に使う key は `Query -> hidden -> POST` で同じ名前を維持する
- `asp-route-*` や手書き URL を追加するときも、`docs/api-spec.md` と同じ key 名にそろえる
- ViewModel のプロパティ名が PascalCase でも、hidden input や link/query key は lowerCamelCase を優先する

## 6. 人間レビューが必要な領域

次の変更は、意図とリスクを書いてレビューを依頼してください。

- 認可
- 存在秘匿
- returnUrl / リダイレクト
- 画像検証 / 画像配信 / 画像削除
- アカウント削除 / Admin 削除 / 関連データの物理削除

## 7. 作業の進め方

- 先に短い Plan を作る
- 実装は小さく進める
- 最後に `scripts/test` を通す
- 実装状況が変わったら `todo.md` を同期する
- 着手テンプレは `docs/task-splitting-template.md` を使う
- PR 本文テンプレは `.github/pull_request_template.md` を使う

## 8. CI と品質ゲート

- GitHub Actions の CI は `minimum-required-checks` と `full-regression` に分かれている
- `minimum-required-checks` は `build` と `CiTier=Critical` のテストを回し、認証 / 存在秘匿 / 画像の最小回帰を確認する
- ブランチ保護を設定するときは、最初は `minimum-required-checks` を required check にする想定
- `full-regression` は全テストと `format` を回す
- `format` もローカルで確認してから PR を出す
- 主要シナリオの結合テストは回帰防止のため維持する

## 9. 依存関係更新

- `.github/dependabot.yml` で Dependabot を運用する
- 自動更新の対象は `NuGet`、`GitHub Actions`、`.NET SDK (global.json)` とする
- Dependabot PR も通常の PR と同様に CI を通してレビューする
- `dotnet-tools.json` のような現在の自動更新対象外ファイルは、必要に応じて手動更新する

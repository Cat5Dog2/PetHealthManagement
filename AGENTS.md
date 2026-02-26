# AGENTS.md

このリポジトリは「ペット健康管理アプリ（ASP.NET Core MVC + Identity / EF Core / SQL Server）」です。

Codexが作業する時は、**必ずこの指示に従って**ください。

---

## 0. 最重要ルール（安全・品質）

### 0.1 403 / 404（存在秘匿の基本）
- **未ログイン**：`[Authorize]` の既定動作に従い **ログインへ 302**（リダイレクト）。
- **ログイン済み**で、**所有者不一致** や **非公開** などにより閲覧できない場合：
  - 次の「存在秘匿対象」は原則 **404**（存在を隠す）。
    - 対象：Pet / HealthLog / ScheduleItem / Visit / Image（画像配信含む）
  - **Admin エリア**（`/Areas/Admin` 等）は、Admin 以外 **403**。

### 0.2 returnUrl（Open Redirect 対策）
- returnUrl を受け取る場合は `Url.IsLocalUrl(returnUrl)`（または同等）で必ず検証する。
- 画面遷移保持は「Query → hidden → POST」で統一する（画面項目定義に合わせる）。
- returnUrl が **未指定 or 非ローカル** の場合は **無視**し、各エンドポイントの **デフォルト遷移先（api-spec.md に記載）**へ進める。

### 0.3 HTTP ステータス / 画面遷移（PRG）
- GET 成功：200（HTML）
- POST 成功：原則 **302 Redirect（PRG）**
- **バリデーションエラー**
  - Create/Edit など **画面を再表示するフォーム送信**：200（同画面再表示、ModelState 表示）
  - 一覧トグル/削除など **画面を持たない Action**：400（不正リクエスト）→ 共通エラー（例：`/Error/400`）

### 0.4 画像アップロード（必須のサーバ側検証）
- 許可拡張子/Content-Type 検証（`.jpg/.jpeg/.png/.webp` & `image/jpeg|image/png|image/webp`）
- **実データをデコードして画像判定**（偽装拒否）
- 1ファイル 2MB、最大10枚（HealthLog/Visit：既存+追加の合算）、ユーザー合計100MB
- デコード後の制限：**最大辺4096px かつ 総画素数 16,777,216px（幅×高さ）以下**
- **EXIF除去・向き正規化**：デコード→向き補正→再エンコード（メタデータ保持しない）
- 保存：アップロード画像は **wwwroot 外**、一時保存は `StorageRoot/tmp`（詳細は基本設計）

### 0.5 画像配信 `GET /images/{imageId}`
- 参照元（Avatar/HealthLog/Visit/PetPhoto 等）を辿って認可し、非許可は **404**（存在秘匿）。
- 次の場合も **404**：ImageAsset 不在 / `Status=Pending` / 参照元が辿れない（削除済み等）
- レスポンス推奨ヘッダ：`Cache-Control: private, no-store` / `Content-Disposition: inline` / `X-Content-Type-Options: nosniff`

### 0.6 フォームの配列送信（DeleteImageIds / NewFiles）
- モデルバインドは ASP.NET Core 方式（同名を複数送信、または `Foo[0]` … / `<input type=file name="NewFiles" multiple>`）
- **未送信は空配列（0件）として扱う**（null 前提にしない）

### 0.7 クエリパラメータ命名
- クエリパラメータは仕様書どおり **lowerCamelCase** を正とする（例：`nameKeyword`）。

### 0.8 「petIdを信頼しない」
- 例：完了トグル等は `scheduleItemId` から PetId を復元し、所有者チェックを行う。
- 認可は常に DB の参照関係で判定する（クライアント送信の petId は **遷移/表示の補助**に留める）。

### 0.9 ドメインの固定ルール
- 健康ログ日時：`RecordedAt` は **DateTimeOffset（+09:00）**（基本設計に準拠）
- `IsPublic` の既定値：**true（公開）**（基本設計に準拠）

---

## 1. 作業の進め方（Codexの標準手順）

1) **Plan**：何をどのファイルにどう直すかを短く列挙してから着手する。  
2) **Small PR**：変更は小さく（1PR=1目的）。  
3) **DoD**：作業の最後に、必ず `scripts/test` を通す。  

---

## 2. よく使うコマンド（この順で実行）

- ビルド：`./scripts/build.sh`（Windowsは `./scripts/build.ps1`）
- テスト：`./scripts/test.sh`（Windowsは `./scripts/test.ps1`）
- フォーマット（導入時）：`./scripts/format.sh`（Windowsは `./scripts/format.ps1`）

---

## 3. 変更時に必ず人間レビューが必要な領域

- 認可（所有者チェック）
- 存在秘匿（404/403/400/500 の扱い）
- returnUrl / リダイレクト
- 画像検証・画像配信・画像削除
- アカウント削除 / Admin削除（データ物理削除）

---

## 4. 参照すべき資料（この順で正）

1. TODO（`todo.md`）
2. 要件定義（`requirements.md`）
3. 基本設計（`basic-design.md`）
4. API仕様（`api-spec.md`）
5. テスト観点（`test-cases-by-screen.md`）
6. ER図（`er-diagram.md`）
7. 画面遷移（`screen-transition-diagram.md`）
8. UIワイヤー（`ui-wireframe.md`）
9. 画面項目定義（`screen-item-definition.md`）

矛盾があれば、まず TODO に「決定事項」として追記し、以後それを正とする。

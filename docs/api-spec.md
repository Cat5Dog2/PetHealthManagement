# ペット健康管理アプリ API仕様書

- 対象：ペット健康管理アプリ（ASP.NET Core MVC + Identity）
- バージョン：1.0
- 作成日：2026-01-13
- 作成者：Cat5Dog2
- 想定読者：開発者本人、レビュー担当者

---

## 1. 共通仕様

### 1.1 ベースURL
- 開発（例）：`https://localhost:{port}`
- 本番：環境により異なる

---

### 1.2 認証・認可
- 認証：ASP.NET Core Identity（Cookie認証）
- 認可：ロール（Admin）＋所有者チェック（`UserId`一致）
- [Authorize]：要認証ルートに付与

- ログイン済み時のアクセス制御（要点）：
  - **健康ログ / 予定 / 通院履歴**
    - 自分のペット：閲覧/操作可（200/302）
    - 他人のペット：存在秘匿のため **404**
  - **ペット詳細（公開検索 + 自分のペット）**
    - 自分のペット：常に閲覧可（200）
    - 他人のペット：`IsPublic=true` のみ閲覧可（200）
    - 他人の `IsPublic=false`：存在秘匿のため **404**
  - **ペットの書き込み系（`/Pets/Edit/*`, `/Pets/Delete/*`）**
    - 自分のペット：操作可（200/302）
    - 他人のペット：公開/非公開を問わず **404**（存在秘匿のため）
  - **画像配信 `GET /images/{imageId}`**
    - 非許可・存在しない・参照元が辿れない等：存在秘匿のため **404**
  - **Admin エリア**
    - Admin 以外：**403**

#### 1.2.1 403 / 404 の使い分け（実装ルール）
- 他人のリソースに対するアクセス（所有者不一致）の扱い：
  - **存在秘匿したいリソース**（ペット、健康ログ、予定、通院履歴、画像など）：**404**
  - **存在秘匿の必要が低い場所**（Admin エリア等）：**403**
- Admin 以外の Admin ルート：**403**

---

### 1.3 ページング
- 一覧は基本 **10件/ページ**
- ページ番号はクエリ `page`（1始まり）

#### 1.3.1 `page` のバリデーション（共通）
- `page` が未指定：1
- `page` が `<=0` または数値でない：1 に補正
- `page` が極端に大きい：DBが返せる範囲でOK（空は空で表示）

---

### 1.4 画像アップロード共通ルール（要点）
- 許可：`.jpg/.jpeg/.png/.webp`、Content-Type は `image/jpeg|image/png|image/webp`
- 1ファイル上限：2MB
- 健康ログ/通院：最大10枚（既存＋追加の合算）
- ユーザー合計：100MB
- EXIF除去・向き正規化：サーバー側でデコード→向き補正→再エンコード、メタデータ保持しない
- 保存先：
  - デフォルト画像：`wwwroot/images/default/`（静的）
  - アップロード画像：`wwwroot` 外（例：`<StorageRoot>/images/`）
  - 一時保存：`<StorageRoot>/tmp/`
- サーバ側検証：
  - **実データを画像としてデコードできないファイルは拒否**（拡張子/Content-Type が正しくても不可）
  - デコード後の画像は **幅・高さの最大辺4096px かつ 総画素数（幅×高さ）16,777,216px 以下** を許容し、これを超える場合は拒否する

---

### 1.5 画像配信 `GET /images/{imageId}` の共通仕様

#### 概要
- `ImageAsset.ImageId`（Guid）で画像を取得し、参照元（Avatar/HealthLog/Visit/PetPhoto）を辿って閲覧可否を判定して配信する。

#### 認証
- `[Authorize]`
  - 未ログイン：ログインへリダイレクト（302）

#### 入力
- Path
  - `imageId`：`Guid`（`ImageAsset.ImageId`）

#### 許可判定（参照元を辿って最終判定）
- Avatar：所有者のみ
- HealthLog：所有者のみ
- Visit：所有者のみ
- PetPhoto：`(参照元 Pet が IsPublic=true) OR (所有者)`

> 備考：非許可は「存在秘匿」のため 403 ではなく 404 を返す。

#### レスポンス（成功）
- Status：200（画像バイナリ）
- レスポンスヘッダ
  - `Cache-Control: private, no-store`
  - `Content-Type: ImageAsset.ContentType`
  - `Content-Disposition: inline`
  - `X-Content-Type-Options: nosniff`

#### エラー（404：存在秘匿を含む）
以下のいずれかの場合は 404 を返す（**権限なしも含めて 404 に統一**）：
- `ImageAsset` が存在しない
- `ImageAsset.Status = Pending`
- 参照元が辿れない（参照レコード削除済み等）
- 許可判定で非許可（存在秘匿）

#### キャッシュ
- 基本は上記推奨の `Cache-Control: private, no-store` とする。
- 必要に応じて最適化として `ETag` を付与し、再検証キャッシュ（例：`private, no-cache`）に切り替える運用を検討する。

---

### 1.6 HTTPステータス / 画面遷移（共通）
- GET 成功：200（HTML）
- POST 成功：原則 302 Redirect（PRGパターン）
  - リダイレクト先は原則 **`returnUrl`を優先**する（指定があり、かつローカルURLの場合のみ）。
  - `returnUrl` 未指定時は各エンドポイントに記載のデフォルトへ遷移する。
  - `returnUrl` は Open Redirect 対策のため `Url.IsLocalUrl(returnUrl)` などで検証し、非ローカルは無視する。
- **バリデーションエラー**
  - **画面を再表示するフォーム送信**（Create/Edit など）：200（同画面再表示、ModelState エラー表示）
  - **画面を持たない Action 系**（一覧トグル、削除など）：400（不正リクエスト）
- 400（不正リクエスト）の表示：**共通エラーページ（例：`/Error/400`）** によりユーザーへ説明を表示する（実装指針は「4. 備考」参照）
- 共通エラーページ：`GET /Error/{statusCode}`（例：`/Error/404`）
  - 認可：匿名可
  - 用途：`UseStatusCodePagesWithReExecute("/Error/{0}")` でのエラーハンドリング（表示専用）
  - `statusCode`：`400/403/404/500` など
- 未ログイン：302（ログインへ）
- 認可NG：403 または 404（存在秘匿ポリシーに従う）
- NotFound：404（対象レコードが存在しない）

---

### 1.7 クエリパラメータ命名規約
- **クエリパラメータ**は仕様書上の表記どおり **lowerCamelCase** を正とする（例：`nameKeyword`）。
- リンク生成・画面遷移・テストは **lowerCamelCase に統一**する。
- **フォーム項目名**は ViewModel プロパティ（PascalCase）になることがある（例：`Name`, `IsPublic`）。

---

### 1.8 固定コード一覧（参照用）

#### Species（種別）コード一覧（固定）
- 表示名 → 内部値（固定文字列）
  - 犬 → `DOG`
  - 猫 → `CAT`
  - ハムスター・モルモット → `HAMSTER_GUINEA_PIG`
  - うさぎ → `RABBIT`
  - その他の哺乳類 → `OTHER_MAMMAL`
  - 小鳥 → `BIRD`
  - お魚 → `FISH`
  - 亀 → `TURTLE`
  - 爬虫類・両生類 → `REPTILE_AMPHIBIAN`
  - 昆虫 → `INSECT`

※表示名（日本語）は変更しても内部値は変更しない。

#### ScheduleItemType（予定種別）コード一覧
- 表示名 → 内部値
  - ワクチン → `Vaccine`
  - 投薬 → `Medicine`
  - 通院 → `Visit`
  - その他 → `Other`

※表示名（日本語）は変更しても内部値は変更しない（内部値は DB/ロジックの判定に使用）。

---

### 1.9 URL 表記（大文字小文字）
- ASP.NET Core のルーティングは基本的に **大文字小文字を区別しない**（環境/ホスティングにより差が出ないよう、リンク生成は統一する）。
- 本仕様書では以下を正とする：
  - MVC ルート：`/Pets`, `/HealthLogs` 等（PascalCase）
  - 画像配信：`/images/{imageId}`（lowercase）
- 画面内リンク、テスト、ドキュメントは **本仕様書の表記に統一**する。

---

### 1.10 ID 型定義（共通）
- `petId` / `healthLogId` / `scheduleItemId` / `visitId`：`int`（1始まり）
- `userId`：`string`（ASP.NET Core Identity の UserId。DB上は `AspNetUsers.Id`（= `ApplicationUser.Id`））
- `imageId`：`guid`（例：`3fa85f64-5717-4562-b3fc-2c963f66afa6`）

---

### 1.11 日付/日時フォーマット（共通）
- **date-only**（例：`BirthDate`, `AdoptedDate`, `DueDate`, `VisitDate`）
  - 入力/送信：`yyyy-MM-dd`（例：`2026-01-11`）
  - 画面側は `<input type="date">` を推奨
- **date-time**（例：`RecordedAt`）
  - 画面側は `<input type="datetime-local">` を推奨（例：`2026-01-11T09:30`）
  - サーバ側は **JST（Asia/Tokyo, +09:00）として解釈し、`DateTimeOffset(+09:00)` として保存**する
  - もしオフセット付きで送る場合は ISO 8601（例：`2026-01-11T09:30:00+09:00`）を許容してもよい

---

### 1.12 戻り先指定 `returnUrl`（共通）
- Create/Edit/Delete/トグル等の POST は、**フォームに `returnUrl`（任意）を hidden で持たせる運用を**する。
- `returnUrl` が指定されており、かつローカルURLの場合：その URL にリダイレクトする。
- 未指定/不正の場合：各エンドポイントに記載のデフォルトへリダイレクトする。
- セキュリティ：Open Redirect 対策として **必ずローカルURL検証**を行う（`Url.IsLocalUrl` 等）。

---

### 1.13 フォームの配列送信（共通）
- 仕様書では「複数値」を分かりやすくするため `DeleteImageIds[]` / `NewFiles[]` のように `[]` を付けて表記することがある。
- **実際の HTML フォーム送信では、ASP.NET Core のモデルバインドに合わせて以下を推奨**：
  - 複数選択（チェックボックス等）：`name="DeleteImageIds"` を **同名で複数送信**（または `DeleteImageIds[0]`, `DeleteImageIds[1]` …）
  - 複数ファイル：`<input type="file" name="NewFiles" multiple>`（サーバ側は `List<IFormFile> NewFiles` 等で受け取る）
- 未送信は「空配列（0件）」として扱う（null 前提にしない）。

---

## 2. ルート一覧（サマリ）

> Controller/Action 対応は基本設計の URL 一覧に準拠。

### 2.1 一般（Main）
| 機能 | HTTP | URL | 認可 |
|---|---:|---|---|
| トップ | GET | `/` | 匿名可 |
| エラーページ | GET | `/Error/{statusCode}` | 匿名可 |
| MyPage | GET | `/MyPage` | 認証必須 |
| プロフィール編集 | GET/POST | `/Account/EditProfile` | 認証必須 |
| パスワード変更 | GET/POST | `/Account/Manage/ChangePassword` | 認証必須（Identity標準） |
| アカウント削除（確認） | GET | `/Account/Delete` | 認証必須 |
| アカウント削除（実行） | POST | `/Account/DeleteConfirmed` | 認証必須 |
| ペット一覧 | GET | `/Pets` | 認証必須 |
| ペット詳細 | GET | `/Pets/Details/{petId}` | 認証必須（公開 or 自分） |
| ペット作成 | GET/POST | `/Pets/Create` | 認証必須 |
| ペット編集 | GET/POST | `/Pets/Edit/{petId}` | 認証必須（所有者のみ） |
| ペット削除 | POST | `/Pets/Delete/{petId}` | 認証必須（所有者のみ） |
| 健康ログ一覧 | GET | `/HealthLogs?petId={petId}` | 認証必須（所有者のみ） |
| 健康ログ詳細 | GET | `/HealthLogs/Details/{healthLogId}` | 認証必須（所有者のみ） |
| 健康ログ作成 | GET/POST | `/HealthLogs/Create?petId={petId}` | 認証必須（所有者のみ） |
| 健康ログ編集 | GET/POST | `/HealthLogs/Edit/{healthLogId}` | 認証必須（所有者のみ） |
| 健康ログ削除 | POST | `/HealthLogs/Delete/{healthLogId}` | 認証必須（所有者のみ） |
| 予定一覧 | GET | `/ScheduleItems?petId={petId}` | 認証必須（所有者のみ） |
| 予定詳細 | GET | `/ScheduleItems/Details/{scheduleItemId}` | 認証必須（所有者のみ） |
| 予定作成 | GET/POST | `/ScheduleItems/Create?petId={petId}` | 認証必須（所有者のみ） |
| 予定編集 | GET/POST | `/ScheduleItems/Edit/{scheduleItemId}` | 認証必須（所有者のみ） |
| 予定完了トグル | POST | `/ScheduleItems/SetDone/{scheduleItemId}` | 認証必須（所有者のみ） |
| 予定削除 | POST | `/ScheduleItems/Delete/{scheduleItemId}` | 認証必須（所有者のみ） |
| 通院履歴一覧 | GET | `/Visits?petId={petId}` | 認証必須（所有者のみ） |
| 通院履歴詳細 | GET | `/Visits/Details/{visitId}` | 認証必須（所有者のみ） |
| 通院履歴作成 | GET/POST | `/Visits/Create?petId={petId}` | 認証必須（所有者のみ） |
| 通院履歴編集 | GET/POST | `/Visits/Edit/{visitId}` | 認証必須（所有者のみ） |
| 通院履歴削除 | POST | `/Visits/Delete/{visitId}` | 認証必須（所有者のみ） |
| 画像配信 | GET | `/images/{imageId}` | 認証必須（参照権限） |

---

### 2.2 管理者（Admin Area）
| 機能 | HTTP | URL | 認可 |
|---|---:|---|---|
| ユーザー一覧 | GET | `/Admin/Users` | Admin |
| ユーザー削除 | POST | `/Admin/Users/Delete/{userId}` | Admin |

---

## 3. エンドポイント詳細

### 3.1 Main
#### GET `/`
- 概要：トップページ
- 認可：匿名可
- 成功：200（HTML）

---

### 3.2 MyPage
#### GET `/MyPage`
- 概要：マイページ（ユーザー情報＋ペット一覧）
- 認可：認証必須
- 成功：200（HTML）

---

### 3.3 Account（プロフィール・削除）
#### GET `/Account/EditProfile`
- 概要：プロフィール編集画面表示（表示名、アバター）
- 認可：認証必須

#### POST `/Account/EditProfile`
- Content-Type：`multipart/form-data`
- フォーム項目（例）：
  - `DisplayName`：任意、最大50
  - `AvatarFile`：任意（画像ルールに準拠、2MB/ファイルなど）
- 成功：302 → `/MyPage`
- 失敗：
  - バリデーション：200（同画面）
  - 未ログイン：302（ログインへ）

#### GET `/Account/Manage/ChangePassword`
- 概要：パスワード変更画面（ASP.NET Core Identity 標準のスキャフォールドを利用）
- 認可：認証必須
- 備考：
  - 本機能は Identity 標準実装に準拠するため、**入力項目・バリデーション詳細は Identity の実装（画面/モデル）を正**とする。
  - 仕様上 `returnUrl` を扱う場合は「1.12 戻り先指定 `returnUrl`」に従う。

#### POST `/Account/Manage/ChangePassword`
- 概要：パスワード変更の実行（Identity 標準）
- 認可：認証必須
- Content-Type：`application/x-www-form-urlencoded`
- フォーム項目：Identity 標準（例：`OldPassword`, `NewPassword`, `ConfirmPassword`）
- 成功：302 → 既定の遷移（例：`/MyPage`）または `returnUrl`（指定がありローカルURLの場合）
- 失敗：
  - バリデーション：200（同画面）
  - 未ログイン：302（ログインへ）

#### GET `/Account/Delete`
- 概要：アカウント削除の確認
- 認可：認証必須

#### POST `/Account/DeleteConfirmed`
- 概要：アカウント削除の実行（関連データも物理削除）
- 認可：認証必須
- 成功：302 → `/`（もしくはログアウト後の遷移）
- 削除範囲（要件）：
  - ペット、健康ログ、健康ログ画像（関連テーブル + ImageAsset）、予定、通院履歴、通院履歴画像（関連テーブル + ImageAsset）
- 画像ファイル削除失敗時：
  - DB削除は継続し、失敗識別子（StorageKey等）を ILogger に出力

---

### 3.4 Pets
#### GET `/Pets`
- 概要：ペット一覧（公開検索 + 自分のペット）
- 認可：認証必須
- Query：
  - `nameKeyword`：部分一致（任意）
  - `speciesFilter`：種別フィルタ（任意）。値は **Species コード**（`DOG`, `CAT`, ...）または未指定（=すべて）
  - `page`：ページ番号（任意、1始まり。省略時1）
- ソート：`UpdatedAt` 降順（なければ `CreatedAt` 降順）
- 対象：
  - 自分のペット（公開/非公開問わず）
  - 他ユーザーの公開ペット（IsPublic=true）

#### GET `/Pets/Details/{petId}`
- 概要：ペット詳細
- 認可：認証必須
- アクセス制御：
  - 自分のペット：常に閲覧可
  - 他人のペット：`IsPublic=true` のみ閲覧可、`IsPublic=false` は 404
- 成功：200（HTML）
- 失敗：
  - 他人の非公開：404
  - 存在しない：404

#### GET `/Pets/Create`
- 概要：ペット作成画面
- 認可：認証必須（未ログインはログインへリダイレクト）
- Query：
  - `returnUrl`：任意（作成後に戻すURL）
    - 画面表示時に hidden へ引き継ぐ用途
    - **ローカルURLでない場合は破棄**（Open Redirect 対策）

#### POST `/Pets/Create`
- 概要：ペット新規作成（画像アップロード含む）
- 認可：認証必須（未ログインはログインへリダイレクト）
- Content-Type：`multipart/form-data`
- フォーム項目（代表）：
  - `Name`：必須、最大50
  - `SpeciesCode`：必須（値は **1.8 Species コード一覧**に従う：`DOG`, `CAT` など）
  - `Breed`：任意、最大100
  - `Sex`：任意、最大10
  - `BirthDate`：任意（`yyyy-MM-dd`）
  - `AdoptedDate`：任意（`yyyy-MM-dd`）
  - `IsPublic`：必須（**新規作成時の初期値は `true`**）
    - checkbox の場合、未送信にならないよう送信形式（hidden 併用等）に注意
  - `PhotoFile`：任意（画像ルールに準拠：拡張子/Content-Type/上限サイズなどは共通仕様に従う）
    - **未送信の場合**：画像は未設定（表示時はデフォルト画像を使用）
  - `returnUrl`：任意（作成後に戻すURL。**ローカルURLのみ有効**）
- 成功：
  - 302 → `returnUrl`（指定があり、かつローカルURLの場合）
  - `returnUrl` 未指定時：302 → `/MyPage`（標準）
- 失敗：
  - バリデーション：200（同画面再表示、エラーメッセージ表示）
  - 未ログイン：302（ログインへ）

#### GET `/Pets/Edit/{petId}`
- 概要：ペット編集画面
- 認可：認証必須（所有者のみ）
- Path：
  - `petId`：`int`
- Query：
  - `returnUrl`：任意（更新後に戻すURL）
    - hidden に引き継ぐ用途
    - **ローカルURLでない場合は破棄**
- アクセス制御：
  - 所有者以外：404（存在秘匿）
  - 対象ペット不存在：404

#### POST `/Pets/Edit/{petId}`
- 概要：ペット更新（画像更新含む）
- 認可：認証必須（所有者のみ）
- Content-Type：`multipart/form-data`
- フォーム項目：
  - Create と同等（`Name`, `SpeciesCode`, `Breed`, `Sex`, `BirthDate`, `AdoptedDate`, `IsPublic`, `PhotoFile`）
  - `RemovePhoto`：任意、bool（未送信は false 扱い）
  - `returnUrl`：任意（ローカルURLのみ有効）
- 画像更新ルール：
  1. `PhotoFile` が送られた場合：画像を **置換**（既存があれば削除）
     - `RemovePhoto=true` が同時でも **置換を優先**
  2. `PhotoFile` 未送信かつ `RemovePhoto=true`：既存画像を **削除**
  3. 上記以外：既存画像を **保持**
- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：`/Pets/Details/{petId}`
- 失敗：
  - バリデーション：200（同画面）
  - 未ログイン：302（ログインへ）
  - 認可NG / 不存在：404（存在秘匿）

#### POST `/Pets/Delete/{petId}`
- 概要：ペット削除（関連データも削除）
- 認可：認証必須（所有者のみ）
- セキュリティ：CSRF 対策必須（Anti-forgery）
- Path：
  - `petId`：`int`
- フォーム項目：
  - `returnUrl`：任意（削除後に戻すURL。ローカルURLのみ有効）
- 削除範囲：
  - ペット本体
  - 関連データ（例：健康ログ、通院履歴、予定、添付画像（ImageAsset 含む） 等）
  - 画像ファイルはストレージからも削除（失敗時はログ出力し、DB削除は継続）
- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：302 → `/MyPage`
- 失敗：
  - 未ログイン：302（ログインへ）
  - 対象ペット不存在：404
  - 認可NG（所有者以外）：404（存在秘匿）

---

### 3.5 HealthLogs
#### GET `/HealthLogs?petId={petId}`
- 概要：ペットごとの健康ログ一覧
- 認可：認証必須（所有者のみ）
- Query：
  - `petId`：必須（対象ペット）
  - `page`：任意（1始まり。省略時1）
- ソート：RecordedAt 降順、10件/ページ

#### GET `/HealthLogs/Details/{healthLogId}`
- 概要：健康ログ詳細（表示専用）
- 認可：認証必須（所有者のみ。所有者以外は存在秘匿）
- 成功：200（HTML）
- 画像：サムネ→拡大表示 `GET /images/{imageId}`

#### GET `/HealthLogs/Create?petId={petId}`
- 概要：健康ログ作成画面
- 認可：認証必須（所有者のみ）
- Query：
  - `petId`：必須
  - `returnUrl`：任意（作成後に戻すURL。画面では hidden に保持。ローカルURLのみ有効）
- 成功：200（HTML）
- 失敗：
  - 対象ペット不存在 / 認可NG：404（存在秘匿）

#### POST `/HealthLogs/Create`
- 概要：健康ログ新規作成（画像アップロード含む）
- 認可：認証必須（所有者のみ）
- Content-Type：`multipart/form-data`
- フォーム項目（代表）：
  - `PetId`：必須（作成対象ペットID）
  - `RecordedAt`：必須（date-time。共通仕様「1.11」参照 / JSTとして解釈）
  - `WeightKg`：任意（0.0〜200.0）
  - `FoodAmountGram`：任意（0〜5000）
  - `WalkMinutes`：任意（0〜1440）
  - `StoolCondition`：任意、最大50
  - `Note`：任意、最大1000
  - `NewFiles[]`：任意（複数ファイル。共通画像ルールに準拠）
  - `returnUrl`：任意（作成後に戻すURL。ローカルURLのみ有効）
- 画像制約：
  - 最大10枚（既存＋追加の合算）
  - ユーザー合計100MB
  - EXIF除去・向き正規化（共通画像ルールに従う）
- アクセス制御：
  - `PetId` の所有者がログインユーザーでない場合は **404（存在秘匿）**
  - `PetId` が存在しない場合も **404**
- 成功：302 → `returnUrl`（指定があり、かつローカルURLの場合）
  - `returnUrl` 未指定時：302 → `/HealthLogs?petId={PetId}`
- 失敗：
  - バリデーション：200（同画面再表示、エラーメッセージ表示）
  - 未ログイン：302（ログインへ）
  - 認可NG / 不存在：404（存在秘匿）

#### GET `/HealthLogs/Edit/{healthLogId}`
- 概要：健康ログ編集画面
- 認可：認証必須（所有者のみ）
- Query：
  - `returnUrl`：任意（更新後に戻すURL）
    - hidden に引き継ぐ用途
    - ローカルURLでない場合は破棄
- 成功：200（HTML）
- 失敗：
  - 対象ログ不存在：404
  - 認可NG：404（存在秘匿）

#### POST `/HealthLogs/Edit/{healthLogId}`
- Content-Type：`multipart/form-data`
- フォーム項目（代表）：
  - Create 同等
  - `DeleteImageIds[]`：任意（既存画像の削除対象 ImageId）
  - `returnUrl`：任意（更新後に戻すURL。ローカルURLのみ有効）
- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：`/HealthLogs/Details/{healthLogId}`
- 失敗：
  - バリデーション：200（同画面）
  - 未ログイン：302（ログインへ）
  - 認可NG：404（存在秘匿）
- 画像制約：最大10枚（既存＋追加の合算）、ユーザー合計100MB、EXIF除去

#### POST `/HealthLogs/Delete/{healthLogId}`
- 概要：健康ログ削除（添付画像含む）
- 認可：認証必須（所有者のみ）
- セキュリティ：CSRF 対策必須（Anti-forgery）

- フォーム項目（例）：
  - `petId`：任意（削除後のリダイレクト先の補助。未指定でも可）
    - **注意**：`petId` は改ざん可能。サーバ側は削除対象レコードから実際の `PetId` を復元し、
      `petId` が未指定/不一致でも **常に正しい `PetId`** を使ってデフォルト遷移先を組み立てる
      （`returnUrl` がある場合は `returnUrl` を優先）。
  - `page`：任意（一覧からの削除時に同じページへ戻す）
  - `returnUrl`：任意（削除後に戻すURL。ローカルURLのみ有効。指定時は `page` より優先）

- 削除範囲：
  - 健康ログ本体
  - 関連する添付画像（ImageAsset/中間テーブル/ストレージ上の画像ファイル）

- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：302 → `/HealthLogs?petId={PetId}&page={page}`
    - `PetId` は削除対象ログから復元した値を使用
    - `page` 省略時は付与しない

- 失敗：
  - バリデーション：400（不正リクエスト）
  - 未ログイン：302（ログインへ）
  - 認可NG / 不存在：404（存在秘匿）

---

### 3.6 ScheduleItems
#### GET `/ScheduleItems?petId={petId}`
- 概要：予定一覧
- 認可：認証必須（所有者のみ）
- Query：
  - `petId`：必須
  - `page`：任意（1始まり。省略時1）
  - `typeFilter`：任意（未指定=すべて。値は「予定種別コード一覧」に従う）
- ソート：`DueDate` 昇順（同日の場合は `Id` 昇順）
- ページング：10件/ページ
- 成功：200（HTML）
- 失敗：
  - `petId` 未指定/不正：400
  - 対象ペット不存在：404
  - 認可NG（所有者以外）：404

#### GET `/ScheduleItems/Details/{scheduleItemId}`
- 概要：予定詳細
- 認可：認証必須（所有者のみ）
- 成功：200（HTML）
- 失敗：
  - 認可NG：404（存在秘匿）
  - 存在しない：404

#### GET `/ScheduleItems/Create?petId={petId}`
- 概要：予定作成画面
- 認可：認証必須（所有者のみ）
- Query：
  - `petId`：必須
  - `returnUrl`：任意（作成後に戻すURL）
    - hidden に引き継ぐ用途
    - ローカルURLでない場合は破棄
- 成功：200（HTML）
- 失敗：
  - 対象ペット不存在 / 認可NG：404（存在秘匿）

#### POST `/ScheduleItems/Create`
- Content-Type：`application/x-www-form-urlencoded`
- フォーム項目（例）：
  - `PetId`：必須
  - `Title`：必須、最大100
  - `ItemType`：必須（固定コード）
  - `DueDate`：必須（`yyyy-MM-dd`）
  - `IsDone`：任意（既定 false）
  - `Note`：任意、最大1000
  - `returnUrl`：任意（作成後に戻すURL。ローカルURLのみ有効）
- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：`/ScheduleItems?petId={petId}`
- 失敗：
  - バリデーション：200（同画面）
  - 未ログイン：302（ログインへ）
  - 認可NG：404（存在秘匿）

#### GET `/ScheduleItems/Edit/{scheduleItemId}`
- 概要：予定編集画面
- 認可：認証必須（所有者のみ）
- Query：
  - `returnUrl`：任意（更新後に戻すURL）
    - hidden に引き継ぐ用途
    - ローカルURLでない場合は破棄
- 成功：200（HTML）
- 失敗：
  - 対象予定不存在：404
  - 認可NG（所有者以外）：404

#### POST `/ScheduleItems/Edit/{scheduleItemId}`
- Content-Type：`application/x-www-form-urlencoded`
- フォーム項目：Create と同等 + `returnUrl`（任意、ローカルURLのみ有効）
- 備考：
  - 完了状態（`IsDone`）の更新は原則 `POST /ScheduleItems/SetDone/{scheduleItemId}` を使用する（一覧トグル用）。
  - 編集画面で完了状態を変更するUIを提供する場合も、内部的には SetDone を呼び出す（推奨）。
- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：`/ScheduleItems/Details/{scheduleItemId}`
- 失敗：
  - バリデーション：200（同画面）
  - 未ログイン：302（ログインへ）
  - 認可NG：404（所有者以外）

#### POST `/ScheduleItems/SetDone/{scheduleItemId}`
- 概要：予定の完了状態（`IsDone`）を更新（一覧トグル用・冪等）
- 認可：認証必須（所有者のみ）
- Content-Type：`application/x-www-form-urlencoded`

- フォーム項目（例）：
  - `isDone`：必須（`true`/`false`）
  - `petId`：任意（更新後のリダイレクト先の補助。未指定でも可）
    - **注意**：サーバ側は scheduleItem から実際の `PetId` を復元し、
      `petId` が未指定/不一致でも **常に正しい `PetId`** を使ってデフォルト遷移先を組み立てる
      （`returnUrl` がある場合は `returnUrl` を優先）。
  - `page`：任意（更新後に同じページへ戻す場合）
  - `returnUrl`：任意（更新後に戻すURL。ローカルURLのみ有効。指定時は `page` より優先）

- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：302 → `/ScheduleItems?petId={PetId}&page={page}`
    - `PetId` は scheduleItem から復元した値を使用
    - `page` 省略時は付与しない

- 失敗：
  - バリデーション：400（不正な `isDone` 等）
  - 未ログイン：302（ログインへ）
  - 認可NG / 不存在：404

#### POST `/ScheduleItems/Delete/{scheduleItemId}`
- 概要：予定削除
- 認可：認証必須（所有者のみ）
- セキュリティ：CSRF 対策必須（Anti-forgery）
- Content-Type：`application/x-www-form-urlencoded`

- フォーム項目（例）：
  - `petId`：任意（削除後のリダイレクト先の補助。未指定でも可）
    - **注意**：`petId` は改ざん可能。サーバ側は削除対象レコードから実際の `PetId` を復元し、
      `petId` が未指定/不一致でも **常に正しい `PetId`** を使ってデフォルト遷移先を組み立てる
      （`returnUrl` がある場合は `returnUrl` を優先）。
  - `page`：任意（一覧からの削除時に同じページへ戻す）
  - `returnUrl`：任意（削除後に戻すURL。ローカルURLのみ有効。指定時は `page` より優先）

- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：302 → `/ScheduleItems?petId={PetId}&page={page}`
    - `PetId` は削除対象から復元した値を使用
    - `page` 省略時は付与しない

- 失敗：
  - バリデーション：400
  - 未ログイン：302（ログインへ）
  - 認可NG：404（所有者以外）
  - 対象予定不存在：404

---

### 3.7 Visits
#### GET `/Visits?petId={petId}`
- 概要：通院履歴一覧
- 認可：認証必須（所有者のみ）
- Query：
  - `petId`：必須
  - `page`：任意（1始まり。省略時1）
- ソート：VisitDate 降順、10件/ページ

#### GET `/Visits/Details/{visitId}`
- 概要：通院履歴詳細（表示専用）
- 認可：認証必須（所有者のみ）
- 成功：200（HTML）
- 画像：サムネ→拡大表示 `GET /images/{imageId}`

#### GET `/Visits/Create?petId={petId}`
- 概要：通院履歴作成画面
- 認可：認証必須（所有者のみ）
- Query：
  - `petId`：必須
  - `returnUrl`：任意（作成後に戻すURL）
    - hidden に引き継ぐ用途
    - ローカルURLでない場合は破棄
- 成功：200（HTML）
- 失敗：
  - 対象ペット不存在：404
  - 認可NG（所有者以外）：404

#### POST `/Visits/Create`
- Content-Type：`multipart/form-data`
- フォーム項目（代表）：
  - `PetId`：必須
  - `VisitDate`：必須（`yyyy-MM-dd`）
  - `ClinicName`：任意、最大100
  - `Diagnosis`：任意、最大500
  - `Prescription`：任意、最大500
  - `Note`：任意、最大1000
  - `NewFiles[]`：任意（複数、最大10枚、共通画像ルール）
  - `returnUrl`：任意（作成後に戻すURL。ローカルURLのみ有効）
- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：`/Visits?petId={petId}`
- 失敗：
  - バリデーション：200（同画面）
  - 未ログイン：302（ログインへ）
  - 認可NG：404（存在秘匿）
- 画像制約：最大10枚（既存＋追加の合算）、ユーザー合計100MB

#### GET `/Visits/Edit/{visitId}`
- 概要：通院履歴編集画面
- 認可：認証必須（所有者のみ）
- Query：
  - `returnUrl`：任意（更新後に戻すURL）
    - hidden に引き継ぐ用途
    - ローカルURLでない場合は破棄
- 成功：200（HTML）
- 失敗：
  - 対象履歴不存在：404
  - 認可NG（所有者以外）：404

#### POST `/Visits/Edit/{visitId}`
- 概要：通院履歴更新（画像の追加/削除含む）
- 認可：認証必須（所有者のみ）
- Content-Type：`multipart/form-data`

- フォーム項目：
  - Create 同等
  - `DeleteImageIds[]`：任意（既存画像の削除対象 ImageId）
  - `returnUrl`：任意（更新後に戻すURL。ローカルURLのみ有効）

- 画像制約：
  - 最大10枚（既存＋追加の合算）
  - ユーザー合計100MB（共通画像ルールに従う）

- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：302 → `/Visits/Details/{visitId}`

- 失敗：
  - バリデーション：200（同画面）
  - 未ログイン：302（ログインへ）
  - 認可NG（所有者以外）：404
  - 対象履歴不存在：404

#### POST `/Visits/Delete/{visitId}`
- 概要：通院履歴削除（添付画像含む）
- 認可：認証必須（所有者のみ）
- セキュリティ：CSRF 対策必須（Anti-forgery）
- Content-Type：`application/x-www-form-urlencoded`

- フォーム項目（例）：
  - `petId`：任意（削除後のリダイレクト先の補助。未指定でも可）
    - **注意**：`petId` は改ざん可能。サーバ側は削除対象レコードから実際の `PetId` を復元し、
      `petId` が未指定/不一致でも **常に正しい `PetId`** を使ってデフォルト遷移先を組み立てる
      （`returnUrl` がある場合は `returnUrl` を優先）。
  - `page`：任意（一覧からの削除時に同じページへ戻す）
  - `returnUrl`：任意（削除後に戻すURL。ローカルURLのみ有効。指定時は `page` より優先）

- 削除範囲：
  - 通院履歴本体
  - 関連する添付画像（ImageAsset/中間テーブル/ストレージ上の画像ファイル）

- 成功：302 → `returnUrl`（指定がありローカルURLの場合）
  - `returnUrl` 未指定時：302 → `/Visits?petId={PetId}&page={page}`
    - `PetId` は削除対象から復元した値を使用
    - `page` 省略時は付与しない

- 失敗：
  - バリデーション：400
  - 未ログイン：302（ログインへ）
  - 認可NG（所有者以外）：404
  - 対象履歴不存在：404

---

### 3.8 Images
#### GET `/images/{imageId}`
- 概要：ImageAsset の配信（健康ログ/通院/アバター/ペット写真を統一的に配信）
- 認証：`[Authorize]`（未ログインはログインへリダイレクト）
- 認可：参照元（Avatar/HealthLog/Visit/PetPhoto）を辿って判定（詳細は 1.5 に準拠）
- レスポンス/ヘッダ/404条件/キャッシュ等の詳細：**「1.5 画像配信 `GET /images/{imageId}` の共通仕様」**に準拠

---

### 3.9 Admin（管理者）
#### GET `/Admin/Users`
- 概要：ユーザー一覧
- 認可：Admin
- 成功：200（HTML）

#### POST `/Admin/Users/Delete/{userId}`
- 概要：任意ユーザーの削除（関連データ＋画像含む）
- 認可：Admin
- 成功：302 → `/Admin/Users`
- 失敗：
  - 未ログイン：302（ログインへ）
  - 認可NG：403（Admin以外）
  - 対象ユーザーが存在しない：404（`/Error/404`）
- 削除範囲／失敗時の扱い：アカウント削除と同様（要件参照）

---

## 4. 備考（実装指針）

- すべての POST には CSRF 対策（Anti-forgery）を適用すること（`[ValidateAntiForgeryToken]`）。
  - すべての POST フォームは Anti-forgery トークンを埋め込むこと（Razor の `@Html.AntiForgeryToken()` 相当）。
- グローバルフィルタとして `AutoValidateAntiforgeryToken` を有効化し、個別に付け忘れを防ぐ。
- `400/403/404` 等のステータスコードは共通エラーページにルーティングして表示する（例：`UseStatusCodePagesWithReExecute("/Error/{0}")`）。
- `returnUrl` を受け取る Action は、必ずローカルURL検証（`Url.IsLocalUrl` 等）を行い、非ローカルは無視する。
- 一覧のページングパラメータは **`page`（小文字）に統一**する。
- `page` の異常値は「1.3.1 `page` のバリデーション」に従い **1に補正**する。
- 一覧の検索条件クエリも **lowerCamelCase**（例：`nameKeyword`, `speciesFilter`）で統一する。
- 一覧のトグル操作（`IsDone`）は **`POST /ScheduleItems/SetDone/{scheduleItemId}`** を使用して更新する（編集 POST への流用はしない）。

---

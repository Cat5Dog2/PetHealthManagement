# ペット健康管理アプリ 基本設計書

- 文書名：ペット健康管理アプリ 基本設計書
- バージョン：1.0
- 作成日：2026-01-13
- 作成者：Cat5Dog2
- 想定読者：開発者本人、レビュー担当者

---

## 1. 前提・対象範囲

### 1.1 前提（要件との整合）
- 前提要件：**「ペット健康管理アプリ 要件定義書 v1.0」**
- 未ログインユーザーは機能画面へアクセスできず、ログイン画面へリダイレクト（Cookie 認証の既定動作）。
- 公開（`IsPublic`）は「**ログイン済みユーザー同士のみ共有**」（未ログイン公開は行わない）。
- 画像は **`wwwroot` 外**に保存し、認可付きエンドポイント **`GET /images/{imageId}`** 経由で配信（デフォルト画像のみ静的配信）。
- 健康ログの日時は **`RecordedAt`（`DateTimeOffset(+09:00)`）** を採用。
- ペットの公開状態 `IsPublic` の既定値は **true（公開）**。

### 1.2 実装技術
- .NET 10 (LTS) / ASP.NET Core MVC
- C# 14
- Entity Framework Core 10
- ASP.NET Core Identity

### 1.3 対象プラットフォーム・インフラ
- 開発環境：Windows
- 本番アプリホスト：Azure App Service（Linux、built-in .NET スタック）
- Web サーバ：Kestrel
- 通信：HTTPS（TLS）
  - 開発：`dotnet dev-certs https` を利用
- DB：RDB（EF Core + マイグレーション）
- 画像ストレージ：ファイルシステム（`StorageRoot` 配下、`wwwroot` 外）
- 機密情報（ConnectionStrings / StorageRoot 等）
  - ユーザーシークレット / 環境変数で管理（リポジトリへはコミットしない）

#### 1.3.1 App Service プラットフォーム決定
- 本アプリは **Azure App Service on Linux** を正とする。
- 理由
  - アプリは ASP.NET Core / .NET 10 であり、ASP.NET Framework や Windows 固有 API に依存していない。
  - 画像ストレージは `Path` ベースのファイルシステム抽象化で実装されており、OS 固有のパスに依存しない。
  - CI は `ubuntu-latest` を使っており、Linux ホスト前提との整合が取りやすい。
- Windows App Service は、ASP.NET Framework や Windows 固有依存が必要になった場合の例外選択肢とする。
- この決定は「画像保存を App Service のファイルシステムで継続するか」「Blob へ移行するか」までは確定しない。画像ストレージの長期方針は別タスクで扱う。

### 1.4 本書の範囲
- 画面/URL/Controller、ViewModel、DB設計（実装イメージ）、画像アップロード・保存・配信、削除フロー、バリデーション、エラー/ログ方針。
- UI の色・レイアウトの細部、E2E テスト設計は対象外。

---

## 2. システム構成・アーキテクチャ

### 2.1 レイヤ構成
- Presentation：MVC（Controller / View / ViewModel）
- Application：ユースケース（サービスクラス）
- Domain：エンティティ（EF Core）
- Infrastructure：DB（RDB）、画像ストレージ（ファイルシステム：将来 Blob へ差替え可能）

### 2.2 プロジェクト構成（案）
- `/Controllers`
  - `HomeController`
  - `MyPageController`
  - `PetsController`
  - `HealthLogsController`
  - `ScheduleItemsController`
  - `VisitsController`
  - `ImagesController`
  - `AccountController`（プロフィール/削除などの拡張）
- `/Areas/Admin/Controllers`
  - `UsersController`
- `/Models`（EF Core エンティティ）
- `/ViewModels`（画面ごとの DTO）
- `/Services`
  - `IImageService`（検証/加工/容量判定/DB連携）
  - `IImageStorageService`（保存/取得/削除：ファイルシステム実装）
  - `IUserDataDeletionService`（ユーザー削除）
  - `AuthorizationHelper`（所有者チェック・参照元辿り）
- `/wwwroot/images/default`（デフォルト画像のみ）
- `/Data`
  - `ApplicationDbContext`（Identity + アプリテーブル）

---

## 3. URL・コントローラ設計

### 3.1 主な URL 一覧
| 機能 | HTTP | URL | Controller / Action | 認可 |
|---|---:|---|---|---|
| トップ | GET | `/` | `HomeController.Index` | 匿名可 |
| 共通エラー | GET | `/Error/{statusCode}` | `ErrorController.Index` | 匿名可 |
| MyPage | GET | `/MyPage` | `MyPageController.Index` | 認証必須 |
| プロフィール編集 | GET/POST | `/Account/EditProfile` | `AccountController.EditProfile` | 認証必須 |
| パスワード変更 | GET/POST | `/Account/Manage/ChangePassword` | Identity 標準 | 認証必須 |
| アカウント削除（確認） | GET | `/Account/Delete` | `AccountController.Delete` | 認証必須 |
| アカウント削除（実行） | POST | `/Account/DeleteConfirmed` | `AccountController.DeleteConfirmed` | 認証必須 |
| ペット一覧（公開検索） | GET | `/Pets?page={page}` | `PetsController.Index` | 認証必須 |
| ペット詳細 | GET | `/Pets/Details/{petId}` | `PetsController.Details` | 認証必須 |
| ペット作成 | GET/POST | `/Pets/Create` | `PetsController.Create` | 認証必須 |
| ペット編集 | GET/POST | `/Pets/Edit/{petId}` | `PetsController.Edit` | 認証必須（所有者のみ） |
| ペット削除 | POST | `/Pets/Delete/{petId}` | `PetsController.Delete` | 認証必須（所有者のみ） |
| 健康ログ一覧 | GET | `/HealthLogs?petId={petId}&page={page}` | `HealthLogsController.Index` | 認証必須（所有者のみ） |
| 健康ログ詳細 | GET | `/HealthLogs/Details/{healthLogId}` | `HealthLogsController.Details` | 認証必須（所有者のみ） |
| 健康ログ作成 | GET/POST | `/HealthLogs/Create?petId={petId}` | `HealthLogsController.Create` | 認証必須（所有者のみ） |
| 健康ログ編集 | GET/POST | `/HealthLogs/Edit/{healthLogId}` | `HealthLogsController.Edit` | 認証必須（所有者のみ） |
| 健康ログ削除 | POST | `/HealthLogs/Delete/{healthLogId}` | `HealthLogsController.Delete` | 認証必須（所有者のみ） |
| 予定一覧 | GET | `/ScheduleItems?petId={petId}&page={page}` | `ScheduleItemsController.Index` | 認証必須（所有者のみ） |
| 予定詳細 | GET | `/ScheduleItems/Details/{scheduleItemId}` | `ScheduleItemsController.Details` | 認証必須（所有者のみ） |
| 予定作成 | GET/POST | `/ScheduleItems/Create?petId={petId}` | `ScheduleItemsController.Create` | 認証必須（所有者のみ） |
| 予定編集 | GET/POST | `/ScheduleItems/Edit/{scheduleItemId}` | `ScheduleItemsController.Edit` | 認証必須（所有者のみ） |
| 予定削除 | POST | `/ScheduleItems/Delete/{scheduleItemId}` | `ScheduleItemsController.Delete` | 認証必須（所有者のみ） |
| 予定完了トグル | POST | `/ScheduleItems/SetDone/{scheduleItemId}` | `ScheduleItemsController.SetDone` | 認証必須（所有者のみ） |
| 通院履歴一覧 | GET | `/Visits?petId={petId}&page={page}` | `VisitsController.Index` | 認証必須（所有者のみ） |
| 通院履歴詳細 | GET | `/Visits/Details/{visitId}` | `VisitsController.Details` | 認証必須（所有者のみ） |
| 通院履歴作成 | GET/POST | `/Visits/Create?petId={petId}` | `VisitsController.Create` | 認証必須（所有者のみ） |
| 通院履歴編集 | GET/POST | `/Visits/Edit/{visitId}` | `VisitsController.Edit` | 認証必須（所有者のみ） |
| 通院履歴削除 | POST | `/Visits/Delete/{visitId}` | `VisitsController.Delete` | 認証必須（所有者のみ） |
| 画像配信（統一） | GET | `/images/{imageId}` | `ImagesController.Get` | 認証必須 |


### 3.2 管理者（Admin Area）
| 機能 | HTTP | URL | Controller / Action | 認可 |
|---|---:|---|---|---|
| ユーザー一覧 | GET | `/Admin/Users` | `Areas.Admin.UsersController.Index` | Admin |
| ユーザー削除 | POST | `/Admin/Users/Delete/{id}` | `Areas.Admin.UsersController.Delete` | Admin |

### 3.3 戻り先 URL（returnUrl）共通仕様
- 未ログインで保護URLへアクセスした場合、ログイン画面へリダイレクトし、ログイン後は `returnUrl` に戻る。
- 登録／編集／削除／トグル更新等の POST 後の遷移先は `returnUrl` を優先できること。
- `returnUrl` は hidden フィールドまたはクエリとして受け取り、**ローカル URL のみ許可**する（`Url.IsLocalUrl(returnUrl)` 等で検証）。
  - 不正／未指定の場合は安全な既定遷移先へフォールバックする（例：一覧へ戻す）。
- 一覧（Index）→編集（Edit）等の導線では、遷移元の一覧URL（`page`/検索条件含む）を `returnUrl` として引き回す。

#### 3.3.1 予定完了トグル（SetDone）の注意
- `POST /ScheduleItems/SetDone/{scheduleItemId}`
  - 受け取り：`isDone`（必須）、`page`（任意）、`returnUrl`（任意）
  - 遷移：`returnUrl` が有効ならそこへ、無効なら一覧（`page` を維持）へ戻す。
- `petId` はクライアント改ざん可能なため、**サーバ側で `scheduleItemId` から PetId を復元**し、所有者チェックを行う。

---

## 4. 画面設計（概要）

### 4.1 MyPage（/MyPage）
- 自分のプロフィール（表示名、メール、アバター）を表示
- 自分のペット一覧（自分のみ）を表示
- アバター/ペット画像は `/images/{imageId}` またはデフォルト画像を表示

### 4.2 ペット一覧（/Pets）
- ページング：10件/ページ（クエリ `page`、1 始まり。未指定／非数／0 以下は 1）
- ソート：`UpdatedAt` 降順（なければ `CreatedAt` 降順）
- 検索条件：名前キーワード（部分一致）、種別（10択、未指定=すべて）
- 表示対象：
  - 自分のペット（公開/非公開問わず）
  - 他ユーザーの公開ペット（`IsPublic=true`）

### 4.3 ペット詳細（/Pets/Details/{petId}）
- 自分のペット：常に閲覧可
- 他ユーザーのペット：`IsPublic=true` の場合のみ閲覧可（`IsPublic=false` は 404）
- オーナーのみ：編集/削除、健康ログ/予定/通院履歴への導線を表示
  - 他ユーザーが `/Pets/Edit/{petId}` 等へ直接アクセスした場合も、存在秘匿のため原則 404

### 4.4 健康ログ（/HealthLogs）
- 所有者のみアクセス可（非所有者は 404：存在秘匿）
- 一覧：`RecordedAt` 降順、10件/ページ（クエリ `page`、1 始まり。未指定／非数／0 以下は 1）
- **詳細：`/HealthLogs/Details/{healthLogId}`（表示専用）**。一覧から遷移し、編集・削除へ導線を提供
- 登録・編集：`RecordedAt` 必須（`DateTimeOffset +09:00`）
- 画像：最大10枚（既存＋追加の合算）、ユーザー合計100MB制限、EXIF除去+向き正規化
  - 詳細画面では画像サムネ一覧→クリックで拡大表示（`GET /images/{imageId}`）

### 4.5 予定（/ScheduleItems）
- 所有者のみアクセス可（非所有者は 404：存在秘匿）
- 一覧：`DueDate` 昇順、10件/ページ（クエリ `page`、1 始まり。未指定／非数／0 以下は 1）
- **詳細：`/ScheduleItems/Details/{scheduleItemId}`（表示専用）**。一覧から遷移し、編集・削除へ導線を提供
- 種別（`Type`）は固定値推奨：`Vaccine` / `Medicine` / `Visit` / `Other`
- `IsDone`（完了フラグ）の切り替えを提供
  - 一覧上のトグル：`POST /ScheduleItems/SetDone/{scheduleItemId}`（`isDone` 必須、`page`/`returnUrl` 任意）

### 4.6 通院履歴（/Visits）
- 所有者のみアクセス可（非所有者は 404：存在秘匿）
- 一覧：`VisitDate` 降順、10件/ページ（クエリ `page`、1 始まり。未指定／非数／0 以下は 1）
- **詳細：`/Visits/Details/{visitId}`（表示専用）**。一覧から遷移し、編集・削除へ導線を提供
- 画像：最大10枚（既存＋追加の合算）、ユーザー合計100MB制限
  - 詳細画面では画像サムネ一覧→クリックで拡大表示（`GET /images/{imageId}`）

### 4.7 管理者（/Admin/Users）
- Admin のみアクセス可
- ユーザー一覧表示、ユーザー削除（関連データ＋画像含む）
- Admin でも他ユーザーの健康ログ等を「閲覧できる」特権は持たない（削除操作のみ）

---

## 5. ViewModel 設計（例）

### 5.1 共通：Species（種別）コードと表示名
- 入力・検索・保存は **内部値（固定文字列）**
- 表示は内部値→表示名へ変換（表示名変更があっても内部値は変えない）

| 表示名 | 内部値 |
|---|---|
| 犬 | `DOG` |
| 猫 | `CAT` |
| ハムスター・モルモット | `HAMSTER_GUINEA_PIG` |
| うさぎ | `RABBIT` |
| その他の哺乳類 | `OTHER_MAMMAL` |
| 小鳥 | `BIRD` |
| お魚 | `FISH` |
| 亀 | `TURTLE` |
| 爬虫類・両生類 | `REPTILE_AMPHIBIAN` |
| 昆虫 | `INSECT` |

> 変換は `SpeciesCatalog`（Dictionary）としてアプリ側に固定実装。

### 5.2 MyPage
```csharp
public class MyPageViewModel
{
    public string DisplayName { get; set; }
    public string Email { get; set; }
    public string AvatarUrl { get; set; } // /images/{imageId} or /images/default/...
    public List<MyPetSummaryViewModel> Pets { get; set; } = new();
}

public class MyPetSummaryViewModel
{
    public int PetId { get; set; }
    public string Name { get; set; }
    public string SpeciesLabel { get; set; }
    public string PhotoUrl { get; set; }
    public bool IsPublic { get; set; }
}
```

### 5.3 ペット検索
```csharp
public class PetSearchViewModel
{
    public string? NameKeyword { get; set; }
    public string? SpeciesFilter { get; set; } // null/empty=すべて, DOG/CAT/...（Speciesコード）
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public List<PetListItemViewModel> Pets { get; set; } = new();
}

public class PetListItemViewModel
{
    public int PetId { get; set; }
    public string Name { get; set; }
    public string SpeciesLabel { get; set; }
    public string? Breed { get; set; }
    public string OwnerDisplayName { get; set; }
    public string PhotoUrl { get; set; }
    public bool IsPublic { get; set; }
    public bool IsOwner { get; set; }
}
```

### 5.4 健康ログ編集（画像あり）
```csharp
public class HealthLogEditViewModel
{
    public int? HealthLogId { get; set; } // Create は null
    public int PetId { get; set; }
    public DateTimeOffset RecordedAt { get; set; } // 必須（+09:00）

    public double? WeightKg { get; set; }
    public int? FoodAmountGram { get; set; }
    public int? WalkMinutes { get; set; }
    public string? StoolCondition { get; set; }
    public string? Note { get; set; }

    public List<ImageItemViewModel> ExistingImages { get; set; } = new();
    public IFormFile[] NewFiles { get; set; } = Array.Empty<IFormFile>(); // 追加（未送信は空として扱う）
    public Guid[] DeleteImageIds { get; set; } = Array.Empty<Guid>(); // 削除対象（未送信は空として扱う）
}

public class ImageItemViewModel
{
    public Guid ImageId { get; set; }
    public string Url { get; set; } // /images/{imageId}
    public int SortOrder { get; set; }
}
```

### 5.5 通院履歴編集（画像あり）
- 健康ログと同様に `ExistingImages/NewFiles/DeleteImageIds` を持つ
- `NewFiles/DeleteImageIds` は未送信の場合でも **空として扱う**（null 前提にしない）

---

## 6. 認可・ステータスコード方針

### 6.1 未ログイン
- 機能画面：ログインへリダイレクト（Cookie 認証の既定動作）
- 画像：`GET /images/{imageId}` もログインへリダイレクト（`[Authorize]`）

### 6.2 ログイン済み：404（存在秘匿）
- 他人の非公開ペット（`IsPublic=false`）：404
- 他人のリソースへのアクセス（所有者不一致）：
  - ペット編集／削除
  - 健康ログ／予定／通院履歴（一覧・詳細・編集・削除・トグル更新）
  - 画像配信 `GET /images/{imageId}`（非許可・存在しない・参照元が辿れない等）
  - いずれも存在秘匿のため **404**

### 6.3 ログイン済み：403（権限不足）
- Admin エリア等、存在秘匿の必要が低い領域：
  - Admin 以外の Admin ルート：403
- それ以外は原則、存在秘匿したいリソース（ペット／健康ログ／予定／通院履歴／画像など）は 404 を優先する。

---

## 7. ドメインモデル / DB 設計（概要）

### 7.1 リレーション（要約）
- ApplicationUser (1) - (N) Pet  
- Pet (1) - (N) HealthLog  
- Pet (1) - (N) ScheduleItem  
- Pet (1) - (N) Visit  
- HealthLog (1) - (N) HealthLogImage - (1) ImageAsset  
- Visit (1) - (N) VisitImage - (1) ImageAsset  
- ApplicationUser (1) - (N) ImageAsset（OwnerId）

### 7.2 エンティティ（実装イメージ）

#### ApplicationUser（Identity 拡張）
```csharp
public class ApplicationUser : IdentityUser
{
    // UI 入力は任意だが、保存時はデフォルト値で埋める（空文字を許可しない想定）
    public string DisplayName { get; set; } // Max 50

    public Guid? AvatarImageId { get; set; }
    public ImageAsset? AvatarImage { get; set; }

    // ユーザー合計容量（100MB）
    public long UsedImageBytes { get; set; }

    // 同時更新対策（推奨）
    public byte[] RowVersion { get; set; } // [Timestamp]
}
```

#### Pet
```csharp
public class Pet
{
    public int Id { get; set; }
    public string OwnerId { get; set; }
    public ApplicationUser Owner { get; set; }

    public string Name { get; set; }          // Max 50
    public string SpeciesCode { get; set; }   // 固定コード（DOG など）
    public string? Breed { get; set; }        // Max 100
    public string? Sex { get; set; }          // Max 10
    public DateTime? BirthDate { get; set; }
    public DateTime? AdoptedDate { get; set; }

    public bool IsPublic { get; set; }        // default true

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? PhotoImageId { get; set; }
    public ImageAsset? PhotoImage { get; set; }
}
```

#### HealthLog / HealthLogImage（日時は RecordedAt）
```csharp
public class HealthLog
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; }

    public DateTimeOffset RecordedAt { get; set; } // +09:00

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public double? WeightKg { get; set; }        // 0.0〜200.0
    public int? FoodAmountGram { get; set; }     // 0〜5000
    public int? WalkMinutes { get; set; }        // 0〜1440
    public string? StoolCondition { get; set; }  // Max 50
    public string? Note { get; set; }            // Max 1000

    public ICollection<HealthLogImage> Images { get; set; } = new List<HealthLogImage>();
}

public class HealthLogImage
{
    public int Id { get; set; }
    public int HealthLogId { get; set; }
    public HealthLog HealthLog { get; set; }

    public Guid ImageId { get; set; }
    public ImageAsset Image { get; set; }

    public int SortOrder { get; set; }
}
```

#### ScheduleItem（DueDate + IsDone）
```csharp
public class ScheduleItem
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; }

    public DateTime DueDate { get; set; }
    public string Type { get; set; }    // 固定値推奨: Vaccine/Medicine/Visit/Other
    public string Title { get; set; }   // Max 100
    public string? Note { get; set; }   // Max 1000
    public bool IsDone { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

#### Visit / VisitImage（要件に合わせて最小構成）
```csharp
public class Visit
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; }

    public DateTime VisitDate { get; set; }
    public string? ClinicName { get; set; }     // Max 100
    public string? Diagnosis { get; set; }      // Max 500
    public string? Prescription { get; set; }   // Max 500
    public string? Note { get; set; }           // Max 1000

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<VisitImage> Images { get; set; } = new List<VisitImage>();
}

public class VisitImage
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public Visit Visit { get; set; }

    public Guid ImageId { get; set; }
    public ImageAsset Image { get; set; }

    public int SortOrder { get; set; }
}
```

#### ImageAsset（共通画像）
```csharp
public enum ImageAssetStatus { Pending, Ready }

public class ImageAsset
{
    public Guid ImageId { get; set; }            // PK
    public string StorageKey { get; set; }       // 例: images/{guid}.jpg
    public string ContentType { get; set; }      // image/jpeg, image/png, image/webp
    public long SizeBytes { get; set; }          // 保存後の実サイズ

    public string OwnerId { get; set; }          // FK: ApplicationUser
    public string Category { get; set; }         // Avatar, PetPhoto, HealthLog, Visit
    public ImageAssetStatus Status { get; set; } // Pending/Ready

    public DateTimeOffset CreatedAt { get; set; }
}
```

### 7.3 インデックス（例）
- Pet：`(OwnerId)`、`(IsPublic, SpeciesCode)`、`(Name)`
- HealthLog：`(PetId, RecordedAt DESC)`
- ScheduleItem：`(PetId, DueDate ASC)`
- Visit：`(PetId, VisitDate DESC)`
- ImageAsset：`(OwnerId, Status)`、`(CreatedAt)`

---

## 8. 画像アップロード・保存・配信 設計（要件 v1.1.1 追従）

### 8.1 保存先
- デフォルト画像：`wwwroot/images/default/`（静的）
- アップロード画像：`wwwroot` 外（例：`<StorageRoot>/images/`）
- 一時保存：`<StorageRoot>/tmp/`
- `StorageRoot` は `appsettings.json` / 環境変数から取得可能にする。
- Azure App Service on Linux では、`Storage__RootPath` は **`/home` 配下の絶対パス**を使う（例：`/home/pethealth-storage`）。
- デプロイ成果物の配置先とは分離するため、Production では相対パス既定値に依存せず、環境変数で絶対パスを与える。

### 8.2 StorageKey（命名規約）
- `images/{ImageId}.{ext}` を基本とする（GUID により衝突回避）
- `{ext}` は再エンコード後の形式に合わせる（jpg/png/webp）

### 8.3 画像検証（許可リスト）
- 拡張子：`.jpg/.jpeg/.png/.webp`
- Content-Type：`image/jpeg`, `image/png`, `image/webp`
- 可能なら実体判定（マジックナンバー）も行う（最低限、読み込みに失敗する画像は弾く）
- 1ファイル上限：2MB
- 健康ログ/通院：最大10枚（既存＋追加の合算）
- ユーザー合計：100MB
  - 判定：既存合計 + 今回合計
  - 超過：全体失敗（部分成功なし）

### 8.4 EXIF除去・Orientation 正規化
- サーバー側で画像を一度デコードし、Orientation を反映して向きを正規化する
- 再エンコードして保存し、メタデータ（EXIF 等）を保持しない

### 8.5 整合性（「一時保存 → DB更新 → 本保存」）
目的：DB とファイルの不整合を減らす

**フロー（複数ファイル対応）**
1. 受信ファイルを検証（形式/サイズ/枚数/容量見込み）
2. 各ファイルを加工（向き正規化+再エンコード）し、tmp に一時保存
3. DB トランザクション（推奨：`RowVersion` + 例外時リトライ）で
   - `UsedImageBytes` の上限判定（既存+今回）
   - `ImageAsset` を `Pending` で追加（`StorageKey` を確定）
   - `UsedImageBytes` を加算更新
4. コミット後、tmp → 本保存先へ移動
5. 本保存に成功した `ImageAsset` を `Ready` に更新

**失敗時（補償）**
- tmp 保存段階で失敗：tmp のみ削除して中断
- DB 更新後〜本保存で失敗：
  - tmp を削除
  - `ImageAsset` の削除、`UsedImageBytes` 巻き戻し等の補償を実施
  - 失敗内容（`StorageKey / ImageId`）を `ILogger` に出力
  - 画面には簡潔な失敗メッセージを返し、全体を失敗扱い

### 8.6 画像配信（GET /images/{imageId}）
- 認証：`[Authorize]`（未ログインはログインへリダイレクト）
- レスポンスヘッダ：
  - `Cache-Control: private, no-store`
  - `X-Content-Type-Options: nosniff`
  - `Content-Type`：`ImageAsset.ContentType`
  - `Content-Disposition`：原則 `inline`
- 404 条件：
  - `ImageAsset` 不存在 / `Status=Pending`
  - 参照元が解決できない（参照レコード削除済み等）
  - 非許可（存在秘匿）
- 許可判定（要件 v1.1.1）
  - Avatar：所有者のみ
  - HealthLog：所有者のみ
  - Visit：所有者のみ
  - PetPhoto：**(参照元 Pet が IsPublic=true) OR (所有者)**

> 実装上の注意：Category は補助情報とし、最終判定は **参照元（Pet/HealthLog/Visit/User）を辿って**行う。

---

## 9. 入力バリデーション設計（復活＋要件整合）

### 9.1 サーバーサイド（DataAnnotations + ModelState）
例：健康ログ

```csharp
public class HealthLogEditViewModel
{
    [Required]
    public DateTimeOffset RecordedAt { get; set; }

    [Range(0.0, 200.0)]
    public double? WeightKg { get; set; }

    [Range(0, 5000)]
    public int? FoodAmountGram { get; set; }

    [Range(0, 1440)]
    public int? WalkMinutes { get; set; }

    [MaxLength(50)]
    public string? StoolCondition { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }
}
```

### 9.2 主要項目の制約（目安）
- ユーザー
  - DisplayName：最大 50（未設定はデフォルト値を保存）
- ペット
  - Name：最大 50（必須）
  - SpeciesCode：必須（固定値）
  - Breed：最大 100
  - Sex：最大 10
- 予定
  - Type：最大 20（固定値推奨）
  - Title：最大 100（必須）
  - Note：最大 1000
- 通院
  - ClinicName：最大 100
  - Diagnosis / Prescription：最大 500
  - Note：最大 1000

### 9.3 画像アップロード（画面側）
- UX 向上のため拡張子チェック・サイズ/枚数表示を行う
- 実際の拒否はサーバー側で確実に行う

---

## 10. 削除・カスケード処理設計

### 10.1 方針
- DB は EF Core のカスケードに依存し過ぎず、**アプリケーションサービスで明示削除**する（画像削除のため）
- ファイル削除に失敗しても DB 削除は継続（`ILogger` に記録）

### 10.2 ユーザー自身のアカウント削除
- `AccountController.DeleteConfirmed` → `IUserDataDeletionService.DeleteUserAsync(userId)`

**概要フロー**
1. 対象ユーザーの関連データ（Pet/HealthLog/Schedule/Visit）と ImageAsset を列挙
2. 画像を 1件ずつ try/catch で削除（失敗はログ、処理継続）
3. 関連データを削除（トランザクション）
4. Identity ユーザーを削除

### 10.3 管理者によるユーザー削除
- `Areas.Admin.UsersController.Delete` → 同サービスを呼び出す
- Admin でも閲覧特権は持たず、「削除」操作のみ

### 10.4 ペット削除
- ペット配下（HealthLog/Schedule/Visit）と、それらの画像参照（ImageAsset）も削除対象
- 画像削除失敗はログ、DB削除は継続

---

## 11. エラーハンドリング・メッセージ（例）

- 400 / 403 / 404 等のエラーは、共通エラーページ `/Error/{statusCode}` を表示する
- 一覧画面上のトグル／削除など「画面を持たない」POST の入力不備（ID 不正、必須不足等）は 400 を返す
- 登録／編集など入力画面を持つ機能は、検証失敗時に同一画面へ戻してエラーメッセージを表示し、データは保存しない

- 画像形式不正：`対応していない画像形式です（JPEG/PNG/WebP のみ）。`
- 画像サイズ超過：`画像サイズが上限を超えています（1枚あたり最大2MB）。`
- 画像枚数超過：`添付できる画像は最大10枚です（既存分を含む）。`
- 容量超過：`画像の合計容量が上限（100MB）を超えます。不要な画像を削除してください。`
- 予期しない保存失敗：`画像の保存に失敗しました。時間をおいて再度お試しください。`

---

## 12. ログ・監査（簡易）
- `ILogger<T>` を利用
- 記録対象（例）
  - 画像保存/移動/削除の失敗（`ImageId/StorageKey/例外`）
  - ユーザー削除処理の開始/終了/例外

---

## 13. 今後の拡張余地（メモ）
- Azure Blob Storage への移行（`IImageStorageService` の差替え）
- Identity 拡張（メール確認、パスワードリセット、MFA）
- 画像のクライアント側圧縮・プレビュー
- エラーで残った画像ファイルの削除（運用バッチ）

---

以上

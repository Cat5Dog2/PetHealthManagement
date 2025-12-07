# ペット健康管理アプリ 基本設計書

* 文書名：ペット健康管理アプリ 基本設計書
* バージョン：1.0
* 作成日：2025-12-07
* 作成者：Cat5Dog2
* 想定読者：開発者本人、レビュー担当者

---

## 1. 前提・対象範囲

### 1.1 前提

* 要件は「ペット健康管理アプリ 要件定義書 v1.0」を前提とする。
* 実装技術
  * .NET 10 (LTS) / ASP.NET Core MVC
  * C# 14
  * Entity Framework Core 10
  * ASP.NET Core Identity

### 1.2 本書の範囲

* アプリ全体構成（レイヤ・プロジェクト構成）の概要
* コントローラ・URL・画面構成
* ドメインモデル（エンティティ）と DB マッピング概要
* アクセス制御（認証・認可・所有者チェック）
* 画像保存方式・命名規約
* 入力バリデーション方針
* 削除（カスケード）設計

---

## 2. システム構成・アーキテクチャ

### 2.1 レイヤ構成

個人開発・ポートフォリオ用途のため、下記のシンプルな 3 レイヤ構成とする。

* Presentation レイヤ
  * ASP.NET Core MVC（Controllers / Views / ViewModels）
* Application レイヤ
  * サービスクラス（ビジネスロジック）
  * 画像保存サービス、削除サービスなど
* Infrastructure / Data レイヤ
  * Entity Framework Core によるリポジトリ的役割（DbContext）
  * ASP.NET Core Identity の永続化

### 2.2 プロジェクト構成（案）

1 プロジェクト構成（Web アプリ単体）とし、名前空間で論理分割する。

* プロジェクト：`PetHealthManager`
  * `/Controllers`
    * `HomeController`
    * `MyPageController`
    * `PetsController`
    * `HealthLogsController`
    * `ScheduleItemsController`
    * `VisitsController`
    * `AccountController`（Identity 拡張部分）
    * `Admin/UsersController`（Areas/Admin）
  * `/Areas/Admin/Controllers`
  * `/Models`
    * エンティティ（EF Core 用）
  * `/ViewModels`
    * 画面毎の ViewModel
  * `/Services`
    * `IImageStorageService`
    * `FileSystemImageStorageService`
    * `IUserDataDeletionService`
    * `UserDataDeletionService`
    * 所有者チェック用ヘルパなど
  * `/Data`
    * `ApplicationDbContext`（Identity + アプリエンティティ）
  * `/Views`
    * 各 Controller の Views
  * `/wwwroot`
    * `/images/default`
    * `/upload/images/profile`
    * `/upload/images/pet`
    * `/upload/images/healthlog`
    * `/upload/images/visit`

---

## 3. URL・コントローラ設計

### 3.1 主な URL 一覧

| 機能           | HTTP     | URL パターン                            | Controller / Action                     |
| ------------ | -------- | ----------------------------------- | --------------------------------------- |
| トップページ       | GET      | `/`                                 | `HomeController.Index`                  |
| MyPage 表示    | GET      | `/MyPage`                           | `MyPageController.Index`                |
| プロフィール編集表示   | GET      | `/Account/EditProfile`              | `AccountController.EditProfile`         |
| プロフィール編集POST | POST     | `/Account/EditProfile`              | `AccountController.EditProfile`         |
| アカウント削除画面    | GET      | `/Account/Delete`                   | `AccountController.Delete`              |
| アカウント削除実行    | POST     | `/Account/DeleteConfirmed`          | `AccountController.DeleteConfirmed`     |
| 全ペット一覧       | GET      | `/Pets`                             | `PetsController.Index`                  |
| ペット詳細        | GET      | `/Pets/Details/{id}`                | `PetsController.Details`                |
| ペット登録画面      | GET      | `/Pets/Create`                      | `PetsController.Create`                 |
| ペット登録実行      | POST     | `/Pets/Create`                      | `PetsController.Create`                 |
| ペット編集画面      | GET      | `/Pets/Edit/{id}`                   | `PetsController.Edit`                   |
| ペット編集実行      | POST     | `/Pets/Edit/{id}`                   | `PetsController.Edit`                   |
| ペット削除確認      | GET      | `/Pets/Delete/{id}`                 | `PetsController.Delete`                 |
| ペット削除実行      | POST     | `/Pets/DeleteConfirmed/{id}`        | `PetsController.DeleteConfirmed`        |
| 健康ログ一覧       | GET      | `/HealthLogs?petId={petId}`         | `HealthLogsController.Index`            |
| 健康ログ詳細（任意）   | GET      | `/HealthLogs/Details/{id}`          | `HealthLogsController.Details`          |
| 健康ログ登録       | GET      | `/HealthLogs/Create?petId={petId}`  | `HealthLogsController.Create`           |
| 健康ログ登録 POST  | POST     | `/HealthLogs/Create`                | `HealthLogsController.Create`           |
| 健康ログ編集       | GET      | `/HealthLogs/Edit/{id}`             | `HealthLogsController.Edit`             |
| 健康ログ編集 POST  | POST     | `/HealthLogs/Edit/{id}`             | `HealthLogsController.Edit`             |
| 健康ログ削除       | GET      | `/HealthLogs/Delete/{id}`           | `HealthLogsController.Delete`           |
| 健康ログ削除 POST  | POST     | `/HealthLogs/DeleteConfirmed/{id}`  | `HealthLogsController.DeleteConfirmed`  |
| 予定一覧         | GET      | `/ScheduleItems?petId={petId}`      | `ScheduleItemsController.Index`         |
| 予定登録/編集/削除   | GET/POST | `/ScheduleItems/...`                | 同上                                      |
| 通院履歴一覧       | GET      | `/Visits?petId={petId}`             | `VisitsController.Index`                |
| 通院履歴登録/編集/削除 | GET/POST | `/Visits/...`                       | 同上                                      |
| 管理者：ユーザー一覧   | GET      | `/Admin/Users`                      | `Admin.UsersController.Index`           |
| 管理者：ユーザー削除確認 | GET      | `/Admin/Users/Delete/{id}`          | `Admin.UsersController.Delete`          |
| 管理者：ユーザー削除実行 | POST     | `/Admin/Users/DeleteConfirmed/{id}` | `Admin.UsersController.DeleteConfirmed` |

※ ログイン／ログアウト／パスワード変更は Identity の標準エンドポイントを利用。

---

## 4. 画面設計（概要）

### 4.1 MyPage 画面

* View：`Views/MyPage/Index.cshtml`
* ViewModel：`MyPageViewModel`

```csharp
public class MyPageViewModel
{
    public string DisplayName { get; set; }
    public string Email { get; set; }
    public string AvatarUrl { get; set; } // 実際に表示するパス（デフォルト適用済）
    public List<MyPetSummaryViewModel> Pets { get; set; }
}

public class MyPetSummaryViewModel
{
    public int PetId { get; set; }
    public string Name { get; set; }
    public string Species { get; set; }
    public string PhotoUrl { get; set; }
    public bool IsPublic { get; set; }
}
```

* 主な表示要素

  * ユーザー表示名、メールアドレス、プロフィール画像
  * 自ペット一覧（カード表示）
  * ボタン／リンク
    * プロフィール編集
    * パスワード変更
    * アカウント削除
    * ペットを登録

### 4.2 ペット一覧画面（/Pets/Index）

* ViewModel：`PetSearchViewModel`, `PetListItemViewModel`

```csharp
public class PetSearchViewModel
{
    public string NameKeyword { get; set; }
    public string SpeciesFilter { get; set; } // "All", "Dog", "Cat", "Other"
    public List<PetListItemViewModel> Pets { get; set; }
}

public class PetListItemViewModel
{
    public int PetId { get; set; }
    public string Name { get; set; }
    public string Species { get; set; }
    public string Breed { get; set; }
    public string OwnerDisplayName { get; set; }
    public string PhotoUrl { get; set; }
    public bool IsOwner { get; set; }
    public bool IsPublic { get; set; }
}
```

* 表示方針
  * IsPublic = true の他人ペット＋自分のペット全てを対象に検索
  * 所有者自身のペットは公開状態に関わらず表示

### 4.3 ペット詳細画面（/Pets/Details/{id}）

* ViewModel：`PetDetailsViewModel`

```csharp
public class PetDetailsViewModel
{
    public int PetId { get; set; }
    public string Name { get; set; }
    public string Species { get; set; }
    public string Breed { get; set; }
    public string Sex { get; set; }
    public DateTime? BirthDate { get; set; }
    public DateTime? AdoptedDate { get; set; }
    public string PhotoUrl { get; set; }
    public string OwnerDisplayName { get; set; }
    public bool IsPublic { get; set; }
    public bool IsOwner { get; set; }
}
```

* オーナーの場合のみ表示するリンク
  * 健康ログ一覧
  * 予定一覧
  * 通院履歴一覧
  * 編集・削除ボタン

### 4.4 健康ログ一覧・登録画面（例）

* 一覧：`HealthLogsController.Index`
  * 入力：`petId`
  * 表示：日付、体重、食事量、散歩時間、排せつ状態、メモ（概要）、画像の有無
* 登録：`HealthLogsController.Create`
  * 入力項目
    * 日付（必須）
    * 体重（任意・数値）
    * 食事量（任意・数値）
    * 散歩時間（任意・数値）
    * 排せつ状態（任意・文字列）
    * メモ（任意・文字列）
    * 画像ファイル（複数選択可）

同様に、予定・通院履歴画面も要件定義書の項目に沿って ViewModel を定義する。

---

## 5. ドメインモデル / DB 設計（概要）

### 5.1 エンティティクラス

#### 5.1.1 ApplicationUser

* Identity のユーザーに以下を追加。

```csharp
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } // Max 50, Required
    public string AvatarPath { get; set; }  // 相対パス or null
    public ICollection<Pet> Pets { get; set; }
}
```

#### 5.1.2 Pet

```csharp
public class Pet
{
    public int Id { get; set; }
    public string OwnerId { get; set; }
    public ApplicationUser Owner { get; set; }

    public string Name { get; set; }      // Required, Max 50
    public string Species { get; set; }   // Required, Max 50
    public string Breed { get; set; }     // Optional, Max 100
    public string Sex { get; set; }       // Optional, Max 10
    public DateTime? BirthDate { get; set; }
    public DateTime? AdoptedDate { get; set; }

    public string PhotoPath { get; set; } // 相対パス or null
    public bool IsPublic { get; set; }    // default true

    public ICollection<HealthLog> HealthLogs { get; set; }
    public ICollection<ScheduleItem> ScheduleItems { get; set; }
    public ICollection<Visit> Visits { get; set; }
}
```

#### 5.1.3 HealthLog / HealthLogImage

```csharp
public class HealthLog
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; }

    public DateTime Date { get; set; }
    public double? WeightKg { get; set; }        // 0.0〜200.0
    public int? FoodAmountGram { get; set; }     // 0〜5000
    public int? WalkMinutes { get; set; }        // 0〜1440
    public string StoolCondition { get; set; }   // Max 100
    public string Note { get; set; }             // Max 1000

    public ICollection<HealthLogImage> Images { get; set; }
}

public class HealthLogImage
{
    public int Id { get; set; }
    public int HealthLogId { get; set; }
    public HealthLog HealthLog { get; set; }

    public string ImagePath { get; set; } // /upload/images/healthlog/xxxx.jpg
    public int SortOrder { get; set; }
}
```

#### 5.1.4 ScheduleItem

```csharp
public class ScheduleItem
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; }

    public DateTime DueDate { get; set; }
    public string Type { get; set; }   // Max 20
    public string Title { get; set; }  // Max 100
    public string Note { get; set; }   // Max 500
    public bool IsDone { get; set; }
}
```

#### 5.1.5 Visit / VisitImage

```csharp
public class Visit
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; }

    public DateTime VisitDate { get; set; }
    public string ClinicName { get; set; }   // Max 100
    public string Diagnosis { get; set; }    // Max 500
    public string Prescription { get; set; } // Max 500
    public string Note { get; set; }         // Max 1000

    public ICollection<VisitImage> Images { get; set; }
}

public class VisitImage
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public Visit Visit { get; set; }

    public string ImagePath { get; set; } // /upload/images/visit/xxxx.jpg
    public int SortOrder { get; set; }
}
```

### 5.2 テーブル命名とマッピング

* ApplicationUser → `AspNetUsers`（Identity 標準テーブルに列追加）
* Pet → `Pets`
* HealthLog → `HealthLogs`
* HealthLogImage → `HealthLogImages`
* ScheduleItem → `ScheduleItems`
* Visit → `Visits`
* VisitImage → `VisitImages`

FK は EF Core の規約に従いつつ、`OnDelete(DeleteBehavior.Cascade)` を適宜設定。
ユーザー削除時はアプリケーション側で明示的に関連データ削除を行う（後述）。

---

## 6. アクセス制御・認可設計

### 6.1 認証

* ASP.NET Core Identity による Cookie 認証
* `[Authorize]` 属性により、未ログインユーザーのアクセスを制御
  * 例：`[Authorize]` を MyPage／Pets／HealthLogs／ScheduleItems／Visits／Admin エリアに付与

### 6.2 ロール

* 管理者ロール `"Admin"`
  * 管理者専用エリア：`[Authorize(Roles = "Admin")]`
  * 付与は DB 直接編集のみ（UI では変更不可）

### 6.3 所有者チェック

* ペット関連情報（ペット、健康ログ、予定、通院履歴）は「オーナーのみ CRUD 可」の要件。
* 実装方針
  * 共通ヘルパメソッド：

```csharp
public class AuthorizationHelper
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public Task<bool> IsPetOwnerAsync(int petId, ClaimsPrincipal user);
    public Task<bool> IsHealthLogOwnerAsync(int logId, ClaimsPrincipal user);
    public Task<bool> IsScheduleItemOwnerAsync(int itemId, ClaimsPrincipal user);
    public Task<bool> IsVisitOwnerAsync(int visitId, ClaimsPrincipal user);
}
```

* Controller 内でチェックし、NG の場合は `Forbid()`（403）を返す。

### 6.4 ペット公開設定による閲覧制御

* ペット詳細
  * `IsPublic = true` or オーナー本人 or 管理者 → 閲覧可
  * それ以外 → 404 を返す（存在秘匿も兼ねる）
* ペット一覧
  * クエリ時に `IsPublic == true` または `OwnerId == CurrentUserId` を条件に含める。

---

## 7. 画像保存・ファイル管理設計

### 7.1 保存先

要件定義書で指定されたパスをそのまま利用。

* `wwwroot/upload/images/profile/`
* `wwwroot/upload/images/pet/`
* `wwwroot/upload/images/healthlog/`
* `wwwroot/upload/images/visit/`
* デフォルト画像
  * `wwwroot/images/default/profile.png`
  * `wwwroot/images/default/pet.png`

### 7.2 ファイル名規約

* 重複回避と推測困難性のため、GUID ベースとする。

例）`{guid}{ext}`
`"3f1a9c8e-....-abcd.jpg"`

* 保存時の流れ（擬似コード）

```csharp
public async Task<string> SaveImageAsync(
    IFormFile file, ImageCategory category)
{
    // category からディレクトリを決定
    var uploadDir = GetDirectory(category); // e.g. "wwwroot/upload/images/pet"

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    var fileName = $"{Guid.NewGuid()}{ext}";
    var fullPath = Path.Combine(uploadDir, fileName);

    using var stream = new FileStream(fullPath, FileMode.Create);
    await file.CopyToAsync(stream);

    // DB には相対パスを保存
    return $"/upload/images/{category.ToFolderName()}/{fileName}";
}
```

### 7.3 デフォルト画像適用

* 表示用 URL プロパティ（例：`AvatarUrl`）は、
  * DB にパスがあればそれを使用
  * なければ `/images/default/profile.png` 等を返す

### 7.4 画像削除

* レコード削除時に対応するファイルを削除
  * `File.Exists` チェック → `File.Delete`
* 各サービス（例：`UserDataDeletionService`）から
  * プロフィール画像
  * ペット画像
  * 健康ログ画像
  * 通院履歴画像
    をまとめて削除。

### 7.5 逃げ道（Blob Storage など）

* `IImageStorageService` インターフェイスで抽象化し、実装クラスを差し替え可能とする。
  * `FileSystemImageStorageService`（現行）
  * 将来：`AzureBlobImageStorageService`（別プロジェクト or 実装クラス追加）
* コントローラからは `IImageStorageService` のみ参照する形にしておく。

---

## 8. 入力バリデーション設計

### 8.1 サーバーサイド

* DataAnnotations による属性付与。

例：HealthLog

```csharp
public class HealthLogEditViewModel
{
    [Required]
    public DateTime Date { get; set; }

    [Range(0.0, 200.0)]
    public double? WeightKg { get; set; }

    [Range(0, 5000)]
    public int? FoodAmountGram { get; set; }

    [Range(0, 1440)]
    public int? WalkMinutes { get; set; }

    [MaxLength(100)]
    public string StoolCondition { get; set; }

    [MaxLength(1000)]
    public string Note { get; set; }
}
```

* 文字列長などは要件定義の上限値に合わせる。

### 8.2 クライアントサイド

* ASP.NET Core MVC の unobtrusive validation を利用。
* DataAnnotations をそのままクライアント側に反映。

### 8.3 画像アップロードチェック

* 拡張子：`.jpg`, `.jpeg`, `.png` のみ許可
* Content-Type：`image/jpeg`, `image/png` のみ許可
* ファイルサイズ上限：2MB 程度（`IFormFile.Length` でチェック）
* 不正なファイルの場合は ModelState エラーとして画面にメッセージ表示。

---

## 9. 削除・カスケード処理設計

### 9.1 ユーザー自身のアカウント削除

* `IUserDataDeletionService.DeleteUserAsync(userId)` を用意。
* 処理内容
  1. 対象ユーザーのペット一覧取得
  2. 各ペットに紐づく
     * 健康ログ＋画像
     * 予定
     * 通院履歴＋画像
       をすべて取得
  3. 画像ファイル削除
  4. DB レコード削除（`SaveChanges` 一括）
  5. 最後にユーザー自身を削除（Identity）

### 9.2 管理者によるユーザー削除

* 基本的には自アカウント削除と同等ロジックを利用
  * サービスを共用し、呼び出し元だけ変える

### 9.3 ペット削除

* 単一ペット削除時も同様に、関連健康ログ／予定／通院履歴と画像を削除するサービスメソッドを用意。

---

## 10. エラーハンドリング・メッセージ

* 404
  * ID 不正／存在しない場合
  * 非公開ペットへの他人アクセス
* 403
  * 所有者チェック NG の場合（健康ログ・予定・通院履歴など）
* 500
  * 予期せぬ例外は、共通エラーページへ遷移
* ユーザー向けメッセージ
  * 画面上部に Alert（Bootstrap など）で表示
  * 成功時：緑（Success）、失敗時：赤（Danger）

---

## 11. ログ・監査（簡易）

* ASP.NET Core のロガー（`ILogger<T>`）を利用
* ログ出力対象
  * ログイン／ログアウト（Identity 標準）
  * ユーザー削除
  * 管理者によるユーザー削除
  * 画像保存・削除失敗

---

## 12. 今後の拡張余地（メモ）

* Identity 拡張機能
  * パスワードリセット、メールアドレス確認、ロックアウトの導入
* 画像保存先の Azure Blob Storage 化
* 通知機能（予定日前のリマインドメールなど）
* 多言語化（日本語／英語切替）

---

以上

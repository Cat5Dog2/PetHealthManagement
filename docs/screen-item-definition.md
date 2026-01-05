# ペット健康管理アプリ 画面項目定義書（v1.0）

- 作成日：2026-01-05  
- 参照：要件定義書 v1.0／基本設計書 v1.0／UIワイヤー／画面遷移図

---

## 1. 共通（全画面）

### 1.1 ヘッダ／ナビ
| 項目ID | 項目名 | 種別 | 表示条件 | 動作/遷移 |
|---|---|---|---|---|
| COM-01 | Logo | リンク | 常時 | `/` |
| COM-02 | MyPage | リンク | 認証時 | `/MyPage` |
| COM-03 | Pets | リンク | 認証時 | `/Pets` |
| COM-04 | Admin | リンク | Adminのみ | `/Admin/Users` |
| COM-05 | Logout | ボタン | 認証時 | ログアウト |

### 1.2 共通ルール（入力・画像）
| 区分 | ルール |
|---|---|
| ページング | 一覧は原則 10件/ページ |
| 画像アップロード | jpg/jpeg/png/webp、1ファイル最大2MB、ユーザー合計100MB（超過時は全体失敗）、健康ログ/通院は最大10枚（既存+追加合算） |
| 画像表示 | デフォルト画像は静的、それ以外は `GET /images/{id}`（認可付き） |
| 未ログイン | 保護画面はログインへリダイレクト |
| 非オーナー | 健康ログ/予定/通院は 403、非公開ペット参照は 404（存在秘匿） |

---

## 2. 画面別項目定義

以降、**「入力/表示」**列は `I=入力` / `D=表示` / `A=操作` を表します。

---

### SCR-001 Home（トップ）
- URL：`/`（匿名可）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 参照元 | 備考 |
|---|---|---:|---|---:|---|---|
| 001-01 | Login / Register | A | リンク |  |  | Identity UI へ |

---

### SCR-002 Login / Register（Identity標準）
- URL：Identity既定（匿名可）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 | 備考 |
|---|---|---:|---|---:|---|---|
| 002-01 | Email | I | メール | ✓ | Identity標準 | |
| 002-02 | Password | I | パスワード | ✓ | Identity標準 | |
| 002-03 | Confirm Password | I | パスワード | （登録時） | Identity標準 | |
| 002-04 | Login / Register | A | ボタン |  |  | 成功で `/MyPage` |

---

### SCR-003 MyPage
- URL：`/MyPage`（認証必須）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 参照元 | 備考 |
|---|---|---:|---|---:|---|---|
| 003-01 | 表示名 | D | テキスト |  | ApplicationUser.DisplayName | |
| 003-02 | Email | D | テキスト |  | ApplicationUser.Email | |
| 003-03 | Avatar | D | 画像 |  | ApplicationUser.AvatarImageId | `/images/{id}` or default |
| 003-04 | プロフィール編集 | A | リンク |  |  | `/Account/EditProfile` |
| 003-05 | パスワード変更 | A | リンク |  |  | Identity標準 |
| 003-06 | アカウント削除 | A | リンク |  |  | `/Account/Delete` |
| 003-07 | ＋ペット登録 | A | ボタン/リンク |  |  | `/Pets/Create` |
| 003-08 | 自分のペット一覧 | D | カード一覧 |  | Pet | サムネ/名前/公開バッジ/詳細 |

---

### SCR-004 プロフィール編集
- URL：`/Account/EditProfile`（認証必須）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 | 参照元 | 備考 |
|---|---|---:|---|---:|---|---|---|
| 004-01 | 表示名 | I | テキスト | 任意 | Max 50 | ApplicationUser.DisplayName | 未設定はデフォルト保存方針 |
| 004-02 | プロフィール画像 | I | ファイル | 任意 | 共通画像ルール | ApplicationUser.AvatarImageId | |
| 004-03 | 保存 | A | ボタン |  |  |  | |
| 004-04 | キャンセル | A | リンク |  |  |  | MyPageへ |

---

### SCR-005 アカウント削除（確認）
- URL：`/Account/Delete`（認証必須）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 備考 |
|---|---|---:|---|---:|---|
| 005-01 | 注意文（関連データ含め物理削除） | D | テキスト |  | |
| 005-02 | 削除実行 | A | ボタン |  | `POST /Account/DeleteConfirmed` |
| 005-03 | キャンセル | A | リンク |  | MyPageへ |

---

### SCR-006 ペット一覧（公開＋自分）
- URL：`/Pets`（認証必須）

**検索/絞り込み**
| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 | 参照元 |
|---|---|---:|---|---:|---|---|
| 006-F01 | 名前キーワード | I | テキスト |  | 部分一致 | Pet.Name |
| 006-F02 | 種別 | I | セレクト |  | 10択 + ALL | Pet.SpeciesCode |
| 006-F03 | 検索 | A | ボタン |  |  | |

**一覧（10件/ページ）**
| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 006-L01 | サムネ | D | 画像 | Pet.PhotoImageId | `/images/{id}` or default |
| 006-L02 | ペット名 | D | テキスト | Pet.Name | |
| 006-L03 | 種別 | D | テキスト | Pet.SpeciesCode | 表示はラベル変換 |
| 006-L04 | 品種 | D | テキスト | Pet.Breed | 任意 |
| 006-L05 | オーナー表示名 | D | テキスト | ApplicationUser.DisplayName | |
| 006-L06 | 公開/自分マーク | D | バッジ | Pet.IsPublic | |
| 006-L07 | 詳細 | A | リンク |  | `/Pets/Details/{id}` |
| 006-P01 | ページャ | D/A | ページャ |  | 10件/ページ、前へ/次へ、ページ番号 |

---

### SCR-007 ペット詳細
- URL：`/Pets/Details/{id}`（認証必須、公開 or オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 参照元 | 備考 |
|---|---|---:|---|---:|---|---|
| 007-01 | ペット画像（大） | D | 画像 |  | Pet.PhotoImageId | |
| 007-02 | ペット名 | D | テキスト |  | Pet.Name | |
| 007-03 | 種別 | D | テキスト |  | Pet.SpeciesCode | |
| 007-04 | 品種 | D | テキスト |  | Pet.Breed | |
| 007-05 | 性別 | D | テキスト |  | Pet.Sex | |
| 007-06 | 誕生日 | D | 日付 |  | Pet.BirthDate | |
| 007-07 | 迎えた日 | D | 日付 |  | Pet.AdoptedDate | |
| 007-08 | オーナー表示名 | D | テキスト |  | ApplicationUser.DisplayName | |
| 007-09 | 公開状態 | D | バッジ |  | Pet.IsPublic | |
| 007-A01 | 編集 | A | リンク |  |  | オーナーのみ `/Pets/Edit/{id}` |
| 007-A02 | 削除 | A | ボタン |  |  | オーナーのみ `POST /Pets/Delete/{id}` |
| 007-A03 | 健康ログ | A | リンク |  |  | オーナーのみ `/HealthLogs?petId={id}` |
| 007-A04 | 予定 | A | リンク |  |  | オーナーのみ `/ScheduleItems?petId={id}` |
| 007-A05 | 通院履歴 | A | リンク |  |  | オーナーのみ `/Visits?petId={id}` |

---

### SCR-008 ペット作成／編集
- URL：`/Pets/Create`、`/Pets/Edit/{id}`（認証必須、編集はオーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 | 参照元 |
|---|---|---:|---|---:|---|---|
| 008-01 | ペット名 | I | テキスト | ✓ | Max 50 | Pet.Name |
| 008-02 | 種別 | I | セレクト | ✓ | 10択 | Pet.SpeciesCode |
| 008-03 | 品種 | I | テキスト | 任意 | Max 100 | Pet.Breed |
| 008-04 | 性別 | I | テキスト/セレクト | 任意 | Max 10 | Pet.Sex |
| 008-05 | 誕生日 | I | 日付 | 任意 |  | Pet.BirthDate |
| 008-06 | 迎えた日 | I | 日付 | 任意 |  | Pet.AdoptedDate |
| 008-07 | 公開設定 | I | チェック | ✓ | bool（初期true） | Pet.IsPublic |
| 008-08 | ペット画像 | I | ファイル | 任意 | 共通画像ルール | Pet.PhotoImageId |
| 008-A01 | 保存 | A | ボタン |  |  |  |
| 008-A02 | キャンセル | A | リンク |  |  | 詳細へ（または一覧へ） |

---

### SCR-009 健康ログ一覧
- URL：`/HealthLogs?petId={id}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 009-01 | タイトル（{PetName}の健康ログ） | D | テキスト | Pet.Name | |
| 009-A01 | ＋健康ログ追加 | A | リンク |  | `/HealthLogs/Create?petId={id}` |
| 009-L01 | 記録日時 | D | 日時 | HealthLog.RecordedAt | 降順 |
| 009-L02 | 体重(kg) | D | 数値 | HealthLog.WeightKg | |
| 009-L03 | 食事量(g) | D | 数値 | HealthLog.FoodAmountGram | |
| 009-L04 | 活動(分) | D | 数値 | HealthLog.WalkMinutes | |
| 009-L05 | 排せつ | D | テキスト | HealthLog.StoolCondition | |
| 009-L06 | メモ（抜粋） | D | テキスト | HealthLog.Note | |
| 009-L07 | 画像あり | D | フラグ | HealthLog.Images | 有無表示 |
| 009-A02 | 詳細 | A | リンク |  | `/HealthLogs/Details/{id}` |
| 009-A03 | 編集 | A | リンク |  | `/HealthLogs/Edit/{id}` |
| 009-A04 | 削除 | A | ボタン |  | `POST /HealthLogs/Delete/{id}` |
| 009-P01 | ページャ | D/A | ページャ |  | 10件/ページ |

---

### SCR-010 健康ログ詳細
- URL：`/HealthLogs/Details/{id}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 010-01 | 記録日時 | D | 日時 | HealthLog.RecordedAt | |
| 010-02 | 体重(kg) | D | 数値 | HealthLog.WeightKg | |
| 010-03 | 食事量(g) | D | 数値 | HealthLog.FoodAmountGram | |
| 010-04 | 活動(分) | D | 数値 | HealthLog.WalkMinutes | |
| 010-05 | 排せつ | D | テキスト | HealthLog.StoolCondition | |
| 010-06 | メモ（全文） | D | テキスト | HealthLog.Note | |
| 010-07 | 画像サムネ一覧 | D | 画像一覧 | HealthLogImage → ImageAsset | クリックで `/images/{id}` |
| 010-A01 | 編集 | A | リンク |  | |
| 010-A02 | 削除 | A | ボタン |  | |
| 010-A03 | 一覧へ戻る | A | リンク |  | |

---

### SCR-011 健康ログ 作成／編集
- URL：`/HealthLogs/Create?petId={id}`、`/HealthLogs/Edit/{id}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 |
|---|---|---:|---|---:|---|
| 011-01 | 記録日時（RecordedAt） | I | 日時 | ✓ | DateTimeOffset（+09:00） |
| 011-02 | 体重(kg) | I | 数値 | 任意 | 0.0〜200.0 |
| 011-03 | 食事量(g) | I | 数値 | 任意 | 0〜5000 |
| 011-04 | 活動(分) | I | 数値 | 任意 | 0〜1440 |
| 011-05 | 排せつ状態 | I | テキスト | 任意 | Max 50 |
| 011-06 | メモ | I | テキストエリア | 任意 | Max 1000 |
| 011-07 | 画像追加 | I | ファイル(複数) | 任意 | 最大10枚（既存+追加）、共通画像ルール |
| 011-08 | 既存画像（編集時） | D/I | サムネ+削除指定 |  | 個別削除（DeleteImageIds）想定 |
| 011-A01 | 保存 | A | ボタン |  | |
| 011-A02 | キャンセル | A | リンク |  | 一覧へ |

---

### SCR-012 予定一覧
- URL：`/ScheduleItems?petId={id}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 012-01 | タイトル（{PetName}の予定） | D | テキスト | Pet.Name | |
| 012-A01 | ＋予定追加 | A | リンク |  | `/ScheduleItems/Create?petId={id}` |
| 012-L01 | 期日 | D | 日付 | ScheduleItem.DueDate | 昇順 |
| 012-L02 | 種別 | D | テキスト | ScheduleItem.Type | |
| 012-L03 | タイトル | D | テキスト | ScheduleItem.Title | |
| 012-L04 | メモ（抜粋） | D | テキスト | ScheduleItem.Note | |
| 012-L05 | 完了（IsDone） | I | トグル/チェック | ScheduleItem.IsDone | 一覧 or 詳細で更新可 |
| 012-A02 | 詳細 | A | リンク |  | `/ScheduleItems/Details/{id}` |
| 012-A03 | 編集 | A | リンク |  | |
| 012-A04 | 削除 | A | ボタン |  | |
| 012-P01 | ページャ | D/A | ページャ |  | 10件/ページ |

---

### SCR-013 予定詳細
- URL：`/ScheduleItems/Details/{id}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 |
|---|---|---:|---|---|
| 013-01 | 期日 | D | 日付 | ScheduleItem.DueDate |
| 013-02 | 種別 | D | テキスト | ScheduleItem.Type |
| 013-03 | タイトル | D | テキスト | ScheduleItem.Title |
| 013-04 | メモ（全文） | D | テキスト | ScheduleItem.Note |
| 013-05 | 完了（IsDone） | D/I | フラグ | ScheduleItem.IsDone |
| 013-A01 | 編集 | A | リンク |  |
| 013-A02 | 削除 | A | ボタン |  |
| 013-A03 | 一覧へ戻る | A | リンク |  |

---

### SCR-014 予定 作成／編集
- URL：`/ScheduleItems/Create?petId={id}`、`/ScheduleItems/Edit/{id}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 |
|---|---|---:|---|---:|---|
| 014-01 | 期日（DueDate） | I | 日付 | ✓ | 必須 |
| 014-02 | 種別（Type） | I | セレクト | ✓ | 固定推奨：Vaccine/Medicine/Visit/Other、Max 20 |
| 014-03 | タイトル | I | テキスト | ✓ | Max 100 |
| 014-04 | メモ | I | テキストエリア | 任意 | Max 500 |
| 014-05 | 完了（IsDone） | I | チェック | （編集時） | 新規は false 初期値 |
| 014-A01 | 保存 | A | ボタン |  | |
| 014-A02 | キャンセル | A | リンク |  | 一覧へ |

---

### SCR-015 通院履歴一覧
- URL：`/Visits?petId={id}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 015-01 | タイトル（{PetName}の通院履歴） | D | テキスト | Pet.Name | |
| 015-A01 | ＋通院履歴追加 | A | リンク |  | `/Visits/Create?petId={id}` |
| 015-L01 | 通院日 | D | 日付 | Visit.VisitDate | 降順 |
| 015-L02 | 病院名 | D | テキスト | Visit.ClinicName | |
| 015-L03 | 診断（抜粋） | D | テキスト | Visit.Diagnosis | |
| 015-L04 | 処方（抜粋） | D | テキスト | Visit.Prescription | |
| 015-L05 | メモ（抜粋） | D | テキスト | Visit.Note | |
| 015-L06 | 画像あり | D | フラグ | Visit.Images | |
| 015-A02 | 詳細 | A | リンク |  | `/Visits/Details/{id}` |
| 015-A03 | 編集 | A | リンク |  | |
| 015-A04 | 削除 | A | ボタン |  | |
| 015-P01 | ページャ | D/A | ページャ |  | 10件/ページ |

---

### SCR-016 通院履歴詳細
- URL：`/Visits/Details/{id}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 |
|---|---|---:|---|---|
| 016-01 | 通院日 | D | 日付 | Visit.VisitDate |
| 016-02 | 病院名 | D | テキスト | Visit.ClinicName |
| 016-03 | 診断（全文） | D | テキスト | Visit.Diagnosis |
| 016-04 | 処方（全文） | D | テキスト | Visit.Prescription |
| 016-05 | メモ（全文） | D | テキスト | Visit.Note |
| 016-06 | 画像サムネ一覧 | D | 画像一覧 | VisitImage → ImageAsset |
| 016-A01 | 編集 | A | リンク |  |
| 016-A02 | 削除 | A | ボタン |  |
| 016-A03 | 一覧へ戻る | A | リンク |  |

---

### SCR-017 通院履歴 作成／編集
- URL：`/Visits/Create?petId={id}`、`/Visits/Edit/{id}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 |
|---|---|---:|---|---:|---|
| 017-01 | 通院日（VisitDate） | I | 日付 | ✓ | 必須 |
| 017-02 | 病院名 | I | テキスト | 任意 | Max 100 |
| 017-03 | 診断 | I | テキストエリア | 任意 | Max 500 |
| 017-04 | 処方 | I | テキストエリア | 任意 | Max 500 |
| 017-05 | メモ | I | テキストエリア | 任意 | Max 1000 |
| 017-06 | 画像追加 | I | ファイル(複数) | 任意 | 最大10枚（既存+追加）、共通画像ルール |
| 017-07 | 既存画像（編集時） | D/I | サムネ+削除指定 |  | 個別削除想定 |
| 017-A01 | 保存 | A | ボタン |  | |
| 017-A02 | キャンセル | A | リンク |  | 一覧へ |

---

### SCR-018 管理者：ユーザー一覧
- URL：`/Admin/Users`（Adminのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 018-L01 | ユーザーID | D | テキスト | ApplicationUser.Id | （表示する場合） |
| 018-L02 | 表示名 | D | テキスト | ApplicationUser.DisplayName | |
| 018-L03 | Email | D | テキスト | ApplicationUser.Email | |
| 018-L04 | ペット登録数 | D | 数値 | Pet（集計） | 要件例に合わせ任意 |
| 018-A01 | 削除 | A | ボタン |  | `POST /Admin/Users/Delete/{id}` |
| 018-P01 | ページャ | D/A | ページャ |  | 10件/ページ |
| 018-01 | 注意文（関連データ含む物理削除） | D | テキスト |  | |

---

## 3. 補足（画面項目定義の粒度について）
- 本書は「**UIに出す/入れる項目**」を確定する目的のため、内部の ViewModel 名や厳密な型（例：`DateTimeOffset` の入力UI）は、実装時に MVC の入力部品に合わせて調整してください（ただし制約値は本書・基本設計・要件から逸脱しない）。

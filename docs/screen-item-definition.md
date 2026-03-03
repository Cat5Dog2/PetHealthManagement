# ペット健康管理アプリ 画面項目定義書（v1.0）

- 作成日：2026-01-16
- 参照：要件定義書 v1.0／基本設計書 v1.0／API仕様書 v1.0／ER図／UIワイヤー／画面遷移図

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

### 1.2 共通ルール（入力・画像・遷移）
| 区分 | ルール |
|---|---|
| ページング | 一覧は原則 10件/ページ。ページ番号はクエリ `page`（1始まり） |
| 画像アップロード | `jpg/jpeg/png/webp`、1ファイル最大2MB、ユーザー合計100MB（超過時は全体失敗）、健康ログ/通院は最大10枚（既存+追加合算） |
| 画像表示 | デフォルト画像は静的、それ以外は `GET /images/{id}`（認可付き） |
| 未ログイン | 保護画面はログインへリダイレクト（`returnUrl` で元ページへ復帰） |
| 存在秘匿 | 非公開ペット（他ユーザー）／健康ログ・予定・通院（非オーナー）は **404**（存在秘匿） |
| returnUrl | 登録/編集/削除/トグル更新などの POST 後は `returnUrl`（ローカルURLのみ許可）を優先。無効/未指定は安全な既定（例：一覧）へ |

---

## 2. 画面別項目定義

以降、**「I/D/A」**列は `I=入力` / `D=表示` / `A=操作` を表します。

---

### SCR-001 Home（トップ）
- URL：`/`（匿名可）
- 仕様：ログイン済みは `/MyPage` へ誘導（自動遷移 or ボタン表示）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 備考 |
|---|---|---:|---|---:|---|
| 001-01 | Login / Register | A | リンク |  | Identity UI へ |
| 001-02 | MyPageへ | A | リンク/ボタン |  | ログイン済み表示時（自動遷移運用の場合は省略可） |

---

### SCR-002 Login / Register（Identity標準）
- URL：Identity既定（匿名可）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 | 備考 |
|---|---|---:|---|---:|---|---|
| 002-01 | Email | I | メール | ✓ | Identity標準 | |
| 002-02 | Password | I | パスワード | ✓ | Identity標準 | |
| 002-03 | Confirm Password | I | パスワード | （登録時） | Identity標準 | |
| 002-04 | Login / Register | A | ボタン |  |  | 成功後：`returnUrl` があれば戻る、なければ `/MyPage` |

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
| 004-H01 | returnUrl | I | hidden | 任意 | ローカルURLのみ |  | Query→hidden→POST で受け渡し |
| 004-01 | 表示名 | I | テキスト | 任意 | Max 50 | ApplicationUser.DisplayName | 未設定はデフォルト保存方針 |
| 004-02 | プロフィール画像 | I | ファイル | 任意 | 共通画像ルール | ApplicationUser.AvatarImageId | |
| 004-A01 | 保存 | A | ボタン |  |  |  | POST後：`returnUrl` 優先、無効なら `/MyPage` |
| 004-A02 | キャンセル | A | リンク |  |  |  | `returnUrl` 優先、無効なら `/MyPage` |

---

### SCR-005 アカウント削除（確認）
- URL：`/Account/Delete`（認証必須）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 備考 |
|---|---|---:|---|---:|---|
| 005-H01 | returnUrl | I | hidden | 任意 | ローカルURLのみ。※削除完了後はセキュリティ上 `/` へ遷移固定でも可 |
| 005-01 | 注意文（関連データ含め物理削除） | D | テキスト |  | |
| 005-02 | 削除実行 | A | ボタン |  | `POST /Account/DeleteConfirmed`（完了後はログアウト状態） |
| 005-03 | キャンセル | A | リンク |  | `returnUrl` 優先、無効なら `/MyPage` |

---

### SCR-006 ペット一覧（公開＋自分）
- URL：`/Pets?nameKeyword={...}&speciesFilter={...}&page={page}`（認証必須）

**検索/絞り込み**
| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 送信名（クエリ） | 制約/形式 | 参照元 | 備考 |
|---|---|---:|---|---:|---|---|---|---|
| 006-F01 | 名前キーワード | I | テキスト |  | `nameKeyword` | 部分一致 | Pet.Name | |
| 006-F02 | 種別 | I | セレクト |  | `speciesFilter` | 10択 + ALL | Pet.SpeciesCode | ALL時は未指定（空）扱い |
| 006-F03 | 検索 | A | ボタン |  |  |  |  | 送信後：`page` は 1 に戻す |

**一覧（10件/ページ）**
| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 006-L01 | サムネ | D | 画像 | Pet.PhotoImageId | `/images/{id}` or default |
| 006-L02 | ペット名 | D | テキスト | Pet.Name | |
| 006-L03 | 種別 | D | テキスト | Pet.SpeciesCode | 表示はラベル変換 |
| 006-L04 | 品種 | D | テキスト | Pet.Breed | 任意 |
| 006-L05 | オーナー表示名 | D | テキスト | ApplicationUser.DisplayName | |
| 006-L06 | 公開/自分マーク | D | バッジ | Pet.IsPublic | |
| 006-L07 | 詳細 | A | リンク |  | `/Pets/Details/{id}?returnUrl={現在の一覧URL}` を推奨 |
| 006-P01 | ページャ | D/A | ページャ |  | クエリ `page` を更新 |

---

### SCR-007 ペット詳細
- URL：`/Pets/Details/{id}?returnUrl={...}`（認証必須、公開 or オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 参照元 | 備考 |
|---|---|---:|---|---:|---|---|
| 007-H01 | returnUrl | D/I | hidden（またはクエリ保持） | 任意 |  | 一覧から来た場合に付与推奨 |
| 007-01 | ペット画像（大） | D | 画像 |  | Pet.PhotoImageId | `/images/{id}` or default |
| 007-02 | ペット名 | D | テキスト |  | Pet.Name | |
| 007-03 | 種別 | D | テキスト |  | Pet.SpeciesCode | |
| 007-04 | 品種 | D | テキスト |  | Pet.Breed | |
| 007-05 | 性別 | D | テキスト |  | Pet.Sex | |
| 007-06 | 誕生日 | D | 日付 |  | Pet.BirthDate | |
| 007-07 | 迎えた日 | D | 日付 |  | Pet.AdoptedDate | |
| 007-08 | オーナー表示名 | D | テキスト |  | ApplicationUser.DisplayName | |
| 007-09 | 公開状態 | D | バッジ |  | Pet.IsPublic | |
| 007-A00 | 一覧へ戻る | A | リンク |  |  | `returnUrl` 優先、無効なら `/Pets` |
| 007-A01 | 編集 | A | リンク |  |  | オーナーのみ `/Pets/Edit/{id}?returnUrl={現在URL}` |
| 007-A02 | 削除 | A | ボタン |  |  | オーナーのみ `POST /Pets/Delete/{id}`（hidden `returnUrl` 推奨） |
| 007-A03 | 健康ログ | A | リンク |  |  | オーナーのみ `/HealthLogs?petId={id}` |
| 007-A04 | 予定 | A | リンク |  |  | オーナーのみ `/ScheduleItems?petId={id}` |
| 007-A05 | 通院履歴 | A | リンク |  |  | オーナーのみ `/Visits?petId={id}` |

---

### SCR-008 ペット作成／編集
- URL：`/Pets/Create?returnUrl={...}`、`/Pets/Edit/{id}?returnUrl={...}`（認証必須、編集はオーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 | 参照元 | 備考 |
|---|---|---:|---|---:|---|---|---|
| 008-H01 | returnUrl | I | hidden | 任意 | ローカルURLのみ |  | Query→hidden→POST で受け渡し |
| 008-01 | ペット名 | I | テキスト | ✓ | Max 50 | Pet.Name | |
| 008-02 | 種別 | I | セレクト | ✓ | 10択 | Pet.SpeciesCode | |
| 008-03 | 品種 | I | テキスト | 任意 | Max 100 | Pet.Breed | |
| 008-04 | 性別 | I | テキスト/セレクト | 任意 | Max 10 | Pet.Sex | |
| 008-05 | 誕生日 | I | 日付 | 任意 |  | Pet.BirthDate | |
| 008-06 | 迎えた日 | I | 日付 | 任意 |  | Pet.AdoptedDate | |
| 008-07 | 公開設定 | I | チェック | ✓ | bool（初期true） | Pet.IsPublic | |
| 008-08 | ペット画像 | I | ファイル | 任意 | 共通画像ルール | Pet.PhotoImageId | |
| 008-A01 | 保存 | A | ボタン |  |  |  | POST後：`returnUrl` 優先、無効なら（作成）`/Pets`／（編集）`/Pets/Details/{id}` |
| 008-A02 | キャンセル | A | リンク |  |  |  | `returnUrl` 優先、無効なら（作成）`/Pets`／（編集）`/Pets/Details/{id}` |

---

### SCR-009 健康ログ一覧
- URL：`/HealthLogs?petId={id}&page={page}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 009-01 | タイトル（{PetName}の健康ログ） | D | テキスト | Pet.Name | |
| 009-A01 | ＋健康ログ追加 | A | リンク |  | `/HealthLogs/Create?petId={id}&returnUrl={現在URL}` |
| 009-L01 | 記録日時 | D | 日時 | HealthLog.RecordedAt | 降順 |
| 009-L02 | 体重(kg) | D | 数値 | HealthLog.WeightKg | |
| 009-L03 | 食事量(g) | D | 数値 | HealthLog.FoodAmountGram | |
| 009-L04 | 活動(分) | D | 数値 | HealthLog.WalkMinutes | |
| 009-L05 | 排せつ | D | テキスト | HealthLog.StoolCondition | |
| 009-L06 | メモ（抜粋） | D | テキスト | HealthLog.Note | |
| 009-L07 | 画像あり | D | フラグ | HealthLog.Images | 有無表示 |
| 009-A02 | 詳細 | A | リンク |  | `/HealthLogs/Details/{id}?returnUrl={現在URL}` |
| 009-A03 | 編集 | A | リンク |  | `/HealthLogs/Edit/{id}?returnUrl={現在URL}` |
| 009-A04 | 削除 | A | ボタン |  | `POST /HealthLogs/Delete/{id}`（hidden `returnUrl` 推奨） |
| 009-P01 | ページャ | D/A | ページャ |  | クエリ `page` を更新 |

---

### SCR-010 健康ログ詳細
- URL：`/HealthLogs/Details/{id}?returnUrl={...}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 010-H01 | returnUrl | D/I | hidden（またはクエリ保持） |  | 一覧から来た場合に付与推奨 |
| 010-01 | 記録日時 | D | 日時 | HealthLog.RecordedAt | |
| 010-02 | 体重(kg) | D | 数値 | HealthLog.WeightKg | |
| 010-03 | 食事量(g) | D | 数値 | HealthLog.FoodAmountGram | |
| 010-04 | 活動(分) | D | 数値 | HealthLog.WalkMinutes | |
| 010-05 | 排せつ | D | テキスト | HealthLog.StoolCondition | |
| 010-06 | メモ（全文） | D | テキスト | HealthLog.Note | |
| 010-07 | 画像サムネ一覧 | D | 画像一覧 | HealthLogImage → ImageAsset | クリックで `/images/{id}` |
| 010-A01 | 編集 | A | リンク |  | `/HealthLogs/Edit/{id}?returnUrl={現在URL}` |
| 010-A02 | 削除 | A | ボタン |  | `POST /HealthLogs/Delete/{id}`（hidden `returnUrl` 推奨） |
| 010-A03 | 一覧へ戻る | A | リンク |  | `returnUrl` 優先、無効なら `/HealthLogs?petId={petId}` |

---

### SCR-011 健康ログ 作成／編集
- URL：`/HealthLogs/Create?petId={id}&returnUrl={...}`、`/HealthLogs/Edit/{id}?returnUrl={...}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 | 備考 |
|---|---|---:|---|---:|---|---|
| 011-H01 | returnUrl | I | hidden | 任意 | ローカルURLのみ | Query→hidden→POST で受け渡し |
| 011-01 | 記録日時（RecordedAt） | I | 日時 | ✓ | DateTimeOffset（+09:00） | |
| 011-02 | 体重(kg) | I | 数値 | 任意 | 0.0〜200.0 | |
| 011-03 | 食事量(g) | I | 数値 | 任意 | 0〜5000 | |
| 011-04 | 活動(分) | I | 数値 | 任意 | 0〜1440 | |
| 011-05 | 排せつ状態 | I | テキスト | 任意 | Max 50 | |
| 011-06 | メモ | I | テキストエリア | 任意 | Max 1000 | |
| 011-07 | 画像追加（`NewFiles[]`） | I | ファイル(複数) | 任意 | 最大10枚（既存+追加）、共通画像ルール | 追加分は `NewFiles[]` で送信 |
| 011-08 | 既存画像（編集時） | D | サムネ一覧 |  |  | 最大10枚合算の表示 |
| 011-09 | 既存画像の削除指定（`DeleteImageIds[]`） | I | hidden/チェック | 任意 | GUID配列 | 編集時のみ。削除対象IDを送信 |
| 011-A01 | 保存 | A | ボタン |  |  | POST後：`returnUrl` 優先、無効なら（作成）一覧へ／（編集）詳細へ |
| 011-A02 | キャンセル | A | リンク |  |  | `returnUrl` 優先、無効なら（作成）一覧へ／（編集）詳細へ |

---

### SCR-012 予定一覧
- URL：`/ScheduleItems?petId={id}&page={page}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 012-01 | タイトル（{PetName}の予定） | D | テキスト | Pet.Name | |
| 012-A01 | ＋予定追加 | A | リンク |  | `/ScheduleItems/Create?petId={id}&returnUrl={現在URL}` |
| 012-L01 | 期日 | D | 日付 | ScheduleItem.DueDate | 昇順 |
| 012-L02 | 種別 | D | テキスト | ScheduleItem.Type | |
| 012-L03 | タイトル | D | テキスト | ScheduleItem.Title | |
| 012-L04 | メモ（抜粋） | D | テキスト | ScheduleItem.Note | |
| 012-L05 | 完了（IsDone） | I | トグル/チェック | ScheduleItem.IsDone | 変更時に `SetDone` を呼ぶ（下記） |
| 012-A05 | 完了トグル更新 | A | 送信（行内） |  | `POST /ScheduleItems/SetDone/{scheduleItemId}`：`isDone`（必須）＋ `returnUrl`（任意）＋ `page`（任意） |
| 012-A02 | 詳細 | A | リンク |  | `/ScheduleItems/Details/{id}?returnUrl={現在URL}` |
| 012-A03 | 編集 | A | リンク |  | `/ScheduleItems/Edit/{id}?returnUrl={現在URL}` |
| 012-A04 | 削除 | A | ボタン |  | `POST /ScheduleItems/Delete/{id}`（hidden `returnUrl` 推奨） |
| 012-P01 | ページャ | D/A | ページャ |  | クエリ `page` を更新 |

---

### SCR-013 予定詳細
- URL：`/ScheduleItems/Details/{id}?returnUrl={...}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 013-H01 | returnUrl | D/I | hidden（またはクエリ保持） |  | 一覧から来た場合に付与推奨 |
| 013-01 | 期日 | D | 日付 | ScheduleItem.DueDate | |
| 013-02 | 種別 | D | テキスト | ScheduleItem.Type | |
| 013-03 | タイトル | D | テキスト | ScheduleItem.Title | |
| 013-04 | メモ（全文） | D | テキスト | ScheduleItem.Note | |
| 013-05 | 完了（IsDone） | D/I | フラグ/トグル | ScheduleItem.IsDone | 変更する場合は `SetDone` を呼ぶ |
| 013-A04 | 完了トグル更新 | A | 送信 |  | `POST /ScheduleItems/SetDone/{scheduleItemId}`（`isDone` 必須、`returnUrl` 任意） |
| 013-A01 | 編集 | A | リンク |  | `/ScheduleItems/Edit/{id}?returnUrl={現在URL}` |
| 013-A02 | 削除 | A | ボタン |  | `POST /ScheduleItems/Delete/{id}`（hidden `returnUrl` 推奨） |
| 013-A03 | 一覧へ戻る | A | リンク |  | `returnUrl` 優先、無効なら `/ScheduleItems?petId={petId}` |

---

### SCR-014 予定 作成／編集
- URL：`/ScheduleItems/Create?petId={id}&returnUrl={...}`、`/ScheduleItems/Edit/{id}?returnUrl={...}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 | 備考 |
|---|---|---:|---|---:|---|---|
| 014-H01 | returnUrl | I | hidden | 任意 | ローカルURLのみ | Query→hidden→POST で受け渡し |
| 014-01 | 期日（DueDate） | I | 日付 | ✓ | 必須 | |
| 014-02 | 種別（Type） | I | セレクト | ✓ | 固定推奨：Vaccine/Medicine/Visit/Other、Max 20 | |
| 014-03 | タイトル | I | テキスト | ✓ | Max 100 | |
| 014-04 | メモ | I | テキストエリア | 任意 | Max 1000 | |
| 014-05 | 完了（IsDone） | I | チェック | （編集時） | 新規は false 初期値 | |
| 014-A01 | 保存 | A | ボタン |  |  | POST後：`returnUrl` 優先、無効なら（作成）一覧へ／（編集）詳細へ |
| 014-A02 | キャンセル | A | リンク |  |  | `returnUrl` 優先、無効なら（作成）一覧へ／（編集）詳細へ |

---

### SCR-015 通院履歴一覧
- URL：`/Visits?petId={id}&page={page}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 015-01 | タイトル（{PetName}の通院履歴） | D | テキスト | Pet.Name | |
| 015-A01 | ＋通院履歴追加 | A | リンク |  | `/Visits/Create?petId={id}&returnUrl={現在URL}` |
| 015-L01 | 通院日 | D | 日付 | Visit.VisitDate | 降順 |
| 015-L02 | 病院名 | D | テキスト | Visit.ClinicName | |
| 015-L03 | 診断（抜粋） | D | テキスト | Visit.Diagnosis | |
| 015-L04 | 処方（抜粋） | D | テキスト | Visit.Prescription | |
| 015-L05 | メモ（抜粋） | D | テキスト | Visit.Note | |
| 015-L06 | 画像あり | D | フラグ | Visit.Images | |
| 015-A02 | 詳細 | A | リンク |  | `/Visits/Details/{id}?returnUrl={現在URL}` |
| 015-A03 | 編集 | A | リンク |  | `/Visits/Edit/{id}?returnUrl={現在URL}` |
| 015-A04 | 削除 | A | ボタン |  | `POST /Visits/Delete/{id}`（hidden `returnUrl` 推奨） |
| 015-P01 | ページャ | D/A | ページャ |  | クエリ `page` を更新 |

---

### SCR-016 通院履歴詳細
- URL：`/Visits/Details/{id}?returnUrl={...}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 016-H01 | returnUrl | D/I | hidden（またはクエリ保持） |  | 一覧から来た場合に付与推奨 |
| 016-01 | 通院日 | D | 日付 | Visit.VisitDate | |
| 016-02 | 病院名 | D | テキスト | Visit.ClinicName | |
| 016-03 | 診断（全文） | D | テキスト | Visit.Diagnosis | |
| 016-04 | 処方（全文） | D | テキスト | Visit.Prescription | |
| 016-05 | メモ（全文） | D | テキスト | Visit.Note | |
| 016-06 | 画像サムネ一覧 | D | 画像一覧 | VisitImage → ImageAsset | クリックで `/images/{id}` |
| 016-A01 | 編集 | A | リンク |  | `/Visits/Edit/{id}?returnUrl={現在URL}` |
| 016-A02 | 削除 | A | ボタン |  | `POST /Visits/Delete/{id}`（hidden `returnUrl` 推奨） |
| 016-A03 | 一覧へ戻る | A | リンク |  | `returnUrl` 優先、無効なら `/Visits?petId={petId}` |

---

### SCR-017 通院履歴 作成／編集
- URL：`/Visits/Create?petId={id}&returnUrl={...}`、`/Visits/Edit/{id}?returnUrl={...}`（認証必須、オーナーのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 制約/形式 | 備考 |
|---|---|---:|---|---:|---|---|
| 017-H01 | returnUrl | I | hidden | 任意 | ローカルURLのみ | Query→hidden→POST で受け渡し |
| 017-01 | 通院日（VisitDate） | I | 日付 | ✓ | 必須 | |
| 017-02 | 病院名 | I | テキスト | 任意 | Max 100 | |
| 017-03 | 診断 | I | テキストエリア | 任意 | Max 500 | |
| 017-04 | 処方 | I | テキストエリア | 任意 | Max 500 | |
| 017-05 | メモ | I | テキストエリア | 任意 | Max 1000 | |
| 017-06 | 画像追加（`NewFiles[]`） | I | ファイル(複数) | 任意 | 最大10枚（既存+追加）、共通画像ルール | 追加分は `NewFiles[]` で送信 |
| 017-07 | 既存画像（編集時） | D | サムネ一覧 |  |  | 最大10枚合算の表示 |
| 017-08 | 既存画像の削除指定（`DeleteImageIds[]`） | I | hidden/チェック | 任意 | GUID配列 | 編集時のみ |
| 017-A01 | 保存 | A | ボタン |  |  | POST後：`returnUrl` 優先、無効なら（作成）一覧へ／（編集）詳細へ |
| 017-A02 | キャンセル | A | リンク |  |  | `returnUrl` 優先、無効なら（作成）一覧へ／（編集）詳細へ |

---

### SCR-018 管理者：ユーザー一覧
- URL：`/Admin/Users?page={page}`（Adminのみ）

| 項目ID | 項目名 | I/D/A | 種別 | 参照元 | 備考 |
|---|---|---:|---|---|---|
| 018-01 | 注意文（関連データ含む物理削除） | D | テキスト |  | |
| 018-L01 | ユーザーID | D | テキスト | ApplicationUser.Id | （表示する場合） |
| 018-L02 | 表示名 | D | テキスト | ApplicationUser.DisplayName | |
| 018-L03 | Email | D | テキスト | ApplicationUser.Email | |
| 018-L04 | ペット登録数 | D | 数値 | Pet（集計） | 要件例に合わせ任意 |
| 018-A01 | 削除 | A | ボタン |  | `POST /Admin/Users/Delete/{id}`（hidden `returnUrl` 推奨） |
| 018-P01 | ページャ | D/A | ページャ |  | クエリ `page` を更新 |

---

### SCR-019 共通エラーページ
- URL：`/Error/{statusCode}`（匿名可）

| 項目ID | 項目名 | I/D/A | 種別 | 必須 | 備考 |
|---|---|---:|---|---:|---|
| 019-01 | ステータスコード | D | テキスト | ✓ | 例：400/403/404/500 |
| 019-02 | メッセージ | D | テキスト |  | ステータスに応じた文言 |
| 019-A01 | Home | A | リンク |  | `/` |
| 019-A02 | MyPage | A | リンク |  | 認証時のみ `/MyPage` |
| 019-A03 | 戻る | A | ボタン |  | ブラウザバック（任意） |

---

## 3. 補足（画面項目定義の粒度について）
- 本書は「**UIに出す/入れる項目**」を確定する目的のため、内部の ViewModel 名や厳密な型（例：`DateTimeOffset` の入力UI）は、実装時に MVC の入力部品に合わせて調整してください（ただし制約値は本書・基本設計・要件から逸脱しない）。
- `returnUrl` は **ローカルURLのみ許可**し、外部URLは破棄して安全な既定遷移にフォールバックしてください。

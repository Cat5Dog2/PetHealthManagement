# ペット健康管理アプリ テストケース表（画面単位） v1.0

- 作成日：2026-01-17
- 対象：ペット健康管理アプリ（Web / ASP.NET Core MVC）
- 参照：要件定義書 / 基本設計書 / API仕様書 / ER図 / 画面遷移図 / UIワイヤー / 画面項目定義書

---

## 証跡
- **SS**：画面キャプチャ（日時が分かる形で保存）
- **NT**：Networkログ（DevToolsのNetwork、リクエスト/レスポンス、302遷移先など）
- **DB**：DB確認（対象テーブルのレコード・件数・FK整合）
- **LOG**：サーバログ（画像削除失敗時のILogger出力など）

---

## 前提データ（テスト用）
- UserA：一般ユーザー、PetA（公開）/ PetA2（非公開）を所有、各種データあり
- UserB：一般ユーザー、他人リソースアクセス検証用
- Admin：管理者（ロール Admin）

---

## SCR-001 Home（トップ）
- URL：`/`（匿名可）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-001-001 | 未ログイン | 1) `/` にアクセス | 1) トップが表示される 2) Login/Register導線が表示される | SS |
| SCR-001-002 | ログイン済み（UserA） | 1) `/` にアクセス | 仕様どおりに `/MyPage` へ誘導（自動遷移 or MyPageボタン表示） | SS/NT |
| SCR-001-003 | 未ログイン | 1) トップの Login を押下 | IdentityのLogin画面へ遷移する | SS |

---

## SCR-002 Login / Register（Identity標準）
- URL：Identity既定（匿名可）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-002-001 | 未ログイン | 1) 保護URL（例：`/MyPage`）へ直アクセス | Login画面へ302リダイレクトされる（returnUrl付き） | NT |
| SCR-002-002 | 未ログイン | 1) 保護URLへ直アクセス → 2) 正しい資格情報でログイン | ログイン後、元のページ（returnUrl）へ戻る | SS/NT |
| SCR-002-003 | 未ログイン | 1) returnUrlに外部URL（`https://example.com`）を付けてログイン | 外部へは遷移せず、既定の安全な遷移先（例：`/MyPage`）へフォールバック | NT/SS |
| SCR-002-004 | 未ログイン | 1) 誤ったパスワードでログイン | Identity標準のエラー表示、ログイン失敗（セッションなし） | SS |
| SCR-002-005 | 未登録メール | 1) Registerでユーザー登録 → 2) ログイン | 登録→ログインできる（Identity標準） | SS |
| SCR-002-006 | 既登録メール | 1) Registerで同一メール登録 | Identity標準のエラー表示、登録不可 | SS |

---

## SCR-003 MyPage
- URL：`/MyPage`（認証必須）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-003-001 | 未ログイン | 1) `/MyPage` にアクセス | Loginへ302リダイレクト | NT |
| SCR-003-002 | ログイン済み（UserA） | 1) `/MyPage` にアクセス | 表示名/Email/アバター/自分のペット一覧が表示される | SS |
| SCR-003-003 | ログイン済み（UserA、ペット0件） | 1) `/MyPage` にアクセス | 「ペットを登録してください」等の案内が表示される | SS |
| SCR-003-004 | ログイン済み（UserA） | 1) ペットカードの詳細リンク押下 | `/Pets/Details/{petId}` に遷移する | SS |
| SCR-003-005 | ログイン済み（UserA） | 1) 「プロフィール編集」押下 | `/Account/EditProfile` に遷移する | SS |
| SCR-003-006 | ログイン済み（UserA） | 1) 「パスワード変更」押下 | Identity標準の変更画面へ遷移する | SS |
| SCR-003-007 | ログイン済み（UserA） | 1) 「アカウント削除」押下 | `/Account/Delete` に遷移する | SS |

---

## SCR-004 プロフィール編集
- URL：`/Account/EditProfile`（認証必須）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-004-001 | 未ログイン | 1) `/Account/EditProfile` にアクセス | Loginへ302リダイレクト | NT |
| SCR-004-002 | ログイン済み（UserA） | 1) GET表示 | 現在の表示名・アバターが表示される（未設定ならデフォルト） | SS |
| SCR-004-003 | ログイン済み（UserA） | 1) 表示名のみ変更し保存（returnUrl未指定） | 302で `/MyPage` へ遷移し、表示名が更新されている | SS/DB |
| SCR-004-004 | ログイン済み（UserA） | 1) 有効画像（jpg/png/webp、2MB以下）をアップロードして保存 | アバターが更新される（`GET /images/{id}` 経由） | SS/DB/NT |
| SCR-004-005 | ログイン済み（UserA） | 1) 2MB超の画像をアップロードして保存 | バリデーションエラーで同画面に戻り、保存されない | SS/DB |
| SCR-004-006 | ログイン済み（UserA） | 1) 許可外Content-Type/拡張子のファイルを指定して保存 | バリデーションエラーで同画面に戻り、保存されない | SS/DB |
| SCR-004-007 | ログイン済み（UserA） | 1) `returnUrl=/Pets?page=2` 付きで表示 → 2) 保存 | `returnUrl` 優先で `/Pets?page=2` に戻る | NT |
| SCR-004-008 | ログイン済み（UserA） | 1) `returnUrl=https://evil.example/` をhidden改ざん → 2) 保存 | 外部URLへは遷移せず安全な既定（例：`/MyPage`）へフォールバック | NT |
| SCR-004-009 | ログイン済み（UserA、画像合計が上限に近い） | 1) 有効画像（2MB以下）をアップロードして保存（合計100MB超になるように事前準備） | エラー表示、保存されない（ユーザー合計100MB制限） | SS/DB |

---

## SCR-005 アカウント削除（確認）
- URL：`/Account/Delete`（認証必須）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-005-001 | 未ログイン | 1) `/Account/Delete` にアクセス | Loginへ302リダイレクト | NT |
| SCR-005-002 | ログイン済み（UserA） | 1) GET表示 | 注意文が表示され、削除確認ができる | SS |
| SCR-005-003 | ログイン済み（UserA） | 1) キャンセル押下（returnUrl未指定） | `/MyPage` に戻る | SS/NT |
| SCR-005-004 | ログイン済み（UserA） | 1) 削除実行（POST `/Account/DeleteConfirmed`） | 1) アカウントおよび関連データが物理削除 2) ログアウト状態 3) `/` へ遷移 | SS/DB/NT |
| SCR-005-005 | ログイン済み（UserA） | 1) 削除後に保護URL（例：`/MyPage`）へアクセス | Loginへ302リダイレクト（ログアウト済み） | NT |
| SCR-005-006 | ログイン済み（UserA、故意にストレージ削除失敗を発生させる） | 1) 画像ファイルを読取専用にする等で削除失敗を誘発 → 2) アカウント削除 | DB削除は継続し、失敗識別子が LOG に出力される | DB/LOG |

---

## SCR-006 ペット一覧（公開＋自分）
- URL：`/Pets?nameKeyword={nameKeyword}&speciesFilter={speciesFilter}&page={page}`（認証必須）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-006-001 | 未ログイン | 1) `/Pets` にアクセス | Loginへ302リダイレクト | NT |
| SCR-006-002 | ログイン済み（UserA） | 1) `/Pets` にアクセス | 1) 自分のペット（公開/非公開問わず）が表示 2) 他人は公開ペットのみ表示 | SS |
| SCR-006-003 | ログイン済み（UserA） | 1) nameKeywordで部分一致検索 | フィルタ結果のみ表示、ページは1に戻る | SS/NT |
| SCR-006-004 | ログイン済み（UserA） | 1) speciesFilterで絞り込み（DOG等） | 該当種別のみ表示 | SS/NT |
| SCR-006-005 | ログイン済み（UserA） | 1) `page=abc` でアクセス | `page=1` 扱いで表示される | NT |
| SCR-006-006 | ログイン済み（UserA） | 1) `page=0` でアクセス | `page=1` 扱いで表示される | NT |
| SCR-006-007 | ログイン済み（UserA、データ11件以上） | 1) 2ページ目へ遷移 | 10件/ページでページングされる | SS |
| SCR-006-008 | ログイン済み（UserA） | 1) ペットの「詳細」リンク押下 | `/Pets/Details/{id}?returnUrl={現在の一覧URL}` で遷移できる | NT |
| SCR-006-009 | ログイン済み（UserA） | 1) 自分の非公開ペットが一覧に出ることを確認 | 自分のペットは非公開でも表示される（バッジ等） | SS |
| SCR-006-010 | ログイン済み（UserB） | 1) UserAの非公開ペットが一覧に出ないことを確認 | 他人の非公開ペットは表示されない | SS |
| SCR-006-011 | ログイン済み（UserA） | 1) `speciesFilter=ALL`（UIのALL相当）でアクセス | 種別未指定と同等で全件相当が表示される | SS/NT |
| SCR-006-012 | ログイン済み（UserA） | 1) `page=-1` でアクセス | `page=1` 扱いで表示される | NT |
| SCR-006-013 | ログイン済み（UserA） | 1) `page=99999` でアクセス | 正常表示（エラーなし）。該当データが無い場合は空一覧として表示される | SS/NT |

---

## SCR-007 ペット詳細
- URL：`/Pets/Details/{petId}?returnUrl=`（認証必須、公開 or オーナー）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-007-001 | ログイン済み（UserA、所有ペット） | 1) 自分のペット詳細を開く | 詳細が表示される | SS |
| SCR-007-002 | ログイン済み（UserB、UserAの公開ペット） | 1) 公開ペット詳細を開く | 詳細が表示される | SS |
| SCR-007-003 | ログイン済み（UserB、UserAの非公開ペット） | 1) 非公開ペット詳細を開く | 404（存在秘匿） | NT/SS |
| SCR-007-004 | ログイン済み（UserA） | 1) `returnUrl=/Pets?page=2` 付きで開く → 2) 一覧へ戻る | `returnUrl` 優先で `/Pets?page=2` に戻る | NT |
| SCR-007-005 | ログイン済み（UserA） | 1) `returnUrl=https://evil.example/` を付ける → 2) 一覧へ戻る | 外部URLへ戻らず、`/Pets` へ戻る等の安全フォールバック | NT |
| SCR-007-006 | ログイン済み（UserA） | 1) 編集リンク押下 | `/Pets/Edit/{id}` に遷移できる（オーナーのみ表示/実行可） | SS/NT |
| SCR-007-007 | ログイン済み（UserB、UserAの公開ペット） | 1) 詳細画面で編集/削除が表示されないこと確認 | オーナー以外に編集/削除導線が出ない | SS |
| SCR-007-008 | ログイン済み（UserA） | 1) 健康ログ/予定/通院履歴リンク押下 | 各一覧（`petId`付き）へ遷移できる（オーナーのみ） | SS |
| SCR-007-009 | ログイン済み（UserB、UserAの公開ペット） | 1) 健康ログ/予定/通院履歴への導線が出ないこと確認 | オーナー以外には健康情報への導線が出ない | SS |
| SCR-007-010 | ログイン済み（UserB） | 1) 直接 `/HealthLogs?petId={UserAのpetId}` を開く | 404（存在秘匿） | NT/SS |

---

## SCR-008 ペット作成／編集
- URL：`/Pets/Create?returnUrl={returnUrl}`、`/Pets/Edit/{petId}?returnUrl={returnUrl}`（認証必須、編集はオーナーのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-008-001 | 未ログイン | 1) `/Pets/Create` にアクセス | Loginへ302リダイレクト | NT |
| SCR-008-002 | ログイン済み（UserA） | 1) Create画面表示 | 入力フォームが表示される（IsPublicの初期値がtrue） | SS |
| SCR-008-003 | ログイン済み（UserA） | 1) 必須（Name/Species）未入力で保存 | バリデーションエラーで同画面に戻り保存されない | SS/DB |
| SCR-008-004 | ログイン済み（UserA） | 1) Name=51文字で保存 | バリデーションエラー | SS |
| SCR-008-005 | ログイン済み（UserA） | 1) 正常入力（画像なし）で保存 | 作成成功、**302で `/MyPage` へ遷移**（`returnUrl` 未指定時）、デフォルト画像で表示される | SS/NT/DB |
| SCR-008-006 | ログイン済み（UserA） | 1) 有効画像を添付して保存 | 作成成功、**302で `/MyPage` へ遷移**（`returnUrl` 未指定時）、画像が `GET /images/{id}` で表示される | SS/NT/DB |
| SCR-008-007 | ログイン済み（UserA） | 1) `returnUrl=/Pets?page=2` を付けてCreate→保存 | **302で `/Pets?page=2` へ遷移**（`returnUrl` 優先） | NT |
| SCR-008-008 | ログイン済み（UserA） | 1) `returnUrl=https://evil.example/` を付けてCreate→保存 | 外部へは遷移せず、**302で `/MyPage`（既定）へフォールバック** | NT |
| SCR-008-009 | ログイン済み（UserB、UserAのペット） | 1) `/Pets/Edit/{UserAのpetId}` を開く | 404（存在秘匿） | NT/SS |
| SCR-008-010 | ログイン済み（UserA） | 1) Edit画面表示 | 既存値が表示される | SS |
| SCR-008-011 | ログイン済み（UserA、既存写真あり） | 1) `RemovePhoto=true` のみで保存 | 既存写真が削除され、デフォルト画像に戻る | SS/DB |
| SCR-008-012 | ログイン済み（UserA、既存写真あり） | 1) `PhotoFile` を指定して保存 | 写真が置換される（既存があれば削除） | SS/DB/NT |
| SCR-008-013 | ログイン済み（UserA、既存写真あり） | 1) `PhotoFile` + `RemovePhoto=true` で保存 | 置換が優先され新画像が設定される | SS/DB |
| SCR-008-014 | ログイン済み（UserA） | 1) IsPublicをfalseにして保存 | 自分は詳細閲覧可能、他人からは404（存在秘匿）になる | SS/NT |
| SCR-008-015 | ログイン済み（UserA） | 1) キャンセル（returnUrl指定あり） | `returnUrl` 優先で戻る | NT |
| SCR-008-016 | ログイン済み（UserA） | 1) 削除ボタン実行（詳細画面から） | `POST /Pets/Delete/{id}` がCSRF有効で実行され、関連データも削除される | NT/DB |
| SCR-008-017 | ログイン済み（UserA、画像合計が上限に近い） | 1) Editで写真を添付して保存（合計100MB超になるように事前準備） | エラー表示、保存されない（ユーザー合計100MB制限） | SS/DB |

---

## SCR-009 健康ログ一覧
- URL：`/HealthLogs?petId={petId}&page={page}`（認証必須、オーナーのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-009-001 | 未ログイン | 1) `/HealthLogs?petId=1` にアクセス | Loginへ302リダイレクト | NT |
| SCR-009-002 | ログイン済み（UserA） | 1) 自分のペットの健康ログ一覧を開く | 一覧が表示（RecordedAt降順、10件/ページ） | SS |
| SCR-009-003 | ログイン済み（UserB、UserAのpetId） | 1) `/HealthLogs?petId={UserAのpetId}` を開く | 404（存在秘匿） | NT/SS |
| SCR-009-004 | ログイン済み（UserA） | 1) `page=0` で開く | `page=1` 扱い | NT |
| SCR-009-005 | ログイン済み（UserA、データ11件以上） | 1) 2ページ目へ移動 | 10件/ページでページング | SS |
| SCR-009-006 | ログイン済み（UserA） | 1) 「＋健康ログ追加」押下 | Create画面へ遷移（`petId` と `returnUrl` 付き） | NT |
| SCR-009-007 | ログイン済み（UserA） | 1) 一覧の行「詳細」押下 | Detailsへ遷移（returnUrl付き） | NT |
| SCR-009-008 | ログイン済み（UserA） | 1) 一覧の行「削除」実行 | 302で一覧へ戻り、対象ログが削除されている | NT/DB |
| SCR-009-009 | ログイン済み（UserA） | 1) `petId` 未指定でアクセス | 400 → `/Error/400` 表示 | NT/SS |
| SCR-009-010 | ログイン済み（UserA） | 1) `page=abc` で開く | `page=1` 扱い | NT |
| SCR-009-011 | ログイン済み（UserA） | 1) `page=-1` で開く | `page=1` 扱い | NT |
| SCR-009-012 | ログイン済み（UserA） | 1) `page=99999` で開く | 正常表示（エラーなし）。該当データが無い場合は空一覧として表示される | SS/NT |

---

## SCR-010 健康ログ詳細
- URL：`/HealthLogs/Details/{healthLogId}?returnUrl=`（認証必須、オーナーのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-010-001 | ログイン済み（UserA） | 1) 自分の健康ログ詳細を開く | 詳細が表示される | SS |
| SCR-010-002 | ログイン済み（UserB） | 1) UserAのhealthLogIdで詳細を開く | 404（存在秘匿） | NT/SS |
| SCR-010-003 | ログイン済み（UserA、画像あり） | 1) サムネをクリック | `GET /images/{imageId}` が呼ばれ画像が表示される | SS/NT |
| SCR-010-004 | ログイン済み（UserA） | 1) 編集押下 | Editへ遷移（returnUrl付き） | NT |
| SCR-010-005 | ログイン済み（UserA） | 1) 削除実行 | 302で一覧へ戻り、削除される | NT/DB |
| SCR-010-006 | ログイン済み（UserA） | 1) 一覧へ戻る（returnUrl指定あり） | returnUrlへ戻る | NT |
| SCR-010-007 | ログイン済み（UserA） | 1) returnUrlを外部へ改ざんして「戻る」 | 外部へは遷移せず安全フォールバック | NT |
| SCR-010-008 | ログイン済み（UserA） | 1) 不正なhealthLogId（存在しない）を開く | 404 | NT/SS |

---

## SCR-011 健康ログ 作成／編集
- URL：`/HealthLogs/Create?petId={petId}&returnUrl={returnUrl}`、`/HealthLogs/Edit/{healthLogId}?returnUrl={returnUrl}`（認証必須、オーナーのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-011-001 | ログイン済み（UserA） | 1) Create画面表示 | 記録日時（必須）等のフォームが表示される | SS |
| SCR-011-002 | ログイン済み（UserB、UserAのpetId） | 1) Create画面を開く | 404（存在秘匿） | NT/SS |
| SCR-011-003 | ログイン済み（UserA） | 1) RecordedAt未入力で保存 | バリデーションエラーで同画面に戻り保存されない | SS/DB |
| SCR-011-004 | ログイン済み（UserA） | 1) Weight=-0.1 で保存 | バリデーションエラー | SS |
| SCR-011-005 | ログイン済み（UserA） | 1) Weight=200.0 で保存 | 保存成功（境界値OK） | SS/DB |
| SCR-011-006 | ログイン済み（UserA） | 1) WalkMinutes=1441 で保存 | バリデーションエラー | SS |
| SCR-011-007 | ログイン済み（UserA） | 1) Note=1000文字で保存 | 保存成功（境界値OK） | SS/DB |
| SCR-011-008 | ログイン済み（UserA） | 1) Note=1001文字で保存 | バリデーションエラー | SS |
| SCR-011-009 | ログイン済み（UserA） | 1) 有効画像を1〜10枚添付して保存 | 保存成功、画像が紐付く（最大10枚） | SS/DB |
| SCR-011-010 | ログイン済み（UserA） | 1) 画像を11枚添付して保存 | バリデーションエラー（最大10枚超過） | SS/DB |
| SCR-011-011 | ログイン済み（UserA、画像合計が上限に近い） | 1) 合計100MB超になるようにアップロード | エラー表示、保存されない（ユーザー合計100MB制限） | SS/DB |
| SCR-011-012 | ログイン済み（UserA） | 1) CreateでreturnUrl指定→保存 | returnUrlへ戻る | NT |
| SCR-011-013 | ログイン済み（UserA） | 1) CreateでreturnUrl外部→保存 | 外部へは遷移しない（安全フォールバック） | NT |
| SCR-011-014 | ログイン済み（UserA、既存ログあり） | 1) Edit画面表示 | 既存値と既存画像サムネが表示される | SS |
| SCR-011-015 | ログイン済み（UserB） | 1) UserAのhealthLogIdでEditを開く | 404（存在秘匿） | NT/SS |
| SCR-011-016 | ログイン済み（UserA、既存画像あり） | 1) DeleteImageIdsで1枚削除指定→保存 | 指定画像が削除され、他は保持される | SS/DB |
| SCR-011-017 | ログイン済み（UserA、既存画像あり） | 1) 既存9枚 + 追加2枚で保存 | 最大10枚超過のためエラー（保存されない） | SS/DB |
| SCR-011-018 | ログイン済み（UserA） | 1) キャンセル（returnUrl指定あり） | returnUrlへ戻る | NT |
| SCR-011-019 | ログイン済み（UserA） | 1) returnUrlを外部へ改ざんしてキャンセル | 外部へは遷移しない | NT |

---

## SCR-012 予定一覧
- URL：`/ScheduleItems?petId={petId}&page={page}`（認証必須、オーナーのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-012-001 | 未ログイン | 1) `/ScheduleItems?petId=1` にアクセス | Loginへ302リダイレクト | NT |
| SCR-012-002 | ログイン済み（UserA） | 1) 自分のペットの予定一覧を開く | 一覧が表示（DueDate昇順、10件/ページ） | SS |
| SCR-012-003 | ログイン済み（UserA） | 1) `petId` 未指定でアクセス | 400 → `/Error/400` 表示 | NT/SS |
| SCR-012-004 | ログイン済み（UserB） | 1) UserAのpetIdでアクセス | 404（存在秘匿） | NT/SS |
| SCR-012-005 | ログイン済み（UserA） | 1) 「＋予定追加」押下 | Createへ遷移（petId/returnUrl付き） | NT |
| SCR-012-006 | ログイン済み（UserA） | 1) 行の「詳細」押下 | Detailsへ遷移（returnUrl付き） | NT |
| SCR-012-007 | ログイン済み（UserA） | 1) 行の完了トグルON（isDone=true） | `POST /ScheduleItems/SetDone/{id}` が呼ばれ、一覧へ戻り状態反映 | NT/SS/DB |
| SCR-012-008 | ログイン済み（UserA） | 1) 完了トグルOFF（isDone=false） | 冪等に更新でき、状態が反映 | NT/SS/DB |
| SCR-012-009 | ログイン済み（UserA） | 1) `isDone` を送らずにSetDoneを叩く（手動） | 400（不正リクエスト） | NT |
| SCR-012-010 | ログイン済み（UserA） | 1) 行の「削除」実行 | 302で一覧へ戻り削除される（petId/pageは対象から復元） | NT/DB |
| SCR-012-011 | ログイン済み（UserA） | 1) `page=abc` でアクセス | `page=1` 扱い | NT |
| SCR-012-012 | ログイン済み（UserA） | 1) `page=0` でアクセス | `page=1` 扱い | NT |
| SCR-012-013 | ログイン済み（UserA、データ11件以上） | 1) 2ページ目へ移動 | 10件/ページでページングされる | SS |
| SCR-012-014 | ログイン済み（UserA） | 1) `isDone=abc`（または `isDone=2`）で `POST /ScheduleItems/SetDone/{id}` を送る（手動） | 400（不正リクエスト） | NT |
| SCR-012-015 | ログイン済み（UserA） | 1) `petId` を不一致に改ざんして `POST /ScheduleItems/Delete/{id}` を送る（例：petId=999） | 302で一覧へ戻るが、遷移先の `petId` は削除対象から復元した正しい値になる（改ざん値ではない） | NT |
| SCR-012-016 | ログイン済み（UserA） | 1) `page=-1` でアクセス | `page=1` 扱い | NT |
| SCR-012-017 | ログイン済み（UserA） | 1) `page=99999` でアクセス | 正常表示（エラーなし）。該当データが無い場合は空一覧として表示される | SS/NT |

---

## SCR-013 予定詳細
- URL：`/ScheduleItems/Details/{scheduleItemId}?returnUrl=`（認証必須、オーナーのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-013-001 | ログイン済み（UserA） | 1) 自分の予定詳細を開く | 詳細が表示される | SS |
| SCR-013-002 | ログイン済み（UserB） | 1) UserAのscheduleItemIdで開く | 404（存在秘匿） | NT/SS |
| SCR-013-003 | ログイン済み（UserA） | 1) 完了トグル更新（ON/OFF） | SetDoneが呼ばれ状態が反映される | NT/SS |
| SCR-013-004 | ログイン済み（UserA） | 1) 編集押下 | Editへ遷移 | SS |
| SCR-013-005 | ログイン済み（UserA） | 1) 削除実行 | 一覧へ戻り削除される | NT/DB |
| SCR-013-006 | ログイン済み（UserA） | 1) 一覧へ戻る（returnUrl指定あり） | returnUrlへ戻る | NT |

---

## SCR-014 予定 作成／編集
- URL：`/ScheduleItems/Create?petId={petId}&returnUrl={returnUrl}`、`/ScheduleItems/Edit/{scheduleItemId}?returnUrl={returnUrl}`（認証必須、オーナーのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-014-001 | ログイン済み（UserA） | 1) Create画面表示 | フォームが表示される（DueDate/Type/Title必須） | SS |
| SCR-014-002 | ログイン済み（UserA） | 1) 必須未入力で保存 | バリデーションエラーで同画面に戻る | SS |
| SCR-014-003 | ログイン済み（UserA） | 1) Title=101文字で保存 | バリデーションエラー | SS |
| SCR-014-004 | ログイン済み（UserA） | 1) 正常入力で保存 | 一覧へ戻り新規予定が表示される | SS/DB |
| SCR-014-005 | ログイン済み（UserA） | 1) returnUrl指定で保存 | returnUrlへ戻る | NT |
| SCR-014-006 | ログイン済み（UserB） | 1) UserAのscheduleItemIdでEditを開く | 404（存在秘匿） | NT/SS |
| SCR-014-007 | ログイン済み（UserA） | 1) Editで内容を更新して保存 | 更新が反映される（returnUrl未指定ならDetailsへ） | SS/DB |
| SCR-014-008 | ログイン済み（UserA） | 1) キャンセル（returnUrl指定） | returnUrlへ戻る | NT |
| SCR-014-009 | ログイン済み（UserA） | 1) returnUrl外部を指定して保存 | 外部へ遷移せず安全フォールバック | NT |
| SCR-014-010 | ログイン済み（UserA） | 1) Editで完了状態変更UIがある場合に操作 | 内部的にSetDone相当で更新される（推奨仕様どおり） | NT/DB |
| SCR-014-011 | ログイン済み（UserA） | 1) Note=1000文字で保存 | 保存成功（境界値OK） | SS/DB |
| SCR-014-012 | ログイン済み（UserA） | 1) Note=1001文字で保存 | バリデーションエラー | SS |

---

## SCR-015 通院履歴一覧
- URL：`/Visits?petId={petId}&page={page}`（認証必須、オーナーのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-015-001 | 未ログイン | 1) `/Visits?petId=1` にアクセス | Loginへ302リダイレクト | NT |
| SCR-015-002 | ログイン済み（UserA） | 1) 自分の通院履歴一覧を開く | 一覧が表示（VisitDate降順、10件/ページ） | SS |
| SCR-015-003 | ログイン済み（UserA） | 1) `petId` 未指定でアクセス | 400 → `/Error/400` 表示（運用方針に従う） | NT/SS |
| SCR-015-004 | ログイン済み（UserB） | 1) UserAのpetIdでアクセス | 404（存在秘匿） | NT/SS |
| SCR-015-005 | ログイン済み（UserA） | 1) 「＋通院履歴追加」押下 | Createへ遷移（petId/returnUrl付き） | NT |
| SCR-015-006 | ログイン済み（UserA） | 1) 行の詳細押下 | Detailsへ遷移（returnUrl付き） | NT |
| SCR-015-007 | ログイン済み（UserA） | 1) 行の削除実行 | 一覧へ戻り削除される（PetIdは対象から復元） | NT/DB |
| SCR-015-008 | ログイン済み（UserA、データ11件以上） | 1) 2ページ目へ移動 | 10件/ページでページングされる | SS |
| SCR-015-009 | ログイン済み（UserA） | 1) `page=abc` でアクセス | `page=1` 扱い | NT |
| SCR-015-010 | ログイン済み（UserA） | 1) `page=0` でアクセス | `page=1` 扱い | NT |
| SCR-015-011 | ログイン済み（UserA） | 1) `page=-1` でアクセス | `page=1` 扱い | NT |
| SCR-015-012 | ログイン済み（UserA） | 1) `page=99999` でアクセス | 正常表示（エラーなし）。該当データが無い場合は空一覧として表示される | SS/NT |

---

## SCR-016 通院履歴詳細
- URL：`/Visits/Details/{visitId}?returnUrl=`（認証必須、オーナーのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-016-001 | ログイン済み（UserA） | 1) 自分の通院履歴詳細を開く | 詳細が表示される | SS |
| SCR-016-002 | ログイン済み（UserB） | 1) UserAのvisitIdで開く | 404（存在秘匿） | NT/SS |
| SCR-016-003 | ログイン済み（UserA、画像あり） | 1) サムネクリック | `GET /images/{imageId}` が呼ばれ表示される | NT/SS |
| SCR-016-004 | ログイン済み（UserA） | 1) 編集押下 | Editへ遷移 | SS |
| SCR-016-005 | ログイン済み（UserA） | 1) 削除実行 | 一覧へ戻り削除される | NT/DB |
| SCR-016-006 | ログイン済み（UserA） | 1) 一覧へ戻る（returnUrl指定あり） | returnUrlへ戻る | NT |

---

## SCR-017 通院履歴 作成／編集
- URL：`/Visits/Create?petId={petId}&returnUrl={returnUrl}`、`/Visits/Edit/{visitId}?returnUrl={returnUrl}`（認証必須、オーナーのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-017-001 | ログイン済み（UserA） | 1) Create表示 | VisitDate必須のフォームが表示される | SS |
| SCR-017-002 | ログイン済み（UserA） | 1) VisitDate未入力で保存 | バリデーションエラーで同画面に戻る | SS/DB |
| SCR-017-003 | ログイン済み（UserA） | 1) ClinicName=101文字で保存 | バリデーションエラー | SS |
| SCR-017-004 | ログイン済み（UserA） | 1) 有効画像を1〜10枚添付して保存 | 保存成功、画像が紐付く | SS/DB |
| SCR-017-005 | ログイン済み（UserA） | 1) 画像を11枚添付して保存 | バリデーションエラー（最大10枚超過） | SS |
| SCR-017-006 | ログイン済み（UserA） | 1) returnUrl指定で保存 | returnUrlへ戻る | NT |
| SCR-017-007 | ログイン済み（UserB、UserAのpetId） | 1) Createを開く | 404（存在秘匿） | NT/SS |
| SCR-017-008 | ログイン済み（UserA） | 1) Edit表示 | 既存値と既存画像サムネが表示される | SS |
| SCR-017-009 | ログイン済み（UserB） | 1) UserAのvisitIdでEditを開く | 404（存在秘匿） | NT/SS |
| SCR-017-010 | ログイン済み（UserA、既存画像あり） | 1) DeleteImageIdsで1枚削除指定→保存 | 指定画像が削除される | SS/DB |
| SCR-017-011 | ログイン済み（UserA、既存9枚） | 1) 追加2枚で保存 | 最大10枚超のためエラー（保存されない） | SS/DB |
| SCR-017-012 | ログイン済み（UserA） | 1) returnUrl外部を指定して保存 | 外部へ遷移せず安全フォールバック | NT |
| SCR-017-013 | ログイン済み（UserA、画像合計が上限に近い） | 1) 有効画像を追加して保存（合計100MB超になるように事前準備） | エラー表示、保存されない（ユーザー合計100MB制限） | SS/DB |
| SCR-017-014 | ログイン済み（UserA） | 1) Diagnosis=501文字で保存 | バリデーションエラー | SS |
| SCR-017-015 | ログイン済み（UserA） | 1) Prescription=501文字で保存 | バリデーションエラー | SS |
| SCR-017-016 | ログイン済み（UserA） | 1) Note=1000文字で保存 | 保存成功（境界値OK） | SS/DB |
| SCR-017-017 | ログイン済み（UserA） | 1) Note=1001文字で保存 | バリデーションエラー | SS |

---

## SCR-018 管理者：ユーザー一覧
- URL：`/Admin/Users?page={page}`（Adminのみ）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-018-001 | 未ログイン | 1) `/Admin/Users` にアクセス | Loginへ302リダイレクト | NT |
| SCR-018-002 | ログイン済み（一般UserA） | 1) `/Admin/Users` にアクセス | 403 → `/Error/403` 表示（Admin以外） | NT/SS |
| SCR-018-003 | ログイン済み（Admin） | 1) `/Admin/Users` にアクセス | ユーザー一覧が表示される（ページングあり） | SS |
| SCR-018-004 | ログイン済み（Admin） | 1) 2ページ目へ移動（データ11件以上） | 10件/ページでページングされる | SS |
| SCR-018-005 | ログイン済み（Admin） | 1) 任意ユーザー削除（POST `/Admin/Users/Delete/{id}`） | 1) 対象ユーザーと関連データが削除 2) 一覧へ戻る | NT/DB |
| SCR-018-006 | ログイン済み（Admin） | 1) 存在しないuserIdを削除 | 404（`/Error/404`） | NT/SS |
| SCR-018-007 | ログイン済み（Admin） | 1) 削除時にストレージ削除失敗を誘発 | DB削除は継続し、ログに失敗識別子出力 | DB/LOG |
| SCR-018-008 | ログイン済み（Admin） | 1) Adminでも他人の健康ログ閲覧が増えないこと確認（UserAの非公開ペット詳細） | Adminでも通常の閲覧権限と同等（非公開は404） | NT/SS |
| SCR-018-009 | ログイン済み（Admin） | 1) `page=-1` で `/Admin/Users` にアクセス | `page=1` 扱いで表示される | NT |
| SCR-018-010 | ログイン済み（Admin） | 1) `page=99999` で `/Admin/Users` にアクセス | 正常表示（エラーなし）。該当データが無い場合は空一覧として表示される | SS/NT |

---

## SCR-019 共通エラーページ
- URL：`/Error/{statusCode}`（匿名可）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SCR-019-001 | なし | 1) `/Error/400` にアクセス | 400用の表示（ステータス/メッセージ/導線）が出る | SS |
| SCR-019-002 | なし | 1) `/Error/403` にアクセス | 403用の表示が出る | SS |
| SCR-019-003 | なし | 1) `/Error/404` にアクセス | 404用の表示が出る | SS |
| SCR-019-004 | なし | 1) `/Error/500` にアクセス | 500用の表示が出る | SS |
| SCR-019-005 | ログイン済み | 1) エラーページのMyPageリンク押下 | `/MyPage` に遷移できる | SS |

---

## 共通：画像配信（`GET /images/{imageId}`）
- 認証必須、キャッシュ `private, no-store`、参照元ベースで認可、条件により404（存在秘匿）

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| IMG-001 | 未ログイン | 1) `/images/{imageId}` にアクセス | Loginへ302リダイレクト（認証必須） | NT |
| IMG-002 | ログイン済み（UserA、Avatar画像あり） | 1) `/images/{avatarId}` にアクセス | 200で画像取得。レスポンスヘッダ：`Cache-Control: private, no-store` / `X-Content-Type-Options: nosniff` / `Content-Disposition: inline` / `Content-Type` が画像に一致 | NT |
| IMG-003 | ログイン済み（UserB、UserAのAvatar画像） | 1) `/images/{UserAのavatarId}` にアクセス | 404（他人のAvatarは不可） | NT |
| IMG-004 | ログイン済み（UserA、Pet写真あり） | 1) `/images/{petPhotoId}` にアクセス | 200で取得できる | NT |
| IMG-005 | ログイン済み（UserB、UserAの公開ペット写真） | 1) `/images/{UserA公開ペットPhotoId}` にアクセス | 200で取得できる（公開ペット写真） | NT |
| IMG-006 | ログイン済み（UserB、UserAの非公開ペット写真） | 1) `/images/{UserA非公開ペットPhotoId}` にアクセス | 404（存在秘匿） | NT |
| IMG-007 | ログイン済み（UserB、UserAの健康ログ画像） | 1) `/images/{healthLogImageId}` にアクセス | 404（健康情報は共有しない） | NT |
| IMG-008 | ログイン済み（UserA、健康ログ画像） | 1) `/images/{healthLogImageId}` にアクセス | 200で取得できる | NT |
| IMG-009 | ログイン済み（UserB、UserAの通院画像） | 1) `/images/{visitImageId}` にアクセス | 404（存在秘匿） | NT |
| IMG-010 | ログイン済み（UserA、通院画像） | 1) `/images/{visitImageId}` にアクセス | 200で取得できる | NT |
| IMG-011 | ログイン済み | 1) 存在しないimageIdでアクセス | 404 | NT |
| IMG-012 | ログイン済み（UserA） | 1) Status=PendingのImageAssetを作りアクセス（運用テスト） | 404（配信不可条件） | NT/DB |

---

## 共通：CSRF（Anti-forgery）
> すべてのPOSTにCSRF対策を適用する方針。フォーム経由では成功し、トークン無し/不正では失敗することを確認する。

| No | 前提 | 手順 | 期待結果 | 証跡 |
|---|---|---|---|---|
| SEC-CSRF-001 | ログイン済み（UserA） | 1) 正規フォームから `POST /Pets/Delete/{id}` | 正常に削除できる | NT |
| SEC-CSRF-002 | ログイン済み（UserA） | 1) トークン無しで `POST /Pets/Delete/{id}` を送る（DevTools/ツール） | 400（/Error/400）で拒否され、削除されない | NT/DB |
| SEC-CSRF-003 | ログイン済み（UserA） | 1) トークン無しで `POST /HealthLogs/Delete/{id}` | 400（/Error/400）で拒否され、削除されない | NT/DB |
| SEC-CSRF-004 | ログイン済み（Admin） | 1) トークン無しで `POST /Admin/Users/Delete/{id}` | 400（/Error/400）で拒否され、削除されない | NT/DB |

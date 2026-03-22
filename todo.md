# ペット健康管理アプリ開発 TODO（実行計画）

> 対象：ASP.NET Core MVC + Identity / EF Core / SQL Server（開発：LocalDB、本番：Azure SQL）

---

## 共通：開発ルール

### 共通DoD（Definition of Done）
- ビルド：`./scripts/build.sh`（Windowsは `./scripts/build.ps1`）が **成功**（ローカル or CI）
- テスト：`./scripts/test.sh`（Windowsは `./scripts/test.ps1`）が **成功**（ローカル or CI）
- 変更点に応じてフォーマット：`./scripts/format.sh`（Windowsは `./scripts/format.ps1`）を **実行し差分なし**（導入時）
- 重要な非機能（存在秘匿404 / returnUrl / 画像制限 / 所有者認可）が **壊れていない**（最低限の回帰確認 or 自動テスト）
- PRは小さく（1PR=1目的、レビュー可能なサイズ）

### 共通ルール
- **PRG（Post/Redirect/Get）**：POST成功後は必ず302でGETへ
- **returnUrl**：Query → hidden → POST で引き回し、`Url.IsLocalUrl()` 等でローカルURLのみ許可
- **存在秘匿(404)**：秘匿対象の所有者不一致・非公開・参照不可は 404
- **所有者認可**：画面入力の `petId` 等を信頼せず、**リソースIDから復元して認可**
- **ページング補正**：`page` 未指定/非数/<=0 は 1
- **命名規約**
  - Query/Route のキーは **lowerCamelCase**（例：`petId`, `healthLogId`, `returnUrl`, `page`）
  - Form は原則 ViewModel のプロパティ名（`asp-for`）に合わせる（多くは PascalCase）
  - 例外：遷移保持など “Query 由来で引き回す値” （`returnUrl`, `page`, `petId` などの hidden/リンク用）は **lowerCamelCase** を正とする
- **Admin(403)**：Adminエリアは非Admin 403（存在秘匿対象ではない）
- **不正入力(400)**：画面を持たないPOSTの不正入力は 400 → `/Error/400`
- **画像URL**：画像配信のURL表記は `/images/{imageId}`（lowercase）を正とする

---

## フェーズ0：プロジェクト準備

> チェックボックスの意味：`[x]` は「方針・成果物が揃っている（または内容が確定）」、`[ ]` は「未実施または未確認」。

### 0.1 ドキュメント整合・運用方針
- [x] 要件・基本設計・API仕様・ER・画面項目/遷移の整合（URL/パラメータ名/ステータスコード/権限制御）
- [x] 仕様の「正」とするルール（矛盾があれば **todo.md に決定事項を追記して正にする**）
- [x] 画像保存方式の方針（`StorageRoot`、`wwwroot`外、配信は `/images/{imageId}`）
- [x] 「存在秘匿(404)」対象の範囲（Pet/HealthLog/ScheduleItem/Visit/Image）
- [x] returnUrl（Open Redirect対策）と「Query → hidden → POST」統一
- [x] 「petIdを信頼しない」（例：完了トグルは scheduleItemId 起点で所有者認可）
- [ ] **命名規約チェックリスト**（Query/Route は lowerCamelCase、Formは例外ルール込み、API仕様との一致）を `CONTRIBUTING.md` に追記

### 0.2 リポジトリ/品質ゲート
- [x] CI（ビルド/テスト）最小セット（GitHub Actions等）
- [ ] **ブランチ保護**：main への直push禁止、CI必須、レビュー必須
- [ ] **CIの段階導入**：最初は「ビルド + 重要シナリオ最小テスト（認証/秘匿/画像）」を必須チェックにする
- [x] tool manifest + `./scripts/format.sh`（Windowsは `./scripts/format.ps1`）を有効化
- [ ] 依存関係更新の運用（例：Dependabot/Renovate。必要なら）

> 参考：ローカルの品質ゲート（build/test/format）は `CONTRIBUTING.md` に明文化済み。

### 0.3 Codex/VS Code 運用（整備済 + 任意）
- [x] `AGENTS.md`（最重要ルール/参照順/人間レビュー必須領域）
- [x] `CONTRIBUTING.md`（build/test/format、PRルール）
- [x] `scripts/`（`build.sh`/`test.sh`/`format.sh` と Windows向け `*.ps1` のワンコマンド化）
- [ ] タスク分割のテンプレ化（Plan→実装→テスト→PR）※必要なら

### 0.4 完了条件/成果物
- [x] 重要ルール（404秘匿/returnUrl/画像制限/所有者認可）が1ページにまとまっている（AGENTS/CONTRIBUTING）
- [x] PRの品質ゲート（build/test/format方針）が明記されている（CONTRIBUTING）
- [x] `./scripts/build.sh` と `./scripts/test.sh`（Windowsは `*.ps1`）が用意されている
- [x] CIで `dotnet build` / `dotnet test` が回り、PRで落ちる

---

## フェーズ1：環境構築・基盤

### 1.1 ソリューション/プロジェクト作成
- [x] ASP.NET Core MVC プロジェクト作成（.NET 10）
- [x] Identity 組み込み（個別アカウント / Cookie認証）
- [ ] 開発用 HTTPS 証明書準備（dev-certs）

### 1.2 設定・シークレット
- [x] `dotnet user-secrets` 初期化
- [x] ConnectionString（LocalDB）設定
- [x] `StorageRoot`（画像保存先）設定
- [ ] 環境別設定（Development/Staging/Production）方針を決め、appsettings を整備

### 1.3 ミドルウェア・共通UI
- [x] ルーティング/エリア（Admin）設定
- [x] `UseStatusCodePagesWithReExecute("/Error/{0}")` 等でエラーページ統一
- [ ] 共通レイアウト（ヘッダ：未ログイン/ログイン/Admin表示切替）
- [x] CSRF（Anti-forgery）をフォームPOSTへ適用

### 1.4 横断共通部品
- [x] `ReturnUrlHelper`（validate + Query→hidden→POST のテンプレ）を作成
- [x] `PagingHelper`（page補正）を作成
- [ ] `OwnershipAuthorizer`（リソースID → PetId/UserId復元 → 所有者チェック → 404秘匿）を作成
- [x] `ErrorController`（`/Error/{statusCode}`：400/403/404/500 の共通表示）を用意
- [ ] **命名規約テスト**（Query/Route と、`returnUrl`/`page`/`petId` 等の hidden/リンク用キーが lowerCamelCase になっている）をスモーク的に追加（必要なら）

### 1.5 完了条件/成果物
- [ ] ローカル起動→ログイン→基本画面遷移が一周できる（エラーページ含む）
- [ ] 環境別設定の読み分け（Development/Staging/Production）が確認できる
- [ ] 共通部品（returnUrl/page/認可/秘匿）が最初の1画面で利用されている

---

## フェーズ2：DB/ドメイン（EF Core + マイグレーション）

### 2.1 Entity/DbContext
- [ ] `ApplicationDbContext`（Identity + アプリテーブル）整備
- [x] エンティティ実装（Pet/HealthLog/ScheduleItem/Visit/ImageAsset/中間テーブル）
- [ ] RowVersion（同時更新）など必要な列の付与
- [ ] インデックス・制約（ユニーク、FK、並び順など）

### 2.2 マイグレーション/初期データ
- [ ] 初回 Migration 作成・適用
- [ ] 開発用 Seed（Species、Adminユーザー/ロール付与手順）
- [ ] 本番向け Migration 運用手順（適用順、ロールバック方針）

### 2.3 完了条件/成果物
- [ ] 新規環境で「マイグレーション適用→Seed→ログイン→初期表示」まで手順化されている

---

## フェーズ3：画像基盤（アップロード・検証・保存・配信）

### 3.1 画像ストレージ
- [x] `IImageStorageService`（保存/取得/削除）実装（ファイルシステム）
- [x] `StorageRoot/tmp`（一時保存）運用
- [x] デフォルト画像を `wwwroot/images/default/` に配置

### 3.2 画像検証・加工
- [x] 許可拡張子/Content-Type（jpeg/png/webp）検証
- [x] 実データをデコードして画像判定（偽装ファイル拒否）
- [x] EXIF除去・向き正規化（再エンコード）
- [x] 解像度制限（最大辺/総画素数）
- [x] サイズ上限（1ファイル2MB）

### 3.2a 濫用対策
- [ ] **リクエストサイズ上限**：Kestrel/`FormOptions.MultipartBodyLengthLimit` 等で上限を設定
- [ ] **同時アップロード/連投対策**：ASP.NET Core Rate Limiting（IP/ユーザー）を導入（まずは緩めでOK）
- [ ] 画像アップロード失敗時のエラーメッセージ（ユーザー向け/ログ向け）を整理

### 3.3 容量制限
- [x] ユーザー合計100MB管理（UsedImageBytes 更新）
- [ ] HealthLog/Visit 添付は最大10枚（既存+追加合算）

### 3.4 画像配信 `GET /images/{imageId}`
- [x] `ImagesController.Get(imageId)` 実装
- [x] 参照元（Avatar/PetPhoto/HealthLog/Visit）を辿って認可
- [x] 非許可/存在不明/参照辿れず/Status=Pending 等は 404（存在秘匿）
- [x] レスポンスヘッダ（Cache-Control/private、nosniff等）

### 3.5 完了条件/成果物
- [ ] 画像アップロード→保存→認可付き配信→削除 が一連で動作（異常系：偽装/超過/上限も含む）
- [ ] 画像関連のログ方針（削除失敗/拒否理由/例外）が決まっている

---

## フェーズ4：認証・アカウント（MyPage/プロフィール/削除）

### 4.1 MyPage
- [x] `GET /MyPage`：ユーザー情報＋自分のペット一覧表示
- [x] ペット0件時の案内表示

### 4.2 プロフィール編集
- [x] `GET/POST /Account/EditProfile`（DisplayName、AvatarFile）
- [x] `returnUrl` の取り回し（Query→hidden→POST、ローカルURLのみ許可）

### 4.3 アカウント削除（ユーザー自身）
- [x] `GET /Account/Delete`（確認画面）
- [x] `POST /Account/DeleteConfirmed`：関連データ物理削除
- [x] 画像ファイル削除失敗時：DB削除は継続し、失敗識別子をログ出力

### 4.4 完了条件/成果物
- [ ] 未ログイン→保護URL→Login→returnUrl復帰が確認できる
- [x] アカウント削除で「関連データが消える/画像削除失敗でも処理継続」が確認できる

---

## フェーズ5：ペット機能（公開プロフィール共有）

### 5.1 ペット一覧（公開＋自分）
- [x] `GET /Pets`：検索（名前部分一致、Speciesフィルタ）
- [x] ページング（10件/ページ、`page`補正：未指定/非数/<=0 は1）
- [x] 表示対象：自分のペット（公開/非公開問わず）＋他人の公開ペット

### 5.2 ペット詳細
- [x] `GET /Pets/Details/{petId}`：公開 or オーナーのみ
- [x] 非公開ペットをオーナー以外が参照：404（存在秘匿）
- [x] オーナーのみ：編集/削除/健康ログ/予定/通院/画像への導線表示

### 5.3 ペット作成/編集/削除
- [x] `GET/POST /Pets/Create`（IsPublic 初期 true）
- [x] `GET/POST /Pets/Edit/{petId}`（オーナーのみ、他人は404）
- [x] 画像置換/削除（RemovePhoto 等）
- [ ] `POST /Pets/Delete/{petId}`（関連データ削除、画像も削除）
- [x] すべての POST：PRG（302）+ returnUrl 優先（ローカルのみ）

### 5.4 完了条件/成果物
- [x] 公開/非公開の表示制御（自分と他人）と、秘匿(404)が確認できる

---

## フェーズ6：健康ログ（CRUD + 画像複数）

### 6.1 一覧/詳細
- [x] `GET /HealthLogs?petId=&page=`：RecordedAt降順（同時刻はId降順）
- [x] `GET /HealthLogs/Details/{healthLogId}`：表示専用（画像サムネ→拡大）
- [x] 非オーナーは 404（存在秘匿）

### 6.2 作成/編集
- [x] `GET/POST /HealthLogs/Create?petId=`：RecordedAt（datetime-local）必須、JST(+09:00)として保存
- [x] `GET/POST /HealthLogs/Edit/{healthLogId}`：既存値表示、更新
- [x] 画像追加（複数）・削除（DeleteImageIds）
- [x] 最大10枚制限（既存+追加）とユーザー合計100MB制限

### 6.3 削除
- [x] `POST /HealthLogs/Delete/{healthLogId}`：画像含め削除
- [x] 「画面を持たない」不正は 400（共通エラーページ）

### 6.4 完了条件/成果物
- [ ] CRUD + 画像複数 + 制限（10枚/100MB/2MB/解像度）が確認できる

---

## フェーズ7：予定（CRUD + 完了トグル）

### 7.1 一覧/詳細
- [x] `GET /ScheduleItems?petId=&page=`：DueDate昇順
- [x] `GET /ScheduleItems/Details/{scheduleItemId}`：表示専用
- [ ] 非オーナーは 404（存在秘匿）

### 7.2 作成/編集/削除
- [x] `GET/POST /ScheduleItems/Create?petId=`
- [x] `GET/POST /ScheduleItems/Edit/{scheduleItemId}`
- [x] `POST /ScheduleItems/Delete/{scheduleItemId}`
- [ ] Type（Vaccine/Medicine/Visit/Other）の表示/入力（固定値）

### 7.3 完了トグル
- [x] `POST /ScheduleItems/SetDone/{scheduleItemId}`（isDone必須）
- [ ] **petIdを信頼しない**：scheduleItemId から PetId を復元して所有者チェック
- [ ] 302遷移：returnUrl 優先、無効なら一覧へ（page維持）

### 7.4 完了条件/成果物
- [ ] 完了トグルがパラメータ改ざん（petId）に対して耐性を持ち、サーバ側で scheduleItemId 起点の所有者認可を実施したうえで、PRG（Post/Redirect/Get）および returnUrl による遷移保持が担保されていることを確認できる

---

## フェーズ8：通院履歴（CRUD + 画像複数）

### 8.1 一覧/詳細
- [x] `GET /Visits?petId=&page=`：VisitDate降順
- [x] `GET /Visits/Details/{visitId}`：表示専用（画像サムネ→拡大）
- [ ] 非オーナーは 404（存在秘匿）

### 8.2 作成/編集/削除
- [ ] `GET/POST /Visits/Create?petId=`
- [ ] `GET/POST /Visits/Edit/{visitId}`
- [ ] `POST /Visits/Delete/{visitId}`
- [ ] 画像追加/削除（最大10枚、ユーザー合計100MB）

### 8.3 完了条件/成果物
- [ ] CRUD + 画像複数 + 制限が確認できる

---

## フェーズ9：管理者機能（Admin Area）

### 9.1 ユーザー一覧
- [x] `GET /Admin/Users`（Adminのみ、非Adminは403）
- [ ] 一覧表示（検索/ページング必要なら追加）

### 9.2 ユーザー削除
- [ ] `POST /Admin/Users/Delete/{userId}`（Adminのみ）
- [ ] ユーザー関連データの一括削除（画像含む）
- [ ] Adminでも閲覧権限は増やさない（削除のみ）

### 9.3 完了条件/成果物
- [ ] 非Adminは403、Adminは削除のみ可能であることが確認できる

---

## フェーズ10：横断（セキュリティ/バリデーション/UX）

### 10.1 returnUrl / Open Redirect対策
- [x] `Url.IsLocalUrl(returnUrl)`（または同等）で検証
- [x] Query→hidden→POST の統一ルール化（画面項目定義に沿う）

### 10.2 ステータスコード・存在秘匿
- [ ] 所有者不一致は原則 404（秘匿対象：Pet/HealthLog/ScheduleItem/Visit/Image）
- [x] Adminルート非許可は 403
- [x] 400/403/404/500 を `/Error/{statusCode}` に統一表示

### 10.3 入力バリデーション
- [ ] 文字数（例：Name 50、Note 1000等）
- [ ] 数値範囲（体重/食事量/散歩時間など）
- [ ] date/date-time 形式（dateは yyyy-MM-dd、RecordedAtは datetime-local）

### 10.4 パフォーマンス/運用
- [ ] N+1回避（一覧で必要な関連だけInclude）
- [x] 画像配信のキャッシュ方針（private/no-store or ETag）
- [ ] ログ（削除失敗、例外、監査的ログが必要なら方針化）

### 10.5 Cookie/ヘッダ（最低限）
- [ ] 認証Cookie/Anti-forgery の属性方針（Secure/HttpOnly/SameSite）を決める
- [ ] セキュリティヘッダ方針（例：HSTS、CSP（最小）、Referrer-Policy、Permissions-Policy など）を決める

### 10.6 完了条件/成果物
- [ ] 「秘匿(404)/403/400」の一貫性と、Open Redirect対策が確認できる
- [ ] Cookie/ヘッダの方針がREADMEか運用メモに残っている

---

## フェーズ11：テスト（単体/結合/E2E）

### 11.1 テスト基盤
- [x] テストプロジェクト作成（xUnit等）
- [ ] DBテスト戦略（LocalDB/テストDB、トランザクション、データリセット）
- [ ] ストレージ（画像）テスト戦略（テスト用StorageRoot、クリーンアップ）

### 11.2 重要シナリオ
- [ ] 認証：未ログイン→保護URL→Login→returnUrl復帰
- [ ] 存在秘匿：他人の非公開Pet/健康情報/画像が404
- [ ] 画像：拡張子偽装、2MB超、解像度超、合計100MB超、最大10枚超
- [x] PRG：POST成功→302→一覧/詳細、returnUrl優先
- [ ] Admin：非Admin 403、Admin削除の一括削除

### 11.3 既存のテスト設計書
- [x] 画面単位テストケース表（`test-cases-by-screen.md`）

### 11.4 テストケース反映
- [ ] 画面単位テストケース表の項目を、結合テスト/E2Eに落とし込み

### 11.5 完了条件/成果物
- [ ] CIで主要シナリオが自動で回り、回帰を検知できる（最低限：認証/秘匿/画像制限）

---

## フェーズ12：デプロイ（Azure想定）

### 12.1 Azure リソース
- [ ] App Service（Linux/Windowsどちらでいくか決定）
- [ ] Azure SQL Database
- [ ] 機密情報：App Service構成 or Key Vault
- [ ] 画像ストレージ：当面はApp Serviceの永続性/容量/スケール課題を検討（必要ならBlobへ移行計画）
- [ ] **DataProtection キー永続化**（複数インスタンス/再デプロイでもCookie復号できるように Blob/Files/KeyVault など）

### 12.2 リリース手順
- [ ] CI/CD（ビルド→テスト→デプロイ）
- [ ] Migration適用手順（デプロイ時の実行方式を決める）
- [ ] ログ/監視（Application Insights等）
- [ ] **スモークテスト**（ログイン/一覧表示/画像GET）を「リリース後の必須チェック」にする

### 12.3 ロールバック/復旧（Runbook）
- [ ] アプリの戻し方（例：デプロイスロット/直前ビルドへ戻す）を手順化
- [ ] DB変更の扱い（基本は前進マイグレーション or バックアップ復元等）を方針化
- [ ] 失敗検知の基準（エラー率/例外/応答時間など）と「戻す判断」を決める

### 12.4 画像運用の紐づけ（Runbook）
- [ ] 画像バックアップ/ライフサイクルの手順を「デプロイ後運用」に紐づけて文書化
- [ ] 画像削除失敗の再試行（リトライキュー/定期バッチ等）を運用タスクとして位置づけ

### 12.5 完了条件/成果物
- [ ] 「デプロイ→マイグレーション→スモーク→監視→ロールバック」まで1本の手順書になっている

---

## 追加タスク（必要に応じて）
- [ ] 監査ログ（Admin削除など）
- [ ] UI改善（入力補助、削除確認モーダル、一覧の検索条件保持）
- [ ] 将来のストレージ差し替え（ファイル→Blob）

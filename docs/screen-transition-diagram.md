# うちの子健康カルテ 画面遷移図

```mermaid
flowchart LR
  Home["Home<br/>(/)<br/>匿名可<br/>※ログイン済みは /MyPage へ302"] --> Login["Login / Register<br/>(Identity UI)"]
  Home -->|ログイン済み| MyPage

  Login --> MyPage["MyPage（ホーム相当）<br/>(/MyPage)"]

  BottomNav["ボトムナビ（ログイン・モバイル）<br/>ホーム(/MyPage) | ペット(/Pets) | 記録(/HealthLogs/Record) | 設定(/Account/EditProfile)"]:::note
  BottomNav -.-> MyPage

  MyPage --> EditProfile["プロフィール編集<br/>(/Account/EditProfile)"]
  MyPage --> ChangePassword["パスワード変更<br/>(/Identity/Account/Manage/ChangePassword)"]
  EditProfile --> AccountDelete["アカウント削除（確認）<br/>(/Account/Delete)"]
  AccountDelete --> AccountDeleteConfirmed["アカウント削除（実行）<br/>(POST /Account/DeleteConfirmed)"]

  MyPage --> PetsIndex["ペット一覧<br/>(/Pets)"]
  MyPage --> PetCreate["ペット登録<br/>(/Pets/Create)"]
  MyPage --> PetDetails["ペット詳細<br/>(/Pets/Details/{petId})"]

  HealthLogRecord["記録するペットを選ぶ<br/>(/HealthLogs/Record)<br/>0匹→/Pets/Create、1匹→作成へ直行、2匹以上→選択表示"]
  BottomNav -.-> HealthLogRecord
  HealthLogRecord --> HealthLogCreate

  PetsIndex --> PetDetails
  PetsIndex --> PetCreate
  PetCreate --> PetsIndex

  PetDetails --> PetEdit["ペット編集<br/>(/Pets/Edit/{petId})"]
  PetEdit --> PetDetails

  PetDetails --> HealthLogsIndex["健康ログ一覧<br/>(/HealthLogs?petId={petId})"]
  HealthLogsIndex --> HealthLogCreate["健康ログ作成<br/>(/HealthLogs/Create?petId={petId})"]
  HealthLogsIndex --> HealthLogEdit["健康ログ編集<br/>(/HealthLogs/Edit/{healthLogId})"]
  HealthLogsIndex --> HealthLogDetails["健康ログ詳細<br/>(/HealthLogs/Details/{healthLogId})"]
  HealthLogsIndex --> HealthLogDelete["健康ログ削除<br/>(POST /HealthLogs/Delete/{healthLogId})"]
  HealthLogDetails --> HealthLogsIndex
  HealthLogDetails --> HealthLogEdit
  HealthLogDetails --> HealthLogDelete
  HealthLogCreate --> HealthLogsIndex
  HealthLogEdit --> HealthLogsIndex

  PetDetails --> ScheduleIndex["予定一覧<br/>(/ScheduleItems?petId={petId})"]
  ScheduleIndex --> ScheduleCreate["予定作成<br/>(/ScheduleItems/Create?petId={petId})"]
  ScheduleIndex --> ScheduleEdit["予定編集<br/>(/ScheduleItems/Edit/{scheduleItemId})"]
  ScheduleIndex --> ScheduleDetails["予定詳細<br/>(/ScheduleItems/Details/{scheduleItemId})"]
  ScheduleIndex --> ScheduleDelete["予定削除<br/>(POST /ScheduleItems/Delete/{scheduleItemId})"]
  ScheduleIndex --> ScheduleSetDone["予定完了切替<br/>(POST /ScheduleItems/SetDone/{scheduleItemId})"]
  ScheduleDetails --> ScheduleIndex
  ScheduleDetails --> ScheduleEdit
  ScheduleDetails --> ScheduleDelete
  ScheduleDetails --> ScheduleSetDone
  ScheduleCreate --> ScheduleIndex
  ScheduleEdit --> ScheduleIndex
  ScheduleSetDone --> ScheduleIndex

  PetDetails --> VisitsIndex["通院履歴一覧<br/>(/Visits?petId={petId})"]
  VisitsIndex --> VisitCreate["通院履歴作成<br/>(/Visits/Create?petId={petId})"]
  VisitsIndex --> VisitEdit["通院履歴編集<br/>(/Visits/Edit/{visitId})"]
  VisitsIndex --> VisitDetails["通院履歴詳細<br/>(/Visits/Details/{visitId})"]
  VisitsIndex --> VisitDelete["通院履歴削除<br/>(POST /Visits/Delete/{visitId})"]
  VisitDetails --> VisitsIndex
  VisitDetails --> VisitEdit
  VisitDetails --> VisitDelete
  VisitCreate --> VisitsIndex
  VisitEdit --> VisitsIndex

  MyPage --> AdminUsers["管理者：ユーザー一覧<br/>(/Admin/Users)"]
  AdminUsers --> AdminUserDelete["管理者：ユーザー削除<br/>(POST /Admin/Users/Delete/{userId})"]

  ErrorPage["共通エラーページ<br/>(/Error/{statusCode})"]

  NoteAuth["※ 保護画面は未ログイン時ログインへリダイレクト"]:::note
  NoteOwner["※ HealthLogs / ScheduleItems / Visits は所有者のみ（非所有者は404）"]:::note
  NotePublic["※ PetDetails は自分 or IsPublic=true のみ（非公開は404）"]:::note
  NoteAdmin["※ /Admin は Admin のみ"]:::note
  NoteError["※ 400/403/404 等は /Error/{statusCode} を表示"]:::note
  NotePhase1["※ 第1段階UIでは健康分析/月間カレンダー等の新規画面は追加しない"]:::note

  NoteAuth -.-> MyPage
  NoteOwner -.-> HealthLogsIndex
  NotePublic -.-> PetDetails
  NoteAdmin -.-> AdminUsers
  NoteError -.-> ErrorPage
  NotePhase1 -.-> MyPage

  classDef note fill:#fff,stroke:#999,stroke-dasharray: 4 4,color:#333;
```

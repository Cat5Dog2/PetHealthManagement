### 1) 共通レイアウト（ヘッダ＋ナビ）

```mermaid
flowchart TB
  Header["Header<br/>Logo | MyPage | Pets | (Admin) | Logout"] --> Body["Body<br/>(画面ごとのコンテンツ)"]
```

---

### 2) MyPage（/MyPage）

```mermaid
flowchart TB
  Header["Header<br/>Logo | MyPage | Pets | (Admin) | Logout"]
  Profile["プロフィールカード<br/>- Avatar<br/>- 表示名<br/>- Email<br/>[プロフィール編集]<br/>[パスワード変更]<br/>[アカウント削除]"]
  Pets["自分のペット一覧<br/>[＋ペット登録]<br/>ペットカード×N<br/>- サムネイル/名前<br/>- 公開/非公開バッジ<br/>[詳細]"]
  Empty["（0件の場合）<br/>ペットを登録してください"]:::note

  Header --> Profile --> Pets
  Pets -.-> Empty

  classDef note fill:#fff,stroke:#999,stroke-dasharray: 4 4,color:#333;
```

（MyPageに表示する項目＋リンク要件）

---

### 3) プロフィール編集（/Account/EditProfile）

```mermaid
flowchart TB
  Header["Header"]
  Form["プロフィール編集フォーム<br/>表示名(テキスト)<br/>プロフィール画像(ファイル)<br/>[保存] [キャンセル(MyPageへ)]"]
  Header --> Form
```

（表示名＋プロフィール画像の編集）

---

### 4) ペット一覧（/Pets）

```mermaid
flowchart TB
  Header["Header"]
  Filters["検索/絞り込み<br/>名前キーワード(部分一致)<br/>種別(10択/ALL)<br/>[検索]"]
  List["一覧(10件/ページ)<br/>行：サムネ | ペット名 | 種別 | (品種) | オーナー | (公開/自分マーク)<br/>[詳細]"]
  Pager["ページャ<br/>Prev | 1 | 2 | ... | Next"]

  Header --> Filters --> List --> Pager
```

（一覧表示項目・ページング・検索条件）

---

### 5) ペット詳細（/Pets/Details/{id}）

```mermaid
flowchart TB
  Header["Header"]
  Summary["ペット基本情報<br/>大きめ写真<br/>名前 / 種別 / 品種 / 性別 / 誕生日 / 迎えた日<br/>オーナー表示名<br/>公開/非公開"]
  OwnerActions["（オーナーのみ表示）<br/>[編集] [削除]<br/>[健康ログ] [予定] [通院履歴]"]:::note
  NonOwnerNote["（非オーナー）<br/>編集/削除/健康情報導線は非表示"]:::note

  Header --> Summary --> OwnerActions
  Summary --> NonOwnerNote

  classDef note fill:#fff,stroke:#999,stroke-dasharray: 4 4,color:#333;
```

（詳細表示項目、オーナー導線、非公開ペットは404秘匿）

---

### 6) 健康ログ：一覧（/HealthLogs?petId=）

```mermaid
flowchart TB
  Header["Header"]
  Title["{PetName} の健康ログ"]
  Actions["[＋健康ログ追加]"]
  List["一覧(10件/ページ, RecordedAt降順)<br/>行：RecordedAt | 体重 | 食事量 | 活動 | 排せつ | メモ(抜粋) | 画像あり | [編集] [削除]"]
  Pager["ページャ"]

  Header --> Title --> Actions --> List --> Pager
```

（健康ログ一覧の表示項目・ソート・画像あり表示）

---

### 7) 健康ログ：作成/編集（/HealthLogs/Create, /HealthLogs/Edit）

```mermaid
flowchart TB
  Header["Header"]
  Form["健康ログ入力<br/>RecordedAt(必須 DateTimeOffset)<br/>体重 / 食事量 / 活動 / 排せつ / メモ<br/>画像アップロード(複数, 最大10枚)<br/>（編集時）既存画像サムネ一覧 + 個別削除<br/>[保存] [キャンセル(一覧へ)]"]
  Header --> Form
```

（登録・編集・画像最大10枚、既存画像の個別削除）

---

### 8) 予定：一覧（/ScheduleItems?petId=）

```mermaid
flowchart TB
  Header["Header"]
  Title["{PetName} の予定"]
  Actions["[＋予定追加]"]
  List["一覧(10件/ページ, DueDate昇順)<br/>行：期日 | 種別 | タイトル | メモ(抜粋) | 完了トグル(IsDone) | [編集] [削除]"]
  Pager["ページャ"]

  Header --> Title --> Actions --> List --> Pager
```

（予定一覧・ソート・完了フラグ切替）

---

### 9) 通院履歴：一覧（/Visits?petId=）

```mermaid
flowchart TB
  Header["Header"]
  Title["{PetName} の通院履歴"]
  Actions["[＋通院履歴追加]"]
  List["一覧(10件/ページ, VisitDate降順)<br/>行：通院日 | 病院名 | 診断(抜粋) | 処方(抜粋) | メモ(抜粋) | 画像あり | [編集] [削除]"]
  Pager["ページャ"]

  Header --> Title --> Actions --> List --> Pager
```

（通院履歴一覧・ソート・画像あり表示）

---

### 10) 管理者：ユーザー一覧（/Admin/Users）

```mermaid
flowchart TB
  Header["Header<br/>Logo | MyPage | Pets | Admin | Logout"]
  List["ユーザー一覧<br/>行：表示名 | Email | (作成日) | [削除]"]
  Danger["注意：削除は関連データ(ペット/健康ログ/予定/通院/画像)を含む物理削除"]:::note

  Header --> List --> Danger

  classDef note fill:#fff,stroke:#999,stroke-dasharray: 4 4,color:#333;
```

（Admin機能のURL・削除、Adminでも閲覧特権なし）

---
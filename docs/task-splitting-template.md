# タスク分割テンプレート

小さな 1 目的 PR に分けて進めるための、コピー用テンプレートです。  
必要に応じて削って使ってください。

## 1. Plan

```md
## Plan
- 目的:
- 仕様の根拠:
- 触るファイル:
- リスク:
- 完了条件:
```

記入例:

```md
## Plan
- 目的: HealthLog 編集画面の returnUrl 引き回しを統一する
- 仕様の根拠: todo.md, docs/api-spec.md, AGENTS.md
- 触るファイル: Controllers/HealthLogsController.cs, Views/HealthLogs/Edit.cshtml, tests/...
- リスク: returnUrl の open redirect、PRG 崩れ
- 完了条件: GET/POST とも returnUrl を Query -> hidden -> POST で維持し、非ローカル URL を無視できる
```

## 2. 実装分割

```md
## 実装メモ
- 変更 1:
- 変更 2:
- 変更 3:
```

分け方の目安:

- 1 タスク = 1 目的
- 認可、存在秘匿、returnUrl、画像処理、物理削除は混ぜすぎない
- UI 調整とドメイン挙動変更は、可能なら別タスクに分ける

## 3. テスト計画

```md
## テスト
- 追加/更新するテスト:
- 手元確認:
- 回帰観点:
```

記入例:

```md
## テスト
- 追加/更新するテスト: Controller テスト 2 件、integration テスト 1 件
- 手元確認: scripts/test.ps1
- 回帰観点: 未ログイン 302、所有者不一致 404、returnUrl のローカル URL 検証
```

## 4. PR 準備

```md
## PR メモ
- 目的:
- 主な変更:
- テスト結果:
- レビューで見てほしい点:
```

## 5. 完了チェック

```md
- [ ] Plan を 5 行以内で書いた
- [ ] 1PR=1目的になっている
- [ ] 仕様の根拠ファイルを確認した
- [ ] 必要なテストを追加または更新した
- [ ] scripts/test を実行した
- [ ] todo.md を同期した
- [ ] レビュー観点を PR に書ける状態にした
```

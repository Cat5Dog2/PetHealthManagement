# PetHealthManagement

ペット健康管理アプリ（ASP.NET Core MVC + Identity / EF Core / SQL Server）です。

## Prerequisites

- .NET SDK 10.0.103 以上（`global.json` 参照）
- SQL Server LocalDB（Windows 開発時）

## Quick Start

```bash
# build
./scripts/build.sh

# test
./scripts/test.sh

# format check
./scripts/format.sh
```

Windows PowerShell では実行ポリシーにより `./scripts/*.ps1` が失敗する場合があります。
その場合は `-ExecutionPolicy Bypass` で実行してください。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/format.ps1
```

## Run App

```bash
dotnet run --project src/PetHealthManagement.Web
```

## Documents

- 開発ルール: `AGENTS.md`
- PR/品質ゲート: `CONTRIBUTING.md`
- 仕様ドキュメント: `docs/`
- 実行計画: `todo.md`

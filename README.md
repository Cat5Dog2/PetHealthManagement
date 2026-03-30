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

## Security Defaults

- Auth cookie: `__Host-PetHealthManagement.Auth`, `Secure`, `HttpOnly`, `SameSite=Lax`
- Anti-forgery cookie: `__Host-PetHealthManagement.AntiForgery`, `Secure`, `HttpOnly`, `SameSite=Strict`
- HSTS: enabled outside Development with a 180-day max age
- Security headers: `Content-Security-Policy`, `Referrer-Policy`, `Permissions-Policy`, `X-Content-Type-Options`, `X-Frame-Options`
- Current CSP is intentionally minimal and still allows `'unsafe-inline'` for scripts and styles because some Razor views still use inline handlers and the import map stub

## Logging Defaults

- Image upload rejection, persistence failure, and file delete failure are logged with structured fields such as `imageCategory`, `ownerId`, `resourceType`, `resourceId`, `reason`, and `storageKey`
- High-risk deletion flows log start/completion/failure with structured fields such as `operation`, `ownerId`, `targetType`, `targetId`, and affected record counts
- Audited delete actions log `actorUserId` and target information for self-service account deletion and Admin user deletion
- Unhandled request exceptions are logged with `method`, `path`, `traceId`, and `userId`
- Persistent audit log retention and external monitoring are still future operational tasks

- 開発ルール: `AGENTS.md`
- PR/品質ゲート: `CONTRIBUTING.md`
- 仕様ドキュメント: `docs/`
- 実行計画: `todo.md`

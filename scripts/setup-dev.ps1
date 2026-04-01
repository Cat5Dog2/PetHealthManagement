<#
.SYNOPSIS
  Applies local migrations and seeds a Development admin account.

.DESCRIPTION
  - Stores development admin settings in user-secrets for the web project.
  - Runs the web project with `--setup-development` under the Development environment.

.PARAMETER AdminEmail
  Development admin email address. Defaults to "admin@example.com".

.PARAMETER AdminPassword
  Development admin password. Required on first run. Defaults to $env:DEV_ADMIN_PASSWORD when omitted.

.PARAMETER AdminDisplayName
  Development admin display name. Defaults to "Development Admin".
#>

param(
  [string]$AdminEmail = 'admin@example.com',
  [string]$AdminPassword = $env:DEV_ADMIN_PASSWORD,
  [string]$AdminDisplayName = 'Development Admin'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Info([string]$Message) {
  Write-Host "[$(Split-Path -Leaf $PSCommandPath)] $Message"
}

function Invoke-Dotnet([string[]]$DotnetCliArgs) {
  & dotnet @DotnetCliArgs
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet $($DotnetCliArgs -join ' ') failed with exit code $LASTEXITCODE."
  }
}

function Get-RepoRoot([string]$StartDir) {
  $dir = Get-Item -LiteralPath $StartDir
  for ($i = 0; $i -lt 9; $i++) {
    $gitDir = Join-Path $dir.FullName '.git'
    $toolManifest = Join-Path $dir.FullName 'dotnet-tools.json'
    $legacyToolManifest = Join-Path $dir.FullName '.config/dotnet-tools.json'
    $globalJson = Join-Path $dir.FullName 'global.json'
    $hasSln = @(Get-ChildItem -Path $dir.FullName -Filter *.sln -File -ErrorAction SilentlyContinue).Count -gt 0

    if ((Test-Path $gitDir -PathType Container) -or
        (Test-Path $toolManifest -PathType Leaf) -or
        (Test-Path $legacyToolManifest -PathType Leaf) -or
        (Test-Path $globalJson -PathType Leaf) -or
        $hasSln) {
      return $dir.FullName
    }

    if (-not $dir.Parent) { break }
    $dir = $dir.Parent
  }

  $leaf = Split-Path -Leaf $StartDir
  if ($leaf -ieq 'scripts' -or $leaf -ieq 'script') {
    return (Split-Path -Parent $StartDir)
  }

  return $StartDir
}

if ([string]::IsNullOrWhiteSpace($AdminPassword)) {
  throw 'AdminPassword is required. Pass -AdminPassword or set DEV_ADMIN_PASSWORD.'
}

$ScriptDir = Split-Path -Parent $PSCommandPath
$RepoRoot = Get-RepoRoot $ScriptDir
Set-Location -LiteralPath $RepoRoot

Write-Info "RepoRoot: $RepoRoot"

Invoke-Dotnet @('user-secrets', 'set', '--project', 'src/PetHealthManagement.Web', 'DevelopmentSetup:AdminEmail', $AdminEmail)
Invoke-Dotnet @('user-secrets', 'set', '--project', 'src/PetHealthManagement.Web', 'DevelopmentSetup:AdminPassword', $AdminPassword)
Invoke-Dotnet @('user-secrets', 'set', '--project', 'src/PetHealthManagement.Web', 'DevelopmentSetup:AdminDisplayName', $AdminDisplayName)

$env:ASPNETCORE_ENVIRONMENT = 'Development'
Invoke-Dotnet @('run', '--project', 'src/PetHealthManagement.Web', '--no-launch-profile', '--', '--setup-development')

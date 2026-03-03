<#
.SYNOPSIS
  Runs dotnet format.

.DESCRIPTION
  Default behavior is "check" mode: it verifies there are no formatting changes needed.
  Use -Apply to actually apply formatting.

  If a local tool manifest exists at ./dotnet-tools.json (or legacy ./.config/dotnet-tools.json),
  this script will run:
    dotnet tool restore
  to install the pinned dotnet-format version.

.USAGE
  # Check (CI-friendly)
  ./scripts/format.ps1

  # Apply formatting
  ./scripts/format.ps1 -Apply

  # Skip tool restore
  ./scripts/format.ps1 -NoRestore

  # Specify target (solution or project)
  ./scripts/format.ps1 -Target ./MyApp.sln

  # Pass extra args to dotnet format
  ./scripts/format.ps1 -- --verbosity detailed
#>

param(
  [switch]$Apply,
  [switch]$NoRestore,
  [string]$Target,

  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$DotnetFormatArgs
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Info([string]$Message) {
  Write-Host "[$(Split-Path -Leaf $PSCommandPath)] $Message"
}

function Write-Warn([string]$Message) {
  Write-Host "[$(Split-Path -Leaf $PSCommandPath)][WARN] $Message" -ForegroundColor Yellow
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

  # Fallback: if the script lives in repoRoot\script(s), use its parent; otherwise keep StartDir
  $leaf = Split-Path -Leaf $StartDir
  if ($leaf -ieq 'scripts' -or $leaf -ieq 'script') {
    return (Split-Path -Parent $StartDir)
  }
  return $StartDir
}

$ScriptDir = Split-Path -Parent $PSCommandPath
$RepoRoot = Get-RepoRoot $ScriptDir
Set-Location -LiteralPath $RepoRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw 'dotnet SDK is not installed or not on PATH.'
}

Write-Info "RepoRoot: $RepoRoot"

# If the caller used `--` as a separator, drop it before passing through to dotnet.
$DotnetFormatArgs = @($DotnetFormatArgs)
if ($DotnetFormatArgs.Count -gt 0 -and $DotnetFormatArgs[0] -eq '--') {
  if ($DotnetFormatArgs.Count -eq 1) {
    $DotnetFormatArgs = @()
  }
  else {
    $DotnetFormatArgs = $DotnetFormatArgs[1..($DotnetFormatArgs.Count - 1)]
  }
}

# Restore local tools (dotnet-format, etc.)
if (-not $NoRestore) {
  $ToolManifest = Join-Path $RepoRoot 'dotnet-tools.json'
  $LegacyToolManifest = Join-Path $RepoRoot '.config/dotnet-tools.json'
  if ((Test-Path $ToolManifest) -or (Test-Path $LegacyToolManifest)) {
    Write-Info 'Restoring local dotnet tools (dotnet tool restore)...'
    Invoke-Dotnet @('tool', 'restore')
  }
  else {
    Write-Warn "Tool manifest not found at $ToolManifest (or $LegacyToolManifest). Skipping tool restore."
    Write-Warn 'If you want a pinned tool version, create it with: dotnet new tool-manifest'
  }
}

# Decide formatting target
$ResolvedTarget = $Target
if ([string]::IsNullOrWhiteSpace($ResolvedTarget)) {
  $Solutions = @(Get-ChildItem -Path $RepoRoot -Filter *.sln -File -ErrorAction SilentlyContinue)
  if ($Solutions.Count -eq 1) {
    $ResolvedTarget = $Solutions[0].FullName
  }
  elseif ($Solutions.Count -gt 1) {
    $ResolvedTarget = $Solutions[0].FullName
    Write-Warn "Multiple .sln files found. Using: $ResolvedTarget"
  }
  else {
    $Projects = @(Get-ChildItem -Path $RepoRoot -Recurse -Filter *.csproj -File -ErrorAction SilentlyContinue)
    if ($Projects.Count -eq 1) {
      $ResolvedTarget = $Projects[0].FullName
    }
    elseif ($Projects.Count -gt 1) {
      $ResolvedTarget = $Projects[0].FullName
      Write-Warn "Multiple .csproj files found. Using: $ResolvedTarget"
    }
  }
}

# Build command
$VerifyArgs = @()
if (-not $Apply) {
  $VerifyArgs += '--verify-no-changes'
}

$TargetArgs = @()
if (-not [string]::IsNullOrWhiteSpace($ResolvedTarget)) {
  $TargetArgs += $ResolvedTarget
}

Write-Info (('Running: dotnet format ' + ($TargetArgs -join ' ') + ' ' + ($VerifyArgs -join ' ') + ' ' + ($DotnetFormatArgs -join ' ')).Trim())

$FormatArgs = @('format') + $TargetArgs + $VerifyArgs + $DotnetFormatArgs
Invoke-Dotnet $FormatArgs

Write-Info 'OK'

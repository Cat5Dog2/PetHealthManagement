<#
.SYNOPSIS
  Runs tests for the .NET solution/project in this repository.

.DESCRIPTION
  - Resolves repository root from the script location (walks up until it finds .git/, tool-manifest, global.json, or a *.sln).
  - Runs:
      dotnet test -c <Configuration> <extra args...>

.PARAMETER Configuration
  Test configuration. Defaults to $env:CONFIGURATION, otherwise "Debug".

.PARAMETER DotnetArgs
  Extra arguments passed through to `dotnet test`.
  Example:
    ./scripts/test.ps1 -Configuration Release -- --filter FullyQualifiedName~MyTests
#>

param(
  [Alias('c')]
  [string]$Configuration = $env:CONFIGURATION,

  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$DotnetArgs
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

if ([string]::IsNullOrWhiteSpace($Configuration)) {
  $Configuration = 'Debug'
}

$DotnetArgs = @($DotnetArgs)

# If the caller used `--` as a separator, drop it before passing through to dotnet.
if ($DotnetArgs.Count -gt 0 -and $DotnetArgs[0] -eq '--') {
  if ($DotnetArgs.Count -eq 1) {
    $DotnetArgs = @()
  }
  else {
    $DotnetArgs = $DotnetArgs[1..($DotnetArgs.Count - 1)]
  }
}

$TestArgs = @('test', '-c', $Configuration) + $DotnetArgs
Invoke-Dotnet $TestArgs

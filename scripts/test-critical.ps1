<#
.SYNOPSIS
  Runs the critical CI test subset for this repository.

.DESCRIPTION
  - Resolves repository root from the script location.
  - Runs:
      dotnet test -c <Configuration> --filter "CiTier=Critical" <extra args...>

.PARAMETER Configuration
  Test configuration. Defaults to $env:CONFIGURATION, otherwise "Debug".

.PARAMETER DotnetArgs
  Extra arguments passed through to `dotnet test`.
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
if ($DotnetArgs.Count -gt 0 -and $DotnetArgs[0] -eq '--') {
  if ($DotnetArgs.Count -eq 1) {
    $DotnetArgs = @()
  }
  else {
    $DotnetArgs = $DotnetArgs[1..($DotnetArgs.Count - 1)]
  }
}

$TestArgs = @('test', '-c', $Configuration, '--filter', 'CiTier=Critical') + $DotnetArgs
Invoke-Dotnet $TestArgs

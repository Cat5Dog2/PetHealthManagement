<#
.SYNOPSIS
  Runs the Playwright E2E test project.

.DESCRIPTION
  - Resolves repository root from the script location.
  - Builds the E2E test project.
  - Optionally installs the requested Playwright browser.
  - Runs:
      RUN_PLAYWRIGHT_E2E=1 BROWSER=<Browser> dotnet test <E2E project> -c <Configuration> --no-build <extra args...>

.PARAMETER Configuration
  Test configuration. Defaults to $env:CONFIGURATION, otherwise "Debug".

.PARAMETER Browser
  Playwright browser. Defaults to $env:BROWSER, otherwise "chromium".

.PARAMETER InstallBrowsers
  Installs the selected Playwright browser before running the E2E tests.

.PARAMETER DotnetArgs
  Extra arguments passed through to `dotnet test`.
  Example:
    ./scripts/test-e2e.ps1 -Browser firefox -DotnetArgs '--filter','FullyQualifiedName~MyTests'
#>

param(
  [Alias('c')]
  [string]$Configuration = $env:CONFIGURATION,

  [string]$Browser = $env:BROWSER,

  [switch]$InstallBrowsers,

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

function Invoke-External([string]$FilePath, [string[]]$Arguments) {
  & $FilePath @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "$FilePath $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
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

function Get-PlaywrightScript([string]$ProjectPath, [string]$Configuration) {
  $projectDir = Split-Path -Parent $ProjectPath
  $binDir = Join-Path $projectDir "bin\$Configuration"
  $script = Get-ChildItem -LiteralPath $binDir -Filter playwright.ps1 -Recurse -File -ErrorAction SilentlyContinue |
    Select-Object -First 1

  if ($null -eq $script) {
    throw "Could not find generated Playwright script under $binDir. Build the E2E test project first."
  }

  return $script.FullName
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

if ([string]::IsNullOrWhiteSpace($Browser)) {
  $Browser = 'chromium'
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

$ProjectPath = Join-Path $RepoRoot 'tests\PetHealthManagement.Web.E2ETests\PetHealthManagement.Web.E2ETests.csproj'

Write-Info "Building E2E test project ($Configuration)..."
$BuildArgs = @('build', $ProjectPath, '-c', $Configuration)
Invoke-Dotnet $BuildArgs

if ($InstallBrowsers) {
  $PlaywrightScript = Get-PlaywrightScript $ProjectPath $Configuration
  Write-Info "Installing Playwright browser: $Browser"
  Invoke-External $PlaywrightScript @('install', $Browser)
}

$HadRunPlaywrightE2E = Test-Path Env:\RUN_PLAYWRIGHT_E2E
$PreviousRunPlaywrightE2E = $env:RUN_PLAYWRIGHT_E2E
$HadBrowser = Test-Path Env:\BROWSER
$PreviousBrowser = $env:BROWSER

try {
  $env:RUN_PLAYWRIGHT_E2E = '1'
  $env:BROWSER = $Browser

  Write-Info "Running Playwright E2E tests with $Browser..."
  $TestArgs = @('test', $ProjectPath, '-c', $Configuration, '--no-build') + $DotnetArgs
  Invoke-Dotnet $TestArgs
}
finally {
  if ($HadRunPlaywrightE2E) {
    $env:RUN_PLAYWRIGHT_E2E = $PreviousRunPlaywrightE2E
  }
  else {
    Remove-Item Env:\RUN_PLAYWRIGHT_E2E -ErrorAction SilentlyContinue
  }

  if ($HadBrowser) {
    $env:BROWSER = $PreviousBrowser
  }
  else {
    Remove-Item Env:\BROWSER -ErrorAction SilentlyContinue
  }
}

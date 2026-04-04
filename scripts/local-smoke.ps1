<#
.SYNOPSIS
  Runs a local HTTPS smoke check against the web app.

.DESCRIPTION
  - Starts the app with the https launch profile.
  - Verifies home, login, protected-route redirect, and shared error pages.
  - Optionally performs form login and verifies authenticated routes.

.PARAMETER BaseUrl
  HTTPS base URL. Defaults to https://localhost:7115.

.PARAMETER Email
  Login email for the optional authenticated smoke step.

.PARAMETER Password
  Login password for the optional authenticated smoke step.

.PARAMETER ExpectAdmin
  When set, also verifies /Admin/Users after login.
#>

param(
  [string]$BaseUrl = 'https://localhost:7115',
  [string]$Email,
  [string]$Password,
  [switch]$ExpectAdmin
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Info([string]$Message) {
  Write-Host "[$(Split-Path -Leaf $PSCommandPath)] $Message"
}

function Invoke-WebRequestAllowErrors {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Uri,
    [string]$Method = 'GET',
    [Microsoft.PowerShell.Commands.WebRequestSession]$WebSession,
    $Body,
    [string]$ContentType = 'application/x-www-form-urlencoded',
    [switch]$DisableRedirect
  )

  if ($DisableRedirect) {
    $request = [System.Net.HttpWebRequest]::Create($Uri)
    $request.Method = $Method.ToUpperInvariant()
    $request.AllowAutoRedirect = $false
    $request.ContentType = $ContentType

    if ($null -ne $WebSession) {
      $request.CookieContainer = $WebSession.Cookies
    }

    if ($Method -eq 'POST' -and $null -ne $Body) {
      $encodedBody =
        if ($Body -is [hashtable]) {
          ($Body.GetEnumerator() | ForEach-Object {
            '{0}={1}' -f [System.Uri]::EscapeDataString([string]$_.Key), [System.Uri]::EscapeDataString([string]$_.Value)
          }) -join '&'
        }
        else {
          [string]$Body
        }

      $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($encodedBody)
      $request.ContentLength = $bodyBytes.Length

      $requestStream = $request.GetRequestStream()
      try {
        $requestStream.Write($bodyBytes, 0, $bodyBytes.Length)
      }
      finally {
        $requestStream.Dispose()
      }
    }

    try {
      return [System.Net.HttpWebResponse]$request.GetResponse()
    }
    catch [System.Net.WebException] {
      if ($_.Exception.Response) {
        return [System.Net.HttpWebResponse]$_.Exception.Response
      }

      throw
    }
  }

  try {
    if ($Method -eq 'POST') {
      if ($null -ne $WebSession) {
        return Invoke-WebRequest -Uri $Uri -Method Post -WebSession $WebSession -Body $Body -ContentType $ContentType -UseBasicParsing
      }

      return Invoke-WebRequest -Uri $Uri -Method Post -Body $Body -ContentType $ContentType -UseBasicParsing
    }

    if ($null -ne $WebSession) {
      return Invoke-WebRequest -Uri $Uri -Method Get -WebSession $WebSession -UseBasicParsing
    }

    return Invoke-WebRequest -Uri $Uri -Method Get -UseBasicParsing
  }
  catch [System.Net.WebException] {
    if ($_.Exception.Response) {
      return $_.Exception.Response
    }

    throw
  }
}

function Wait-UntilAvailable {
  param(
    [string]$Uri,
    [string]$StartupLogPath,
    [int]$TimeoutSeconds = 30
  )

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  do {
    if (-not [string]::IsNullOrWhiteSpace($StartupLogPath) -and (Test-Path $StartupLogPath)) {
      $startupLog = Get-Content -LiteralPath $StartupLogPath -Raw -ErrorAction SilentlyContinue
      if ($startupLog -match [regex]::Escape("Now listening on: $Uri")) {
        return
      }
    }

    try {
      Invoke-WebRequest -Uri $Uri -Method Get -TimeoutSec 5 -UseBasicParsing | Out-Null
      return
    }
    catch {
      Start-Sleep -Milliseconds 500
    }
  } while ((Get-Date) -lt $deadline)

  throw "Timed out waiting for $Uri"
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

$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot = Get-RepoRoot $scriptDir
$stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) "pethealth-local-smoke-stdout-$([guid]::NewGuid().ToString('N')).log"
$stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) "pethealth-local-smoke-stderr-$([guid]::NewGuid().ToString('N')).log"
$completed = $false

Write-Info 'Checking HTTPS development certificate before smoke run...'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptDir 'dev-certs.ps1')
if ($LASTEXITCODE -ne 0) {
  throw 'HTTPS development certificate check failed.'
}

Write-Info "Starting app at $BaseUrl ..."
$process = Start-Process `
  -FilePath 'dotnet' `
  -ArgumentList @('run', '--project', 'src/PetHealthManagement.Web', '--launch-profile', 'https', '--no-build') `
  -WorkingDirectory $repoRoot `
  -RedirectStandardOutput $stdoutPath `
  -RedirectStandardError $stderrPath `
  -PassThru

try {
  Wait-UntilAvailable -Uri $BaseUrl -StartupLogPath $stdoutPath

  $homeResponse = Invoke-WebRequestAllowErrors -Uri "$BaseUrl/"
  if ($homeResponse.StatusCode -ne 200) {
    throw "Home page returned unexpected status code: $($homeResponse.StatusCode)"
  }

  $loginResponse = Invoke-WebRequestAllowErrors -Uri "$BaseUrl/Identity/Account/Login"
  if ($loginResponse.StatusCode -ne 200) {
    throw "Login page returned unexpected status code: $($loginResponse.StatusCode)"
  }

  $myPageResponse = Invoke-WebRequestAllowErrors -Uri "$BaseUrl/MyPage" -DisableRedirect
  if ($myPageResponse.StatusCode -ne 302) {
    throw "Anonymous /MyPage request returned unexpected status code: $($myPageResponse.StatusCode)"
  }

  foreach ($statusCode in 400, 403, 404, 500) {
    $errorResponse = Invoke-WebRequestAllowErrors -Uri "$BaseUrl/Error/$statusCode"
    if ([int]$errorResponse.StatusCode -ne $statusCode) {
      throw "/Error/$statusCode returned unexpected status code: $($errorResponse.StatusCode)"
    }
  }

  Write-Info 'Anonymous smoke checks passed.'

  if ([string]::IsNullOrWhiteSpace($Email) -or [string]::IsNullOrWhiteSpace($Password)) {
    $completed = $true
    Write-Info 'Skipping authenticated smoke step because Email/Password were not provided.'
    return
  }

  $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
  $loginPage = Invoke-WebRequest -Uri "$BaseUrl/Identity/Account/Login" -WebSession $session -UseBasicParsing
  $tokenMatch = [regex]::Match(
    $loginPage.Content,
    '<input name="__RequestVerificationToken" type="hidden" value="([^"]+)"')

  if (-not $tokenMatch.Success) {
    throw 'Failed to find antiforgery token on the login page.'
  }

  $loginForm = @{
    '__RequestVerificationToken' = $tokenMatch.Groups[1].Value
    'Input.Email' = $Email
    'Input.Password' = $Password
    'Input.RememberMe' = 'false'
  }

  $authenticatedResponse = Invoke-WebRequestAllowErrors -Uri "$BaseUrl/Identity/Account/Login" -Method POST -WebSession $session -Body $loginForm -DisableRedirect
  if ($authenticatedResponse.StatusCode -ne 302) {
    throw "Login POST returned unexpected status code: $($authenticatedResponse.StatusCode)"
  }

  $myPageAuthenticated = Invoke-WebRequestAllowErrors -Uri "$BaseUrl/MyPage" -WebSession $session
  if ($myPageAuthenticated.StatusCode -ne 200) {
    throw "Authenticated /MyPage returned unexpected status code: $($myPageAuthenticated.StatusCode)"
  }

  $petsResponse = Invoke-WebRequestAllowErrors -Uri "$BaseUrl/Pets" -WebSession $session
  if ($petsResponse.StatusCode -ne 200) {
    throw "Authenticated /Pets returned unexpected status code: $($petsResponse.StatusCode)"
  }

  if ($ExpectAdmin) {
    $adminResponse = Invoke-WebRequestAllowErrors -Uri "$BaseUrl/Admin/Users" -WebSession $session
    if ($adminResponse.StatusCode -ne 200) {
      throw "Authenticated /Admin/Users returned unexpected status code: $($adminResponse.StatusCode)"
    }
  }

  Write-Info 'Authenticated smoke checks passed.'
  $completed = $true
}
finally {
  if ($process -and -not $process.HasExited) {
    Stop-Process -Id $process.Id -Force
  }

  if ($completed) {
    foreach ($path in @($stdoutPath, $stderrPath)) {
      if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
      }
    }
  }
  else {
    Write-Info "Smoke run logs were kept for troubleshooting: $stdoutPath"
    Write-Info "Smoke run logs were kept for troubleshooting: $stderrPath"
  }
}

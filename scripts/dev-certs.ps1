<#
.SYNOPSIS
  Checks or trusts the local ASP.NET Core HTTPS development certificate.

.PARAMETER Trust
  Trusts the development certificate after checking the current status.
#>

param(
  [switch]$Trust
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

function Test-TrustedDevelopmentCertificate {
  & dotnet dev-certs https --check --trust
  return ($LASTEXITCODE -eq 0)
}

Write-Info 'Validating ASP.NET Core HTTPS development certificate and trust status...'
if (Test-TrustedDevelopmentCertificate) {
  Write-Info 'HTTPS development certificate is present and trusted.'
  return
}

if (-not $Trust) {
  throw 'HTTPS development certificate is missing or not trusted. Re-run with -Trust to trust the certificate on this machine.'
}

Write-Info 'Trusting ASP.NET Core HTTPS development certificate...'
Invoke-Dotnet @('dev-certs', 'https', '--trust')

if (-not (Test-TrustedDevelopmentCertificate)) {
  throw 'HTTPS development certificate trust validation failed after running --trust.'
}

Write-Info 'HTTPS development certificate is ready and trusted.'

#!/usr/bin/env bash
set -euo pipefail

log() { echo "[$(basename "$0")] $*"; }

check_trusted_cert() {
  dotnet dev-certs https --check --trust
}

TRUST=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --trust)
      TRUST=true
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

log "Validating ASP.NET Core HTTPS development certificate and trust status..."
if check_trusted_cert; then
  log "HTTPS development certificate is present and trusted."
  exit 0
fi

if [[ "$TRUST" != true ]]; then
  echo "HTTPS development certificate is missing or not trusted. Re-run with --trust to trust the certificate on this machine." >&2
  exit 1
fi

log "Trusting ASP.NET Core HTTPS development certificate..."
dotnet dev-certs https --trust

check_trusted_cert
log "HTTPS development certificate is ready and trusted."

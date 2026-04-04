#!/usr/bin/env bash
set -euo pipefail

log() { echo "[$(basename "$0")] $*"; }

find_repo_root() {
  local start_dir="$1"
  local dir="$start_dir"
  for _ in {1..8}; do
    if [[ -d "$dir/.git" ]] \
      || [[ -f "$dir/dotnet-tools.json" ]] \
      || [[ -f "$dir/.config/dotnet-tools.json" ]] \
      || [[ -f "$dir/global.json" ]] \
      || compgen -G "$dir"/*.sln >/dev/null; then
      echo "$dir"
      return 0
    fi
    local parent
    parent="$(cd "$dir/.." && pwd)"
    if [[ "$parent" == "$dir" ]]; then
      break
    fi
    dir="$parent"
  done
  if [[ "$(basename "$start_dir")" == "scripts" || "$(basename "$start_dir")" == "script" ]]; then
    echo "$(cd "$start_dir/.." && pwd)"
  else
    echo "$start_dir"
  fi
}

ADMIN_EMAIL="admin@example.com"
ADMIN_PASSWORD="${DEV_ADMIN_PASSWORD:-}"
ADMIN_DISPLAY_NAME="Development Admin"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --admin-email)
      ADMIN_EMAIL="${2:-}"
      shift 2
      ;;
    --admin-password)
      ADMIN_PASSWORD="${2:-}"
      shift 2
      ;;
    --admin-display-name)
      ADMIN_DISPLAY_NAME="${2:-}"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

if [[ -z "$ADMIN_PASSWORD" ]]; then
  echo "Admin password is required. Pass --admin-password or set DEV_ADMIN_PASSWORD." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(find_repo_root "$SCRIPT_DIR")"
cd "$REPO_ROOT"
log "RepoRoot: $REPO_ROOT"

dotnet user-secrets set --project src/PetHealthManagement.Web "DevelopmentSetup:AdminEmail" "$ADMIN_EMAIL"
dotnet user-secrets set --project src/PetHealthManagement.Web "DevelopmentSetup:AdminPassword" "$ADMIN_PASSWORD"
dotnet user-secrets set --project src/PetHealthManagement.Web "DevelopmentSetup:AdminDisplayName" "$ADMIN_DISPLAY_NAME"

ASPNETCORE_ENVIRONMENT=Development \
dotnet run --project src/PetHealthManagement.Web --no-launch-profile -- --setup-development

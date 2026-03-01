#!/usr/bin/env bash
set -euo pipefail

log() { echo "[$(basename "$0")] $*"; }
warn() { echo "[$(basename "$0")][WARN] $*" >&2; }

# Resolve repository root even if this script is executed from anywhere.
# Heuristics: walk up from the script directory until we find one of:
#   - .git/ folder
#   - .config/dotnet-tools.json
#   - global.json
#   - at least one *.sln file
find_repo_root() {
  local start_dir="$1"
  local dir="$start_dir"
  for _ in {1..8}; do
    if [[ -d "$dir/.git" ]] \
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
  # Fallback: if the script lives in repoRoot/script(s), use its parent; otherwise keep start_dir.
  if [[ "$(basename "$start_dir")" == "scripts" || "$(basename "$start_dir")" == "script" ]]; then
    echo "$(cd "$start_dir/.." && pwd)"
  else
    echo "$start_dir"
  fi
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(find_repo_root "$SCRIPT_DIR")"
cd "$REPO_ROOT"
log "RepoRoot: $REPO_ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is not installed or not on PATH." >&2
  exit 1
fi

CONFIGURATION="${CONFIGURATION:-Debug}"
if [[ "${1:-}" == "--configuration" ]]; then
  CONFIGURATION="${2:-$CONFIGURATION}"
  shift 2
fi

dotnet test -c "$CONFIGURATION" "$@"

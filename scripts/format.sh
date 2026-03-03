#!/usr/bin/env bash
set -euo pipefail

log() { echo "[$(basename "$0")] $*"; }
warn() { echo "[$(basename "$0")][WARN] $*" >&2; }

# Resolve repository root even if this script is executed from anywhere.
# Heuristics: walk up from the script directory until we find one of:
#   - .git/ folder
#   - dotnet-tools.json (or legacy .config/dotnet-tools.json)
#   - global.json
#   - at least one *.sln file
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
  # Fallback: if the script lives in repoRoot/script(s), use its parent; otherwise keep start_dir.
  if [[ "$(basename "$start_dir")" == "scripts" || "$(basename "$start_dir")" == "script" ]]; then
    echo "$(cd "$start_dir/.." && pwd)"
  else
    echo "$start_dir"
  fi
}

usage() {
  cat <<'USAGE'
Runs dotnet format.

Default is "check" mode (CI-friendly): verifies no changes are needed.
Use --apply to actually apply formatting.

Usage:
  ./scripts/format.sh                 # check
  ./scripts/format.sh --apply         # apply
  ./scripts/format.sh --no-restore    # skip dotnet tool restore
  ./scripts/format.sh --target path/to/app.sln
  ./scripts/format.sh -- --verbosity detailed
USAGE
}

APPLY=0
NO_RESTORE=0
TARGET=""
EXTRA_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --apply)
      APPLY=1
      shift
      ;;
    --no-restore)
      NO_RESTORE=1
      shift
      ;;
    --target)
      TARGET="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      EXTRA_ARGS+=("$@")
      break
      ;;
    *)
      EXTRA_ARGS+=("$1")
      shift
      ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(find_repo_root "$SCRIPT_DIR")"
cd "$REPO_ROOT"
log "RepoRoot: $REPO_ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is not installed or not on PATH." >&2
  exit 1
fi

if [[ $NO_RESTORE -eq 0 ]]; then
  if [[ -f "$REPO_ROOT/dotnet-tools.json" || -f "$REPO_ROOT/.config/dotnet-tools.json" ]]; then
    log "Restoring local dotnet tools (dotnet tool restore)..."
    dotnet tool restore
  else
    warn "Tool manifest not found at $REPO_ROOT/dotnet-tools.json (or legacy .config/dotnet-tools.json). Skipping tool restore."
    warn "If you want a pinned tool version, create it with: dotnet new tool-manifest"
  fi
fi

VERIFY_ARGS=()
if [[ $APPLY -eq 0 ]]; then
  VERIFY_ARGS+=(--verify-no-changes)
fi

RESOLVED_TARGET="$TARGET"
if [[ -z "$RESOLVED_TARGET" ]]; then
  mapfile -t SLNS < <(compgen -G "$REPO_ROOT"/*.sln || true)
  if [[ ${#SLNS[@]} -eq 1 ]]; then
    RESOLVED_TARGET="${SLNS[0]}"
  elif [[ ${#SLNS[@]} -gt 1 ]]; then
    RESOLVED_TARGET="${SLNS[0]}"
    warn "Multiple .sln files found. Using: $RESOLVED_TARGET"
  else
    mapfile -t CSPROJS < <(find "$REPO_ROOT" -name '*.csproj' -type f 2>/dev/null || true)
    if [[ ${#CSPROJS[@]} -eq 1 ]]; then
      RESOLVED_TARGET="${CSPROJS[0]}"
    elif [[ ${#CSPROJS[@]} -gt 1 ]]; then
      RESOLVED_TARGET="${CSPROJS[0]}"
      warn "Multiple .csproj files found. Using: $RESOLVED_TARGET"
    fi
  fi
fi

TARGET_ARGS=()
if [[ -n "$RESOLVED_TARGET" ]]; then
  TARGET_ARGS+=("$RESOLVED_TARGET")
fi

log "Running: dotnet format ${TARGET_ARGS[*]:-} ${VERIFY_ARGS[*]:-} ${EXTRA_ARGS[*]:-}"
dotnet format "${TARGET_ARGS[@]}" "${VERIFY_ARGS[@]}" "${EXTRA_ARGS[@]}"
log "OK"

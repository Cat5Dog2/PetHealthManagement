#!/usr/bin/env bash
set -euo pipefail

log() { echo "[${0##*/}] $*"; }

usage() {
  printf '%s\n' \
    'Usage: scripts/test-e2e.sh [options] [-- dotnet test args...]' \
    '' \
    'Options:' \
    '  --configuration <value>  Test configuration. Defaults to CONFIGURATION or Debug.' \
    '  --browser <value>        Playwright browser. Defaults to BROWSER or chromium.' \
    '  --install-browsers       Install the selected Playwright browser before testing.' \
    '  -h, --help               Show this help.'
}

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

  local start_leaf="${start_dir##*/}"
  if [[ "$start_leaf" == "scripts" || "$start_leaf" == "script" ]]; then
    echo "$(cd "$start_dir/.." && pwd)"
  else
    echo "$start_dir"
  fi
}

find_playwright_script() {
  local bin_dir="$1"
  local script=""

  shopt -s globstar nullglob
  local scripts=("$bin_dir"/**/playwright.ps1)
  shopt -u globstar nullglob

  if [[ ${#scripts[@]} -gt 0 ]]; then
    script="${scripts[0]}"
  fi

  if [[ -z "$script" ]]; then
    echo "Could not find generated Playwright script under $bin_dir. Build the E2E test project first." >&2
    exit 1
  fi

  echo "$script"
}

run_playwright_script() {
  local script="$1"
  local browser="$2"

  if command -v pwsh >/dev/null 2>&1; then
    pwsh "$script" install "$browser"
  elif command -v powershell >/dev/null 2>&1; then
    powershell -NoProfile -ExecutionPolicy Bypass -File "$script" install "$browser"
  else
    echo "PowerShell is required to run Playwright's generated install script." >&2
    exit 1
  fi
}

SCRIPT_SOURCE="${BASH_SOURCE[0]}"
if [[ "$SCRIPT_SOURCE" == */* ]]; then
  SCRIPT_SOURCE_DIR="${SCRIPT_SOURCE%/*}"
else
  SCRIPT_SOURCE_DIR="."
fi

SCRIPT_DIR="$(cd "$SCRIPT_SOURCE_DIR" && pwd)"
REPO_ROOT="$(find_repo_root "$SCRIPT_DIR")"
cd "$REPO_ROOT"
log "RepoRoot: $REPO_ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is not installed or not on PATH." >&2
  exit 1
fi

CONFIGURATION="${CONFIGURATION:-Debug}"
BROWSER="${BROWSER:-chromium}"
INSTALL_BROWSERS=false
DOTNET_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration)
      if [[ $# -lt 2 ]]; then
        echo "--configuration requires a value." >&2
        exit 1
      fi
      CONFIGURATION="$2"
      shift 2
      ;;
    --browser)
      if [[ $# -lt 2 ]]; then
        echo "--browser requires a value." >&2
        exit 1
      fi
      BROWSER="$2"
      shift 2
      ;;
    --install-browsers)
      INSTALL_BROWSERS=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      DOTNET_ARGS+=("$@")
      break
      ;;
    *)
      DOTNET_ARGS+=("$1")
      shift
      ;;
  esac
done

PROJECT_PATH="tests/PetHealthManagement.Web.E2ETests/PetHealthManagement.Web.E2ETests.csproj"

log "Building E2E test project ($CONFIGURATION)..."
dotnet build "$PROJECT_PATH" -c "$CONFIGURATION"

if [[ "$INSTALL_BROWSERS" == true ]]; then
  PLAYWRIGHT_SCRIPT="$(find_playwright_script "tests/PetHealthManagement.Web.E2ETests/bin/$CONFIGURATION")"
  log "Installing Playwright browser: $BROWSER"
  run_playwright_script "$PLAYWRIGHT_SCRIPT" "$BROWSER"
fi

log "Running Playwright E2E tests with $BROWSER..."
RUN_PLAYWRIGHT_E2E=1 BROWSER="$BROWSER" dotnet test "$PROJECT_PATH" -c "$CONFIGURATION" --no-build "${DOTNET_ARGS[@]}"

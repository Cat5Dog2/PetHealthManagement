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

make_temp_dir() {
  local temp_dir
  temp_dir="$(mktemp -d 2>/dev/null || mktemp -d -t pethealth-local-smoke)"
  echo "$temp_dir"
}

wait_until_available() {
  local uri="$1"
  local startup_log_path="$2"
  local timeout_seconds="${3:-30}"
  local deadline=$((SECONDS + timeout_seconds))

  while (( SECONDS < deadline )); do
    if [[ -n "$startup_log_path" ]] && [[ -f "$startup_log_path" ]] \
      && grep -Fq "Now listening on: $uri" "$startup_log_path"; then
      return 0
    fi

    if curl -sS --output /dev/null --max-time 5 "$uri"; then
      return 0
    fi

    sleep 0.5
  done

  echo "Timed out waiting for $uri" >&2
  return 1
}

perform_request() {
  : >"$RESPONSE_BODY_PATH"
  : >"$RESPONSE_HEADERS_PATH"
  curl -sS \
    -o "$RESPONSE_BODY_PATH" \
    -D "$RESPONSE_HEADERS_PATH" \
    -w '%{http_code}' \
    "$@"
}

extract_antiforgery_token() {
  local token
  token="$(
    grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' "$RESPONSE_BODY_PATH" \
      | head -n 1 \
      | sed 's/.* value="\([^"]*\)"/\1/'
  )"

  if [[ -z "$token" ]]; then
    return 1
  fi

  printf '%s' "$token"
}

BASE_URL="https://localhost:7115"
EMAIL=""
PASSWORD=""
EXPECT_ADMIN=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-url)
      BASE_URL="${2:-}"
      shift 2
      ;;
    --email)
      EMAIL="${2:-}"
      shift 2
      ;;
    --password)
      PASSWORD="${2:-}"
      shift 2
      ;;
    --expect-admin)
      EXPECT_ADMIN=true
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(find_repo_root "$SCRIPT_DIR")"
TEMP_DIR="$(make_temp_dir)"
STDOUT_PATH="$TEMP_DIR/stdout.log"
STDERR_PATH="$TEMP_DIR/stderr.log"
COOKIE_JAR_PATH="$TEMP_DIR/cookies.txt"
RESPONSE_BODY_PATH="$TEMP_DIR/response-body.html"
RESPONSE_HEADERS_PATH="$TEMP_DIR/response-headers.txt"
COMPLETED=false
APP_PID=""

cleanup() {
  if [[ -n "$APP_PID" ]] && kill -0 "$APP_PID" 2>/dev/null; then
    kill "$APP_PID" 2>/dev/null || true
    wait "$APP_PID" 2>/dev/null || true
  fi

  if [[ "$COMPLETED" == "true" ]]; then
    rm -rf "$TEMP_DIR"
  else
    log "Smoke run logs were kept for troubleshooting: $STDOUT_PATH"
    log "Smoke run logs were kept for troubleshooting: $STDERR_PATH"
  fi
}

trap cleanup EXIT

cd "$REPO_ROOT"
log "RepoRoot: $REPO_ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is not installed or not on PATH." >&2
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is required for local-smoke.sh." >&2
  exit 1
fi

log "Checking HTTPS development certificate before smoke run..."
bash "$SCRIPT_DIR/dev-certs.sh"

log "Starting app at $BASE_URL ..."
dotnet run --project src/PetHealthManagement.Web --launch-profile https --no-build \
  >"$STDOUT_PATH" 2>"$STDERR_PATH" &
APP_PID="$!"

wait_until_available "$BASE_URL" "$STDOUT_PATH"

status_code="$(perform_request "$BASE_URL/")"
if [[ "$status_code" != "200" ]]; then
  echo "Home page returned unexpected status code: $status_code" >&2
  exit 1
fi

status_code="$(perform_request "$BASE_URL/Identity/Account/Login")"
if [[ "$status_code" != "200" ]]; then
  echo "Login page returned unexpected status code: $status_code" >&2
  exit 1
fi

status_code="$(perform_request --max-redirs 0 "$BASE_URL/MyPage")"
if [[ "$status_code" != "302" ]]; then
  echo "Anonymous /MyPage request returned unexpected status code: $status_code" >&2
  exit 1
fi

for expected_status in 400 403 404 500; do
  status_code="$(perform_request "$BASE_URL/Error/$expected_status")"
  if [[ "$status_code" != "$expected_status" ]]; then
    echo "/Error/$expected_status returned unexpected status code: $status_code" >&2
    exit 1
  fi
done

log "Anonymous smoke checks passed."

if [[ -z "$EMAIL" || -z "$PASSWORD" ]]; then
  COMPLETED=true
  log "Skipping authenticated smoke step because --email/--password were not provided."
  exit 0
fi

status_code="$(perform_request -c "$COOKIE_JAR_PATH" -b "$COOKIE_JAR_PATH" "$BASE_URL/Identity/Account/Login")"
if [[ "$status_code" != "200" ]]; then
  echo "Login page for authenticated smoke returned unexpected status code: $status_code" >&2
  exit 1
fi

ANTIFORGERY_TOKEN="$(extract_antiforgery_token)" || {
  echo "Failed to find antiforgery token on the login page." >&2
  exit 1
}

status_code="$(
  perform_request \
    -X POST \
    -c "$COOKIE_JAR_PATH" \
    -b "$COOKIE_JAR_PATH" \
    --max-redirs 0 \
    --data-urlencode "__RequestVerificationToken=$ANTIFORGERY_TOKEN" \
    --data-urlencode "Input.Email=$EMAIL" \
    --data-urlencode "Input.Password=$PASSWORD" \
    --data-urlencode "Input.RememberMe=false" \
    "$BASE_URL/Identity/Account/Login"
)"
if [[ "$status_code" != "302" ]]; then
  echo "Login POST returned unexpected status code: $status_code" >&2
  exit 1
fi

status_code="$(perform_request -c "$COOKIE_JAR_PATH" -b "$COOKIE_JAR_PATH" "$BASE_URL/MyPage")"
if [[ "$status_code" != "200" ]]; then
  echo "Authenticated /MyPage returned unexpected status code: $status_code" >&2
  exit 1
fi

status_code="$(perform_request -c "$COOKIE_JAR_PATH" -b "$COOKIE_JAR_PATH" "$BASE_URL/Pets")"
if [[ "$status_code" != "200" ]]; then
  echo "Authenticated /Pets returned unexpected status code: $status_code" >&2
  exit 1
fi

if [[ "$EXPECT_ADMIN" == "true" ]]; then
  status_code="$(perform_request -c "$COOKIE_JAR_PATH" -b "$COOKIE_JAR_PATH" "$BASE_URL/Admin/Users")"
  if [[ "$status_code" != "200" ]]; then
    echo "Authenticated /Admin/Users returned unexpected status code: $status_code" >&2
    exit 1
  fi
fi

log "Authenticated smoke checks passed."
COMPLETED=true

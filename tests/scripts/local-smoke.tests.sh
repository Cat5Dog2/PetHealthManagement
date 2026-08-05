#!/usr/bin/env bash
# Regression tests for scripts/local-smoke.sh.
#
# These cover the two defects that made the CD run for #135 fail without saying
# where: readiness treated an HTTP 500 as "ready", and perform_request retried
# server errors that a check was deliberately asserting.
#
# curl is replaced by tests/scripts/fake-curl.sh, and SMOKE_RETRY_DELAY_SECONDS
# is set to 0, so nothing here waits on real time.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SMOKE_SCRIPT="$REPO_ROOT/scripts/local-smoke.sh"

BASE_URL="http://smoke.invalid"
SMOKE_PASSWORD="hunter2-should-never-be-logged"

tests_run=0
tests_failed=0
FAKE_CURL_DIR=""
STDOUT_FILE=""
STDERR_FILE=""
SMOKE_EXIT=0

log() { echo "[local-smoke.tests] $*"; }

setup() {
  FAKE_CURL_DIR="$(mktemp -d)"
  mkdir -p "$FAKE_CURL_DIR/bin"
  cp "$SCRIPT_DIR/fake-curl.sh" "$FAKE_CURL_DIR/bin/curl"
  chmod +x "$FAKE_CURL_DIR/bin/curl"
  : >"$FAKE_CURL_DIR/calls.log"
  STDOUT_FILE="$FAKE_CURL_DIR/stdout.txt"
  STDERR_FILE="$FAKE_CURL_DIR/stderr.txt"
  queue_defaults
}

# A baseline where every check gets the status it asserts, so each test only has
# to describe the endpoint it wants to go wrong. The fake serves 200 for
# anything left unqueued.
queue_defaults() {
  # Anonymous 302, then authenticated 200.
  queue_response "$BASE_URL/MyPage" "0|302" "0|200"
  # Anonymous GET, authenticated GET, then the login POST redirect.
  queue_response "$BASE_URL/Identity/Account/Login" "0|200" "0|200" "0|302"
  local code
  for code in 400 403 404 500; do
    queue_response "$BASE_URL/Error/$code" "0|$code"
  done
}

teardown() {
  [[ -n "$FAKE_CURL_DIR" ]] && rm -rf "$FAKE_CURL_DIR"
  FAKE_CURL_DIR=""
}

# queue_response <url> <curl_exit>|<status> ...
queue_response() {
  local url="$1"
  shift
  local key
  key="$(printf '%s' "${url#*://}" | tr -c 'A-Za-z0-9' '_')"
  printf '%s\n' "$@" >"$FAKE_CURL_DIR/q_$key"
}

run_smoke() {
  PATH="$FAKE_CURL_DIR/bin:$PATH" \
  FAKE_CURL_DIR="$FAKE_CURL_DIR" \
  SMOKE_RETRY_DELAY_SECONDS=0 \
    bash "$SMOKE_SCRIPT" --use-existing-app --base-url "$BASE_URL" --timeout 5 "$@" \
    >"$STDOUT_FILE" 2>"$STDERR_FILE"
  SMOKE_EXIT=$?
  return 0
}

fail() {
  echo "  FAIL: $*" >&2
  tests_failed=$((tests_failed + 1))
}

assert_exit_zero() {
  if (( SMOKE_EXIT != 0 )); then
    fail "expected exit 0, got $SMOKE_EXIT. stderr: $(cat "$STDERR_FILE")"
  fi
}

assert_exit_nonzero() {
  if (( SMOKE_EXIT == 0 )); then
    fail "expected a non-zero exit, got 0"
  fi
}

assert_output_contains() {
  if ! grep -Fq "$1" "$STDOUT_FILE" "$STDERR_FILE"; then
    fail "expected output to contain: $1"
  fi
}

assert_output_missing() {
  if grep -Fq "$1" "$STDOUT_FILE" "$STDERR_FILE"; then
    fail "expected output NOT to contain: $1"
  fi
}

# Counting requests is how a "did not retry" claim is checked without depending
# on the wording of any log line.
assert_call_count() {
  local needle="$1" expected="$2" actual
  actual="$(grep -c -F -- "$needle" "$FAKE_CURL_DIR/calls.log" 2>/dev/null || true)"
  actual="${actual:-0}"
  if [[ "$actual" != "$expected" ]]; then
    fail "expected $expected request(s) to $needle, saw $actual"
  fi
}

start_test() {
  tests_run=$((tests_run + 1))
  echo "- $1"
  setup
}

# ---------------------------------------------------------------------------

start_test "readiness waits through 500 until the app returns 200"
# Readiness and the home-page check share this queue, so the run only survives
# if readiness consumed every 500 before the checks began. Returning early on
# the first 500 leaves the remaining ones for the home-page check, which then
# exhausts its retries and fails the run.
queue_response "$BASE_URL" "0|500" "0|500" "0|500" "0|500" "0|500" "0|200"
run_smoke
assert_exit_zero
assert_output_contains "Anonymous smoke checks passed."
teardown

start_test "readiness waits through a transport failure until the app returns 200"
queue_response "$BASE_URL" "7|000" "7|000" "7|000" "7|000" "7|000" "0|200"
run_smoke
assert_exit_zero
assert_output_contains "Anonymous smoke checks passed."
teardown

start_test "an expected /Error/500 succeeds without retrying"
run_smoke
assert_exit_zero
assert_call_count "$BASE_URL/Error/500" 1
teardown

start_test "an unexpected 500 fails the smoke run after the retry budget"
# The leading 200 lets readiness through; the 500s then belong to the home-page
# check and outlast its four attempts.
queue_response "$BASE_URL" "0|200" "0|500" "0|500" "0|500" "0|500"
run_smoke
assert_exit_nonzero
assert_output_contains "Home page returned unexpected status code: 500"
teardown

start_test "a final transport failure names the endpoint that failed"
queue_response "$BASE_URL" "0|200" "7|000" "7|000" "7|000" "7|000"
run_smoke
assert_exit_nonzero
assert_output_contains "Home page: request failed after 4 attempts"
teardown

start_test "the smoke account password never reaches stdout or stderr"
run_smoke --email "smoke@example.com" --password "$SMOKE_PASSWORD"
assert_exit_zero
assert_output_missing "$SMOKE_PASSWORD"
teardown

start_test "the production retry delay stays at 10 seconds"
# Static guard: the tests force the delay to 0, so an accidental edit to the
# default would otherwise go unnoticed here and only show up in CD.
if ! grep -Fq 'SMOKE_RETRY_DELAY_SECONDS:-10' "$SMOKE_SCRIPT"; then
  fail "the default retry delay in local-smoke.sh is no longer 10 seconds"
fi
teardown

# ---------------------------------------------------------------------------

if (( tests_failed > 0 )); then
  # Counted per assertion, not per test: one broken test can report several.
  log "$tests_failed assertion failure(s) across $tests_run tests."
  exit 1
fi

log "All $tests_run tests passed."

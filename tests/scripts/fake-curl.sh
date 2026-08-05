#!/usr/bin/env bash
# Stand-in for curl, used by local-smoke.tests.sh.
#
# Responses are scripted per URL through queue files in $FAKE_CURL_DIR:
#   q_<sanitized-url>   one "<curl_exit>|<http_status>" per line, consumed in order
# An empty or missing queue falls back to a successful 200, so a test only has
# to describe the endpoints it actually cares about.
#
# Invocations are recorded in $FAKE_CURL_DIR/calls.log as bare URLs. The
# argument list is deliberately never logged: the login POST carries the smoke
# account password.
set -uo pipefail

out=""
dump=""
url=""

args=("$@")
i=0
while (( i < ${#args[@]} )); do
  case "${args[$i]}" in
    -o|--output)
      i=$((i + 1))
      out="${args[$i]:-}"
      ;;
    -D|--dump-header)
      i=$((i + 1))
      dump="${args[$i]:-}"
      ;;
    # Flags that take a value we do not care about; skip the value so it is
    # never mistaken for the URL.
    -w|--write-out|-c|--cookie-jar|-b|--cookie|-X|--request|--max-time|--max-redirs|--data-urlencode|--data)
      i=$((i + 1))
      ;;
    http://*|https://*)
      url="${args[$i]}"
      ;;
  esac
  i=$((i + 1))
done

printf '%s\n' "$url" >>"$FAKE_CURL_DIR/calls.log"

# A trailing slash is dropped so readiness ("$BASE_URL") and the home-page check
# ("$BASE_URL/") draw from one queue. They hit the same endpoint, so a test that
# gave them separate queues could not tell whether readiness actually waited.
key="$(printf '%s' "${url#*://}" | sed 's:/$::' | tr -c 'A-Za-z0-9' '_')"
queue="$FAKE_CURL_DIR/q_$key"

curl_exit=0
status=200
if [[ -s "$queue" ]]; then
  first_line="$(head -n 1 "$queue")"
  IFS='|' read -r curl_exit status <<<"$first_line"
  tail -n +2 "$queue" >"$queue.tmp" && mv "$queue.tmp" "$queue"
fi

if [[ -n "$out" ]]; then
  # The login page has to carry a token or extract_antiforgery_token aborts.
  if [[ "$url" == *Login* ]]; then
    printf '%s' '<input name="__RequestVerificationToken" type="hidden" value="fake-token" />' >"$out"
  else
    printf '%s' '<html></html>' >"$out"
  fi
fi

if [[ -n "$dump" ]]; then
  printf 'HTTP/1.1 %s\r\nX-Content-Type-Options: nosniff\r\n\r\n' "$status" >"$dump"
fi

if (( curl_exit != 0 )); then
  echo "curl: ($curl_exit) fake transport failure" >&2
  exit "$curl_exit"
fi

printf '%s' "$status"

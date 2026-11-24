#!/usr/bin/env bash
# Smoke check for the API's container image. Builds the image from this
# repository, runs it on a fresh volume, and checks, in order:
#   1. readiness   - /health/ready answers 200
#   2. API key     - a protected route answers 401 without the key, 200 with it
#   3. non-root    - the process in the container does not run as root
#   4. persistence - a watchlist written through the API is still there after
#                    the container is removed and a new one starts on the volume
# Exits non-zero on the first failed check, naming it. Removes the containers,
# volume and image it created, and nothing else, however it exits.
#
# It never reaches CoinGecko: it only uses routes that do not call it, and the
# container resolves CoinGecko's host to itself, so a call would fail rather
# than spend a real key's rate limit. Both keys are placeholders.
#
# Run from anywhere: bash test/container-smoke.sh
set -euo pipefail

run_id="$(date +%s)-$$"
image="cryptostuff-api-smoke:$run_id"
volume="cryptostuff-smoke-$run_id"
container_prefix="cryptostuff-smoke-$run_id"
api_key="smoke-api-key-not-a-secret"
coingecko_key="smoke-coingecko-key-not-a-secret"
ready_timeout_seconds=90

containers=()
current=""
base_url=""
status=""
body=""

# Git Bash rewrites arguments that look like Unix paths before a native
# program sees them (`/data` would reach docker as `C:/Program Files/Git/data`).
# MSYS_NO_PATHCONV turns that off for docker only; elsewhere it means nothing.
docker() {
  MSYS_NO_PATHCONV=1 command docker "$@"
}

# Every name below is unique to this run, so removing them never touches a
# resource the script did not create; a name that was never created just
# fails to remove, silently.
cleanup() {
  local container
  for container in "${containers[@]}"; do
    docker rm -f "$container" >/dev/null 2>&1 || true
  done
  docker volume rm "$volume" >/dev/null 2>&1 || true
  docker image rm "$image" >/dev/null 2>&1 || true
}
trap cleanup EXIT
trap 'exit 130' INT TERM

fail() {
  echo "SMOKE CHECK FAILED: $1" >&2
  if [[ -n "$current" ]]; then
    echo "--- logs of $current ---" >&2
    docker logs "$current" >&2 2>&1 || true
  fi
  exit 1
}

pass() {
  echo "PASS $1"
}

start_container() {
  current="$container_prefix-$1"
  containers+=("$current")
  docker run --detach --name "$current" \
    --publish 127.0.0.1::8080 \
    --volume "$volume:/data" \
    --env "Api__Key=$api_key" \
    --env "CoinGecko__ApiKey=$coingecko_key" \
    --add-host api.coingecko.com:127.0.0.1 \
    "$image" >/dev/null || fail "$2: docker run failed for $current"
  local mapping
  mapping=$(docker port "$current" 8080/tcp) || fail "$2: no host port published for $current (did it exit?)"
  base_url="http://${mapping%%$'\n'*}"
}

# Polls readiness until it answers 200, the container stops, or the timeout.
wait_until_ready() {
  local check=$1 deadline=$((SECONDS + ready_timeout_seconds)) ready_status=""
  while ((SECONDS < deadline)); do
    ready_status=$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 5 "$base_url/health/ready") || true
    if [[ "$ready_status" == 200 ]]; then
      return 0
    fi
    if [[ "$(docker inspect --format '{{.State.Running}}' "$current")" != true ]]; then
      fail "$check: $current stopped before /health/ready answered 200"
    fi
    sleep 1
  done
  fail "$check: /health/ready did not answer 200 within ${ready_timeout_seconds}s (last status: ${ready_status:-none})"
}

# request METHOD PATH [curl options...] - sets $status and $body.
request() {
  local method=$1 path=$2 response
  shift 2
  response=$(curl --silent --max-time 10 --request "$method" --write-out $'\n%{http_code}' "$@" "$base_url$path") || true
  status=${response##*$'\n'}
  body=${response%$'\n'*}
}

cd "$(dirname "${BASH_SOURCE[0]}")/.."

echo "==> Building $image"
docker build --tag "$image" . || fail "build: docker build failed"

docker volume create "$volume" >/dev/null || fail "build: could not create volume $volume"

echo "==> Starting the first container"
start_container first readiness
wait_until_ready readiness
pass "readiness: /health/ready answered 200 at $base_url"

request GET /watchlists
[[ "$status" == 401 ]] || fail "API key: GET /watchlists without the key answered $status, not 401"
request GET /watchlists --header "X-Api-Key: $api_key"
[[ "$status" == 200 ]] || fail "API key: GET /watchlists with the key answered $status, not 200"
pass "API key: GET /watchlists answered 401 without the key and 200 with it"

uids=$(docker exec "$current" cat /proc/1/status | awk '/^Uid:/ { print $2, $3 }') \
  || fail "non-root: could not read the process's user from /proc/1/status"
read -r real_uid effective_uid <<<"$uids"
if [[ -z "$real_uid" || "$real_uid" == 0 || "$effective_uid" == 0 ]]; then
  fail "non-root: the container's process runs as uid '${real_uid:-unknown}' (effective '${effective_uid:-unknown}')"
fi
pass "non-root: the container's process runs as uid $real_uid"

watchlist_name="smoke-$run_id"
request POST /watchlists --header "X-Api-Key: $api_key" --header 'Content-Type: application/json' \
  --data "{\"name\":\"$watchlist_name\"}"
[[ "$status" == 201 ]] || fail "persistence: POST /watchlists answered $status, not 201: $body"
watchlist_id=$(sed -n 's/.*"id":\([0-9][0-9]*\).*/\1/p' <<<"$body")
[[ -n "$watchlist_id" ]] || fail "persistence: no id in the created watchlist: $body"

echo "==> Removing the first container and starting a second on the same volume"
docker rm --force "$current" >/dev/null || fail "persistence: could not remove $current"
start_container second persistence
wait_until_ready persistence
request GET "/watchlists/$watchlist_id" --header "X-Api-Key: $api_key"
[[ "$status" == 200 ]] || fail "persistence: GET /watchlists/$watchlist_id after the restart answered $status, not 200: $body"
[[ "$body" == *"\"name\":\"$watchlist_name\""* ]] \
  || fail "persistence: watchlist $watchlist_id after the restart is not '$watchlist_name': $body"
pass "persistence: watchlist $watchlist_id ($watchlist_name) survived the container's replacement"

current=""
echo "Smoke check passed: readiness, API key, non-root, persistence."

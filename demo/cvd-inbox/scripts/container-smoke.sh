#!/usr/bin/env bash
# Runs the CVD Inbox image the way the docs recommend (read-only, no capabilities, data volume) and checks it.
# Usage: scripts/container-smoke.sh [image]
set -euo pipefail
# Git Bash would turn /home/app in "volume:/home/app" into a Windows path. No effect on Linux.
export MSYS_NO_PATHCONV=1

image="${1:-cvd-inbox:0.1.0}"
name="cvd-inbox-smoke-$$"
volume="$name-data"
port=18080
expires="$(date -u -d '+180 days' +%Y-%m-%dT%H:%M:%SZ)"
password='smoke test password, long enough'

cleanup() {
  docker rm -f "$name" >/dev/null 2>&1 || true
  docker volume rm "$volume" >/dev/null 2>&1 || true
}
trap cleanup EXIT

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

start() {
  docker run -d --name "$name" --read-only --cap-drop=ALL --security-opt=no-new-privileges \
    -v "$volume:/home/app" -p "127.0.0.1:$port:8080" \
    -e Disclosure__PublicOrigin=https://inbox.example.test \
    -e "Disclosure__Expires=$expires" \
    "$image" >/dev/null
  for _ in $(seq 1 30); do
    curl -fsS "http://127.0.0.1:$port/.well-known/security.txt" >/dev/null 2>&1 && return 0
    sleep 1
  done
  docker logs "$name" >&2
  fail "the app did not answer on port $port"
}

user="$(docker image inspect "$image" --format '{{.Config.User}}')" || fail "cannot inspect image $image"
[[ "$user" =~ ^[1-9][0-9]*(:[0-9]+)?$ ]] || fail "the image does not run as a numeric non-root user (User='$user')"

ports="$(docker image inspect "$image" --format '{{json .Config.ExposedPorts}}')" || fail "cannot inspect image $image"
[ "$ports" = '{"8080/tcp":{}}' ] || fail "unexpected exposed ports: $ports"

start
# No "-o /dev/null": with MSYS_NO_PATHCONV a Windows curl cannot write to it. Read the last line instead.
content_type="$(curl -fsS -w '\n%{content_type}' "http://127.0.0.1:$port/.well-known/security.txt" | tail -n 1)" \
  || fail "security.txt request failed"
[ "$content_type" = "text/plain; charset=utf-8" ] || fail "security.txt has content type '$content_type'"

# Kestrel stops a chunked body that grows past 128 KiB; the in-memory test server cannot check this.
# A JSON string that never ends keeps the parser reading, so the limit is what stops it.
answer="$( { printf '{"product":"'; head -c 200000 /dev/zero | tr '\0' x; printf '"}'; } \
  | curl -sS -X POST -H 'Content-Type: application/json' -H 'Transfer-Encoding: chunked' --data-binary @- \
    -w '\n%{http_code} %{content_type}' "http://127.0.0.1:$port/api/reports" | tail -n 1)" \
  || fail "the chunked request failed"
[ "$answer" = "413 application/problem+json" ] || fail "a chunked body over 128 KiB got '$answer', not 413 with a problem"

# The session call writes the first data protection key; it must land in the volume.
curl -fsS "http://127.0.0.1:$port/api/auth/session" >/dev/null || fail "GET /api/auth/session failed"
printf '%s\n' "$password" | docker exec -i "$name" ./cvd-inbox create-operator smoke --password-stdin >/dev/null \
  || fail "create-operator failed"

files="$(docker cp "$name:/home/app/data" - | tar -t)" || fail "cannot read /home/app/data from the container"
grep -q 'data/cvd-inbox.db$' <<<"$files" || fail "no database in the data volume"
grep -q 'data/keys/key-.*\.xml$' <<<"$files" || fail "no data protection key in the data volume"

# TMPDIR is on the volume: nothing may be left at its root outside data/ (the first path part is the tar prefix).
volume_files="$(docker cp "$name:/home/app" - | tar -t)" || fail "cannot read /home/app from the container"
stray="$(grep -Ev '^[^/]+/$|^[^/]+/data(/|$)' <<<"$volume_files" || true)"
[ -z "$stray" ] || fail "files outside data/ in the volume: $stray"

[ "$(docker inspect -f '{{.State.Running}}' "$name")" = "true" ] || fail "the container is not running"
if docker exec "$name" sh -c true >/dev/null 2>&1; then
  fail "the image has a shell"
fi

docker rm -f "$name" >/dev/null
start
if second="$(printf '%s\n' "$password" | docker exec -i "$name" ./cvd-inbox create-operator smoke --password-stdin 2>&1)"; then
  fail "the operator did not survive a container recreate: create-operator succeeded again"
fi
grep -q 'already exists' <<<"$second" || fail "the second create-operator failed for another reason: $second"

docker exec "$name" ./cvd-inbox remove-operator smoke >/dev/null || fail "remove-operator failed"
printf '%s\n' "$password" | docker exec -i "$name" ./cvd-inbox create-operator smoke --password-stdin >/dev/null \
  || fail "create-operator after remove-operator failed"
echo "container smoke test passed"

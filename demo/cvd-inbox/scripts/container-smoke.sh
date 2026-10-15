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

user="$(docker image inspect "$image" --format '{{.Config.User}}')"
case "$user" in
  "" | 0 | root | 0:0) fail "the image runs as root (User='$user')" ;;
esac

ports="$(docker image inspect "$image" --format '{{json .Config.ExposedPorts}}')"
[ "$ports" = '{"8080/tcp":{}}' ] || fail "unexpected exposed ports: $ports"

start
# No "-o /dev/null": with MSYS_NO_PATHCONV a Windows curl cannot write to it. Read the last line instead.
content_type="$(curl -fsS -w '\n%{content_type}' "http://127.0.0.1:$port/.well-known/security.txt" | tail -n 1)"
[ "$content_type" = "text/plain; charset=utf-8" ] || fail "security.txt has content type '$content_type'"

# The session call writes the first data protection key; it must land in the volume.
curl -fsS "http://127.0.0.1:$port/api/auth/session" >/dev/null
printf '%s\n' "$password" | docker exec -i "$name" ./cvd-inbox create-operator smoke --password-stdin >/dev/null \
  || fail "create-operator failed"

files="$(docker cp "$name:/home/app/data" - | tar -t)"
grep -q 'data/cvd-inbox.db$' <<<"$files" || fail "no database in the data volume"
grep -q 'data/keys/key-.*\.xml$' <<<"$files" || fail "no data protection key in the data volume"

if docker exec "$name" sh -c true >/dev/null 2>&1; then
  fail "the image has a shell"
fi

docker rm -f "$name" >/dev/null
start
if printf '%s\n' "$password" | docker exec -i "$name" ./cvd-inbox create-operator smoke --password-stdin >/dev/null 2>&1; then
  fail "the operator did not survive a restart"
fi

echo "container smoke test passed"

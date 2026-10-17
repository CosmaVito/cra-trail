#!/usr/bin/env bash
# Packs the CLI, installs it as a dotnet tool in a temporary folder and runs every command once.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

dotnet pack "$root/src/CraTrail.Cli" -c Release -o "$work/nupkg" -p:Version=0.0.0-smoke >/dev/null
dotnet tool install CraTrail --tool-path "$work/tool" --add-source "$work/nupkg" --version 0.0.0-smoke >/dev/null
cra="$work/tool/cra-trail"
shared="$root/conformance/shared"

mkdir -p "$work/repo"
cd "$work/repo"
"$cra" --version
"$cra" init
cat > cra.yml <<'YAML'
product:
  name: Smoke Test
  support-end: 2031-12-31
  cvd-contact: security@example.org
YAML
"$cra" pack \
  --product-sbom "$shared/sbom-clean.cdx.json" \
  --grype "$shared/grype-clean.json" \
  --secrets-sarif "$shared/secrets-clean.sarif" \
  --sast-sarif "$shared/sast-clean.sarif" \
  --cvd-policy SECURITY.md \
  --created-at 2026-10-15T00:00:00Z \
  --out pack.zip
"$cra" evaluate pack.zip --out pack-again.zip
"$cra" verify pack-again.zip
"$cra" verify pack.zip
"$cra" gate pack.zip >/dev/null
echo "smoke test passed"

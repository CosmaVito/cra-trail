#!/usr/bin/env bash
# Builds the Angular client and copies it into the server's wwwroot, which git does not track.
set -euo pipefail

demo="$(cd "$(dirname "$0")/.." && pwd)"
wwwroot="$demo/src/CvdInbox/wwwroot"

cd "$demo/client"
npm ci
npx ng build
rm -rf "$wwwroot"
mkdir -p "$wwwroot"
cp -r dist/cvd-inbox/browser/. "$wwwroot/"
echo "client copied to $wwwroot"

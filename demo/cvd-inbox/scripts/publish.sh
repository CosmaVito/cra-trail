#!/usr/bin/env bash
# Builds the client, publishes CVD Inbox for linux-x64, then packs the release zip and the container image.
# Usage: scripts/publish.sh [version]
# Output: artifacts/publish/, artifacts/cvd-inbox-<version>-linux-x64.zip and the local image cvd-inbox:<version>.
set -euo pipefail

demo="$(cd "$(dirname "$0")/.." && pwd)"
version="${1:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$demo/src/CvdInbox/CvdInbox.csproj")}"
artifacts="$demo/artifacts"
publish="$artifacts/publish"
zip_file="$artifacts/cvd-inbox-$version-linux-x64.zip"

bash "$demo/scripts/build-client.sh"

rm -rf "$artifacts"
dotnet publish "$demo/src/CvdInbox/CvdInbox.csproj" -c Release -r linux-x64 --self-contained false \
  -p:Version="$version" -o "$publish"

if command -v zip >/dev/null 2>&1; then
  (cd "$publish" && zip -qrX "$zip_file" .)
else
  # Git Bash on Windows has no zip; the tar that ships with Windows writes zip archives.
  /c/Windows/System32/tar.exe -a -c -f "$(cygpath -w "$zip_file")" -C "$(cygpath -w "$publish")" .
fi

docker build -t "cvd-inbox:$version" "$demo"

echo "zip:   $zip_file"
echo "image: cvd-inbox:$version"

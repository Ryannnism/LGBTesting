#!/usr/bin/env bash
# Build release folder and zip it for upload to Lightsail.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$ROOT/out/release}"
ZIP="${2:-$ROOT/out/lgbapp-release.zip}"

"$ROOT/scripts/build-release.sh" "$OUT"

rm -f "$ZIP"
mkdir -p "$(dirname "$ZIP")"
(cd "$OUT" && zip -qr "$ZIP" .)

echo "Created $ZIP"

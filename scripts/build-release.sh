#!/usr/bin/env bash
# Build API + frontend into a deployable release folder (used locally and in CI).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$ROOT/out/release}"

rm -rf "$OUT"
mkdir -p "$OUT/api" "$OUT/frontend" "$OUT/scripts"

echo "Publishing API..."
dotnet publish "$ROOT/LGBApp.Backend/LGBApp.Backend.csproj" \
  -c Release \
  -o "$OUT/api" \
  --no-self-contained

echo "Building frontend..."
cd "$ROOT/LGBApp.Frontend"
if [[ -f package-lock.json ]]; then
  npm ci
else
  npm install
fi
npm run build
cp -r dist/* "$OUT/frontend/"

cp "$ROOT/scripts/deploy-lightsail-remote.sh" "$OUT/scripts/"
chmod +x "$OUT/scripts/deploy-lightsail-remote.sh"

{
  git -C "$ROOT" rev-parse HEAD 2>/dev/null || echo "unknown"
  date -u +%Y-%m-%dT%H:%MZ
} > "$OUT/VERSION.txt"

echo "Release ready at $OUT"

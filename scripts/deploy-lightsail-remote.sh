#!/usr/bin/env bash
# Run on the Lightsail VM (as root or via sudo) after uploading lgbapp-release.zip to /tmp.
set -euo pipefail

RELEASE_ZIP="${1:-/tmp/lgbapp-release.zip}"
APP_ROOT="${LGB_APP_ROOT:-/var/www/lgbapp}"
SERVICE_NAME="${LGB_SERVICE_NAME:-lgbapp-api}"
APP_USER="${LGB_APP_USER:-www-data}"

if [[ ! -f "$RELEASE_ZIP" ]]; then
  echo "Release zip not found: $RELEASE_ZIP" >&2
  exit 1
fi

STAGING="$(mktemp -d)"
cleanup() { rm -rf "$STAGING"; }
trap cleanup EXIT

unzip -qo "$RELEASE_ZIP" -d "$STAGING"

install -d -o "$APP_USER" -g "$APP_USER" "$APP_ROOT/api" "$APP_ROOT/frontend" "$APP_ROOT/uploads"

if systemctl is-active --quiet "$SERVICE_NAME"; then
  systemctl stop "$SERVICE_NAME"
fi

rsync -a --delete "$STAGING/api/" "$APP_ROOT/api/"
rsync -a --delete "$STAGING/frontend/" "$APP_ROOT/frontend/"
if [[ -f "$STAGING/scripts/deploy-lightsail-remote.sh" ]]; then
  install -m 755 "$STAGING/scripts/deploy-lightsail-remote.sh" "$APP_ROOT/scripts/deploy-lightsail-remote.sh"
fi
if [[ -f "$STAGING/VERSION.txt" ]]; then
  cp "$STAGING/VERSION.txt" "$APP_ROOT/VERSION.txt"
fi

chown -R "$APP_USER":"$APP_USER" "$APP_ROOT/api" "$APP_ROOT/frontend"

# Keep uploads on persistent disk; symlink if /var/data/lgbapp/uploads exists.
if [[ -d /var/data/lgbapp/uploads && ! -L "$APP_ROOT/api/uploads" ]]; then
  rm -rf "$APP_ROOT/api/uploads"
  ln -sfn /var/data/lgbapp/uploads "$APP_ROOT/api/uploads"
  chown -h "$APP_USER":"$APP_USER" "$APP_ROOT/api/uploads" || true
fi

systemctl daemon-reload
systemctl start "$SERVICE_NAME"
systemctl --no-pager --full status "$SERVICE_NAME"

echo "Deploy complete. Version:"
cat "$APP_ROOT/VERSION.txt" 2>/dev/null || true

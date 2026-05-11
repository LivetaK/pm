#!/usr/bin/env bash
set -euo pipefail

# start-dev.sh
# Convenience helper for local development.
# What it does:
#  - runs SQL migrations in migrations/ (via scripts/run-migrations.sh)
#  - starts the API in the background and writes logs to /tmp/pm-api.log
#  - opens Swagger UI in the default browser
#  - prints the stripe listen command (optionally attempts to open a new Terminal tab and run it if --auto-stripe is passed)
# Usage:
#   ./scripts/start-dev.sh           # run migrations, start API, open Swagger, print stripe listen command
#   ./scripts/start-dev.sh --auto-stripe  # also attempt to open a new Terminal tab and run stripe listen

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MIGRATIONS_SCRIPT="$ROOT_DIR/scripts/run-migrations.sh"
LOGFILE="/tmp/pm-api.log"
PIDFILE="/tmp/pm-api.pid"
API_PROJECT="$ROOT_DIR/src/pm.API/pm.API.csproj"
SWAGGER_URL="http://localhost:5216/swagger"
STRIPE_CMD="stripe listen --forward-to localhost:5216/api/v1/payments/webhook"

AUTO_STRIPE=0
if [ "${1:-}" = "--auto-stripe" ] || [ "${1:-}" = "--stripe" ]; then
  AUTO_STRIPE=1
fi

echo "[dev] root dir: $ROOT_DIR"

if [ ! -f "$MIGRATIONS_SCRIPT" ]; then
  echo "[dev] migrations script not found at $MIGRATIONS_SCRIPT" >&2
  exit 1
fi

echo "[dev] Running migrations..."
# ensure migrations script is executable
chmod +x "$MIGRATIONS_SCRIPT"
# run migrations (the script reads appsettings.Development.json if PG_CONN not set)
"$MIGRATIONS_SCRIPT"

# Start API in background
if [ -f "$PIDFILE" ]; then
  OLDPID=$(cat "$PIDFILE" || true)
  if [ -n "$OLDPID" ] && kill -0 "$OLDPID" 2>/dev/null; then
    echo "[dev] API already running with PID $OLDPID (stop it first or remove $PIDFILE)"
  else
    rm -f "$PIDFILE"
  fi
fi

echo "[dev] Starting API (logs -> $LOGFILE)"
nohup dotnet run --project "$API_PROJECT" > "$LOGFILE" 2>&1 &
API_PID=$!
echo "$API_PID" > "$PIDFILE"

# Wait for server to start listening (basic check)
echo -n "[dev] Waiting for server to respond"
for i in {1..20}; do
  if curl -sSf "http://localhost:5216/" >/dev/null 2>&1; then
    echo " -> up"
    break
  fi
  echo -n "."
  sleep 1
done

if ! curl -sSf "http://localhost:5216/" >/dev/null 2>&1; then
  echo "\n[dev] Warning: server did not respond after waiting. Check $LOGFILE for details."
fi

# Open Swagger UI
echo "[dev] Opening Swagger UI: $SWAGGER_URL"
open "$SWAGGER_URL" || echo "[dev] Could not open browser automatically. Open $SWAGGER_URL manually."

# Print stripe listen command
echo
echo "[dev] To forward Stripe webhooks to your local server, run (in a separate terminal):"
echo "  $STRIPE_CMD"

if [ "$AUTO_STRIPE" -eq 1 ]; then
  echo "[dev] Attempting to open a new Terminal tab and run Stripe listen..."
  # Use osascript to tell Terminal to run the command in a new tab (macOS)
  if command -v osascript >/dev/null 2>&1; then
    osascript <<EOF >/dev/null 2>&1 || true
tell application "Terminal"
    activate
    tell application "System Events" to keystroke "t" using command down
    delay 0.2
    do script "${STRIPE_CMD}" in front window
end tell
EOF
    echo "[dev] stripe listen started in new Terminal tab (if available)."
  else
    echo "[dev] osascript not available; cannot auto-start stripe listen. Run the command above manually." >&2
  fi
fi

echo
echo "[dev] Done. API PID: $API_PID  Logs: $LOGFILE  PID file: $PIDFILE"

exit 0


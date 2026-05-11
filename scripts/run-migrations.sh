#!/usr/bin/env bash
set -euo pipefail

# run-migrations.sh
# Usage:
#   PG_CONN can be provided as a .NET-style connection string or individual env vars.
#   If PG_CONN is not set, the script will try to read src/pm.API/appsettings.Development.json DefaultConnection.
# Example: PG_CONN="Host=localhost;Port=5432;Database=db;Username=user;Password=pass" ./scripts/run-migrations.sh

MIGRATIONS_DIR="$(dirname "$0")/../migrations"
if [ ! -d "$MIGRATIONS_DIR" ]; then
  echo "Migrations directory not found: $MIGRATIONS_DIR"
  exit 1
fi

CONN=${PG_CONN:-}
if [ -z "$CONN" ]; then
  if [ -f "src/pm.API/appsettings.Development.json" ]; then
    CONN=$(sed -n 's/.*"DefaultConnection": "\(.*\)".*/\1/p' src/pm.API/appsettings.Development.json || true)
  fi
fi

if [ -z "$CONN" ]; then
  echo "No connection string found. Set PG_CONN environment variable or configure src/pm.API/appsettings.Development.json"
  exit 1
fi

# Parse .NET-style connection string: Host=...;Port=...;Database=...;Username=...;Password=...
HOST=$(echo "$CONN" | sed -n 's/.*Host=\([^;]*\).*/\1/p')
PORT=$(echo "$CONN" | sed -n 's/.*Port=\([^;]*\).*/\1/p')
DB=$(echo "$CONN" | sed -n 's/.*Database=\([^;]*\).*/\1/p')
USER=$(echo "$CONN" | sed -n 's/.*Username=\([^;]*\).*/\1/p')
PASS=$(echo "$CONN" | sed -n 's/.*Password=\([^;]*\).*/\1/p')

if [ -z "$HOST" ] || [ -z "$DB" ] || [ -z "$USER" ]; then
  echo "Failed to parse connection string. Please provide a valid connection string in PG_CONN or appsettings.Development.json"
  exit 1
fi

if [ -z "$PORT" ]; then
  PORT=5432
fi

export PGPASSWORD="$PASS"

for f in $(ls "$MIGRATIONS_DIR"/*.sql | sort); do
  echo "Applying migration: $f"
  psql -h "$HOST" -p "$PORT" -U "$USER" -d "$DB" -f "$f"
done

echo "Migrations applied successfully."


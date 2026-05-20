#!/usr/bin/env bash
# scripts/start-pg.sh — bring the ProjectJA dev Postgres back to a healthy state.
#
# Safe to run any time:
#   • running container → no-op
#   • exited/created/paused container → start it
#   • container missing (Docker Desktop restart with --rm cleans it up) → recreate
#     it against the existing named volume so data survives
#
# Defaults match the README's "Running locally (on-prem mode)" recipe; every
# value can be overridden via env vars if you've customised the local setup.

set -euo pipefail

CONTAINER="${PROJECTJA_PG_CONTAINER:-projectja-pg}"
VOLUME="${PROJECTJA_PG_VOLUME:-projectja-pgdata}"
IMAGE="${PROJECTJA_PG_IMAGE:-postgres:16-alpine}"
PORT="${PROJECTJA_PG_PORT:-5432}"
PASSWORD="${POSTGRES_PASSWORD:-postgres}"
DB="${POSTGRES_DB:-projectja}"
READY_TIMEOUT_SECS="${READY_TIMEOUT_SECS:-30}"

# --- preflight ---------------------------------------------------------------
echo "→ Checking Docker..."
if ! command -v docker >/dev/null 2>&1; then
    echo "✗ docker command not found. Install Docker Desktop and re-run." >&2
    exit 1
fi
if ! docker info >/dev/null 2>&1; then
    echo "✗ Docker daemon is not responding. Start Docker Desktop and re-run." >&2
    exit 1
fi

# --- bring container up ------------------------------------------------------
# `docker inspect` exits non-zero AND can still write a stray line to stdout
# when the container doesn't exist, so an inline `|| echo missing` ends up with
# a multi-line value. Branch on the exit code explicitly instead.
if state="$(docker inspect --format '{{.State.Status}}' "$CONTAINER" 2>/dev/null)"; then
    :
else
    state="missing"
fi
echo "→ Container '$CONTAINER' state: $state"

case "$state" in
    running)
        echo "→ Already running — nothing to do."
        ;;
    exited|created|paused)
        echo "→ Starting existing container..."
        docker start "$CONTAINER" >/dev/null
        ;;
    missing)
        echo "→ Container missing. Recreating against volume '$VOLUME' (data preserved)..."
        docker run --rm -d --name "$CONTAINER" \
            -e POSTGRES_PASSWORD="$PASSWORD" \
            -e POSTGRES_DB="$DB" \
            -p "$PORT:5432" \
            -v "$VOLUME:/var/lib/postgresql/data" \
            "$IMAGE" >/dev/null
        ;;
    *)
        echo "✗ Unexpected container state: $state" >&2
        exit 1
        ;;
esac

# --- wait for readiness ------------------------------------------------------
echo "→ Waiting for Postgres to accept connections (timeout ${READY_TIMEOUT_SECS}s)..."
for ((i = 1; i <= READY_TIMEOUT_SECS; i++)); do
    if docker exec "$CONTAINER" pg_isready -q -U postgres -d "$DB" >/dev/null 2>&1; then
        echo "✓ Postgres ready after ${i}s."
        echo "  Connection: localhost:$PORT   user=postgres   db=$DB"
        echo "  Next: run the app from Rider, or:"
        echo "        dotnet run --project src/ProjectJA.Host"
        exit 0
    fi
    sleep 1
done

echo "✗ Postgres did not become ready within ${READY_TIMEOUT_SECS}s." >&2
echo "  Recent container logs:" >&2
docker logs --tail 30 "$CONTAINER" >&2
exit 1

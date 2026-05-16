#!/usr/bin/env bash
# Server-side roll: pull image, run migrator, swap app, wait for /healthz/ready.
# Usage:
#   ./deploy.sh            # uses :latest
#   ./deploy.sh v0.4.2     # specific tag

set -euo pipefail

PROJECT_ROOT="${PROJECT_ROOT:-/opt/projectja}"
COMPOSE_FILE="${COMPOSE_FILE:-${PROJECT_ROOT}/deploy/docker-compose.yml}"
IMAGE_TAG="${1:-latest}"
HEALTH_URL="${HEALTH_URL:-http://localhost:8080/healthz/ready}"
HEALTH_TIMEOUT_SECS="${HEALTH_TIMEOUT_SECS:-90}"

cd "${PROJECT_ROOT}"

echo "==> Deploying ProjectJA tag=${IMAGE_TAG}"
export PROJECTJA_TAG="${IMAGE_TAG}"

echo "==> Pulling image"
docker compose -f "${COMPOSE_FILE}" pull app

echo "==> Running per-tenant migrations"
docker compose -f "${COMPOSE_FILE}" run --rm app \
  dotnet migrator/ProjectJA.Tools.Migrator.dll

echo "==> Rolling app container"
docker compose -f "${COMPOSE_FILE}" up -d app

echo "==> Waiting for /healthz/ready (timeout=${HEALTH_TIMEOUT_SECS}s)"
deadline=$(( $(date +%s) + HEALTH_TIMEOUT_SECS ))
while [[ $(date +%s) -lt ${deadline} ]]; do
  if curl -fs "${HEALTH_URL}" > /dev/null 2>&1; then
    echo "==> Ready."
    docker image prune -f > /dev/null || true
    exit 0
  fi
  sleep 2
done

echo "!! App did not become ready within ${HEALTH_TIMEOUT_SECS}s. Recent logs:"
docker compose -f "${COMPOSE_FILE}" logs --tail=100 app || true
exit 1

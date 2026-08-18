#!/usr/bin/env bash
# Resumes a backend paused by stop.sh: starts the Postgres Flexible Server first (the
# container apps need it reachable on boot), then restores each Container App's normal scale
# settings. These min/max values must match the `scale` blocks in main.bicep.
#
# Usage: ./deploy/azure/start.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if [ -f "$REPO_ROOT/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  source "$REPO_ROOT/.env"
  set +a
fi

RESOURCE_GROUP="${RESOURCE_GROUP:?Set RESOURCE_GROUP in .env or as an environment variable}"
NAME_PREFIX="${NAME_PREFIX:-jobradar}"

echo "==> Starting Postgres Flexible Server"
PG_SERVER="$(az postgres flexible-server list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv)"
if [ -z "$PG_SERVER" ]; then
  echo "  (no Postgres Flexible Server found in $RESOURCE_GROUP - skipping)"
else
  echo "  - $PG_SERVER (this can take a minute or two)"
  az postgres flexible-server start --resource-group "$RESOURCE_GROUP" --name "$PG_SERVER" --output none
fi

echo "==> Restoring container app scale settings"
# Must match main.bicep's scale.minReplicas/maxReplicas for each app - jobaggregator and
# notifications are pinned at exactly 1 (see docs/azure-hosting.md for why); the rest scale
# 0-2 on their own after this.
declare -A MIN_REPLICAS=(
  ["$NAME_PREFIX-gateway"]=0
  ["$NAME_PREFIX-users"]=0
  ["$NAME_PREFIX-jobaggregator"]=1
  ["$NAME_PREFIX-matching"]=0
  ["$NAME_PREFIX-notifications"]=1
)
declare -A MAX_REPLICAS=(
  ["$NAME_PREFIX-gateway"]=2
  ["$NAME_PREFIX-users"]=2
  ["$NAME_PREFIX-jobaggregator"]=1
  ["$NAME_PREFIX-matching"]=2
  ["$NAME_PREFIX-notifications"]=1
)

for app in "${!MIN_REPLICAS[@]}"; do
  min="${MIN_REPLICAS[$app]}"
  max="${MAX_REPLICAS[$app]}"
  echo "  - $app (min=$min, max=$max)"
  az containerapp update --resource-group "$RESOURCE_GROUP" --name "$app" --min-replicas "$min" --max-replicas "$max" --output none
done

echo "==> Done. jobaggregator-service and notifications-service are pinned on and starting now;"
echo "    gateway/users/matching will cold-start on the first request (a few seconds)."

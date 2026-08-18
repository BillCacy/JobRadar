#!/usr/bin/env bash
# Pauses the deployed Azure backend to stop billing while you're not testing: scales all 5
# Container Apps to zero replicas and stops the Postgres Flexible Server. Nothing is deleted -
# run start.sh to bring it all back exactly as it was.
#
# Service Bus (Basic tier) and Log Analytics have no idle/compute cost, so there's nothing to
# stop for those - only Container Apps compute and Postgres compute bill while idle.
#
# Usage: ./deploy/azure/stop.sh
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

APPS=(
  "$NAME_PREFIX-gateway"
  "$NAME_PREFIX-users"
  "$NAME_PREFIX-jobaggregator"
  "$NAME_PREFIX-matching"
  "$NAME_PREFIX-notifications"
)

echo "==> Scaling all container apps to zero replicas in $RESOURCE_GROUP"
for app in "${APPS[@]}"; do
  echo "  - $app"
  az containerapp update --resource-group "$RESOURCE_GROUP" --name "$app" --min-replicas 0 --max-replicas 0 --output none
done

echo "==> Stopping Postgres Flexible Server"
PG_SERVER="$(az postgres flexible-server list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv)"
if [ -z "$PG_SERVER" ]; then
  echo "  (no Postgres Flexible Server found in $RESOURCE_GROUP - skipping)"
else
  echo "  - $PG_SERVER"
  az postgres flexible-server stop --resource-group "$RESOURCE_GROUP" --name "$PG_SERVER" --output none
fi

echo "==> Done. Run start.sh when you're ready to test again."
echo "    Note: Azure auto-restarts a stopped Postgres server after 7 days regardless - if you're pausing longer than that, re-run stop.sh afterward."

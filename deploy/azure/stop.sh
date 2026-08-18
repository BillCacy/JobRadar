#!/usr/bin/env bash
# Pauses the deployed Azure backend to stop billing while you're not testing: stops all 5
# Container Apps (via the runningStatus start/stop action - Azure requires max-replicas >= 1,
# so scaling to zero isn't an option) and stops the Postgres Flexible Server. Nothing is
# deleted - run start.sh to bring it all back exactly as it was.
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

SUBSCRIPTION_ID="$(az account show --query id -o tsv)" || {
  echo "Not logged in to Azure CLI - run 'az login' first." >&2
  exit 1
}

if ! az group show --name "$RESOURCE_GROUP" --output none; then
  echo "Resource group '$RESOURCE_GROUP' not found (or you don't have access) - check RESOURCE_GROUP in .env." >&2
  exit 1
fi

APPS=(
  "$NAME_PREFIX-gateway"
  "$NAME_PREFIX-users"
  "$NAME_PREFIX-jobaggregator"
  "$NAME_PREFIX-matching"
  "$NAME_PREFIX-notifications"
)

echo "==> Stopping all container apps in $RESOURCE_GROUP"
for app in "${APPS[@]}"; do
  echo "  - $app"
  az rest --method post --url "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.App/containerApps/$app/stop?api-version=2023-05-01" --output none
done

echo "==> Stopping Postgres Flexible Server"
PG_SERVER="$(az postgres flexible-server list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv)"
if [ -z "$PG_SERVER" ]; then
  echo "  (no Postgres Flexible Server found in $RESOURCE_GROUP - skipping)"
else
  PG_STATE="$(az postgres flexible-server show --resource-group "$RESOURCE_GROUP" --name "$PG_SERVER" --query "state" -o tsv)"
  if [ "$PG_STATE" = "Stopped" ]; then
    echo "  - $PG_SERVER is already stopped - skipping."
  elif [ "$PG_STATE" = "Ready" ]; then
    echo "  - $PG_SERVER"
    az postgres flexible-server stop --resource-group "$RESOURCE_GROUP" --name "$PG_SERVER" --output none
  else
    echo "Postgres server $PG_SERVER is in '$PG_STATE' state (expected 'Ready' or 'Stopped') - wait for it to settle and try again." >&2
    exit 1
  fi
fi

echo "==> Done. Run start.sh when you're ready to test again."
echo "    Note: Azure auto-restarts a stopped Postgres server after 7 days regardless - if you're pausing longer than that, re-run stop.sh afterward."

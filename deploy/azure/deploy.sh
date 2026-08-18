#!/usr/bin/env bash
# Builds/pushes the 5 service images to a container registry, then deploys main.bicep via
# main.bicepparam.
#
# Secrets are never read from a committed file - main.bicepparam pulls every value from the
# environment via readEnvironmentVariable(), and this script sources .env into the shell first
# (falling back to already-set environment variables) so it can supply them:
#   POSTGRES_ADMIN_PASSWORD, ADZUNA_APP_KEY (optional), JOOBLE_API_KEY (optional)
#
# Usage:
#   RESOURCE_GROUP=jobradar-rg CONTAINER_REGISTRY=ghcr.io/yourusername \
#   POSTGRES_ADMIN_PASSWORD='...' ./deploy/azure/deploy.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# Unlike `docker compose`, `az` doesn't auto-load .env - source it ourselves if present so the
# same file that configures docker-compose.yml can supply these too.
if [ -f "$REPO_ROOT/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  source "$REPO_ROOT/.env"
  set +a
fi

RESOURCE_GROUP="${RESOURCE_GROUP:?Set RESOURCE_GROUP, e.g. jobradar-rg}"
LOCATION="${LOCATION:-eastus2}"
CONTAINER_REGISTRY="${CONTAINER_REGISTRY:?Set CONTAINER_REGISTRY, e.g. ghcr.io/yourusername}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
# Not otherwise used in this script, but failing fast here beats spending several minutes
# building/pushing images only to have the bicepparam deployment reject a missing value.
: "${POSTGRES_ADMIN_PASSWORD:?Set POSTGRES_ADMIN_PASSWORD}"

cd "$REPO_ROOT"

echo "==> Building and pushing images to $CONTAINER_REGISTRY (tag: $IMAGE_TAG)"
declare -A DOCKERFILES=(
  [gateway]="src/Gateway/JobRadar.Gateway/Dockerfile"
  [users]="src/Services/JobRadar.Users/Dockerfile"
  [jobaggregator]="src/Services/JobRadar.JobAggregator/Dockerfile"
  [matching]="src/Services/JobRadar.Matching/Dockerfile"
  [notifications]="src/Services/JobRadar.Notifications/Dockerfile"
)

for svc in "${!DOCKERFILES[@]}"; do
  image="$CONTAINER_REGISTRY/jobradar-$svc:$IMAGE_TAG"
  echo "  - $image"
  docker build -f "${DOCKERFILES[$svc]}" -t "$image" .
  docker push "$image"
done

echo "==> Ensuring resource group $RESOURCE_GROUP exists in $LOCATION"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

echo "==> Deploying main.bicep via main.bicepparam"
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$REPO_ROOT/deploy/azure/main.bicep" \
  --parameters "$REPO_ROOT/deploy/azure/main.bicepparam" \
  --query "properties.outputs" \
  --output json

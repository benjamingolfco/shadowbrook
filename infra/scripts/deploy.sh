#!/usr/bin/env bash
set -euo pipefail

# Shadowbrook - Deploy Bicep infrastructure
# Usage: ./deploy.sh <environment> [--what-if]
#
# Two-phase subscription-level deployment:
#   1. Shared infrastructure (ACR) — creates shadowbrook-shared-rg
#   2. Environment infrastructure  — creates shadowbrook-{env}-rg
#
# Bicep is the single source of truth — resource groups are created
# by the templates, not by this script.
#
# Required environment variables:
#   SQL_ADMIN_LOGIN    — SQL Server admin username
#   SQL_ADMIN_PASSWORD — SQL Server admin password

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BICEP_DIR="$SCRIPT_DIR/../bicep"

if [ $# -eq 0 ]; then
  echo "Usage: $0 <environment> [--what-if]"
  echo "  environment: dev, staging, prod"
  echo ""
  echo "Required env vars: SQL_ADMIN_LOGIN, SQL_ADMIN_PASSWORD"
  exit 1
fi

ENVIRONMENT=$1
WHAT_IF_FLAG=""
if [ "${2:-}" == "--what-if" ]; then
  WHAT_IF_FLAG="--what-if"
fi

# Validate required environment variables (read by .bicepparam via readEnvironmentVariable)
: "${SQL_ADMIN_LOGIN:?Environment variable SQL_ADMIN_LOGIN is required}"
: "${SQL_ADMIN_PASSWORD:?Environment variable SQL_ADMIN_PASSWORD is required}"

SUBSCRIPTION_ID="37109c89-82e6-4907-8cd1-ca80800d0730"  # benjamingolfco
LOCATION="eastus2"
ACR_NAME="shadowbrookacr"

# Ensure we're deploying to the correct subscription
az account set --subscription "$SUBSCRIPTION_ID"

# Step 1: Deploy shared infrastructure (creates RG + ACR)
echo "==> Deploying shared infrastructure..."
az deployment sub create \
  --name "shadowbrook-shared" \
  --location "$LOCATION" \
  --parameters "$BICEP_DIR/parameters.shared.bicepparam" \
  $WHAT_IF_FLAG

# Step 1b: Seed ACR with a placeholder image if the repository doesn't exist yet
if [ -z "$WHAT_IF_FLAG" ]; then
  if ! az acr repository show --name "$ACR_NAME" --repository shadowbrook &>/dev/null; then
    echo "==> Seeding ACR with placeholder image..."
    az acr import --name "$ACR_NAME" \
      --source mcr.microsoft.com/dotnet/samples:aspnetapp \
      --image shadowbrook:latest
  else
    echo "==> ACR already has shadowbrook repository, skipping seed"
  fi
fi

# Step 2: Preserve running container image (avoid clobbering with placeholder)
CONTAINER_APP_NAME="shadowbrook-app-${ENVIRONMENT}"
RESOURCE_GROUP="shadowbrook-${ENVIRONMENT}-rg"
IMAGE_TAG=$(az containerapp show \
  --name "$CONTAINER_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.template.containers[0].image" \
  --output tsv 2>/dev/null | sed 's/.*://' || echo "latest")
echo "==> Using image tag: $IMAGE_TAG"

# Step 3: Deploy environment infrastructure (creates RG + all env resources)
echo "==> Deploying $ENVIRONMENT environment..."
az deployment sub create \
  --name "shadowbrook-${ENVIRONMENT}" \
  --location "$LOCATION" \
  --parameters "$BICEP_DIR/parameters.${ENVIRONMENT}.bicepparam" \
  --parameters imageTag="$IMAGE_TAG" \
  $WHAT_IF_FLAG

#!/usr/bin/env bash
set -euo pipefail

# Shadowbrook - Deploy Bicep infrastructure
# Usage: ./deploy.sh <environment> [--what-if]
#
# Two-phase deployment:
#   1. Shared infrastructure (ACR) → shadowbrook-shared-rg
#   2. Environment infrastructure  → shadowbrook-{env}-rg
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

SHARED_RESOURCE_GROUP="shadowbrook-shared-rg"
RESOURCE_GROUP="shadowbrook-${ENVIRONMENT}-rg"
LOCATION="eastus"

# Step 1: Deploy shared infrastructure (ACR)
echo "==> Deploying shared infrastructure to $SHARED_RESOURCE_GROUP..."
az group create --name "$SHARED_RESOURCE_GROUP" --location "$LOCATION" --output none

az deployment group create \
  --name "shadowbrook-shared" \
  --resource-group "$SHARED_RESOURCE_GROUP" \
  --parameters "$BICEP_DIR/parameters.shared.bicepparam" \
  $WHAT_IF_FLAG

# Step 2: Deploy environment infrastructure
echo "==> Deploying $ENVIRONMENT environment to $RESOURCE_GROUP..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

az deployment group create \
  --name "shadowbrook-${ENVIRONMENT}" \
  --resource-group "$RESOURCE_GROUP" \
  --parameters "$BICEP_DIR/parameters.${ENVIRONMENT}.bicepparam" \
  $WHAT_IF_FLAG

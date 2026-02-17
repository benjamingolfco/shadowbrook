#!/usr/bin/env bash
set -euo pipefail

# Reset Shadowbrook database
# Drops the Azure SQL database so the container app recreates it via EF migrations on next startup.
#
# Usage: ./reset-db.sh [environment]
#   environment: dev (default), staging, prod

ENVIRONMENT="${1:-dev}"
SQL_SERVER="shadowbrook-sql-${ENVIRONMENT}"
DATABASE="shadowbrook-db-${ENVIRONMENT}"
RESOURCE_GROUP="shadowbrook-${ENVIRONMENT}-rg"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}[WARN]${NC} This will DROP database '${DATABASE}' on server '${SQL_SERVER}' (${ENVIRONMENT} environment)."
echo -e "${YELLOW}[WARN]${NC} The container app will recreate it via EF migrations on next startup."
echo ""
read -p "Type 'yes' to confirm: " -r
echo ""

if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
  echo -e "${GREEN}[INFO]${NC} Cancelled."
  exit 0
fi

echo -e "${GREEN}[INFO]${NC} Dropping database '${DATABASE}'..."
az sql db delete \
  --name "$DATABASE" \
  --server "$SQL_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --yes

echo -e "${GREEN}[INFO]${NC} Recreating empty database '${DATABASE}'..."
az sql db create \
  --name "$DATABASE" \
  --server "$SQL_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --service-objective Basic \
  --max-size 2GB

echo -e "${GREEN}[INFO]${NC} Database reset complete. Restart the container app to apply migrations:"
echo "  az containerapp revision restart --name shadowbrook-app-${ENVIRONMENT} --resource-group ${RESOURCE_GROUP} --revision \$(az containerapp revision list --name shadowbrook-app-${ENVIRONMENT} --resource-group ${RESOURCE_GROUP} --query '[0].name' -o tsv)"

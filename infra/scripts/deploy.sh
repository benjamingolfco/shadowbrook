#!/usr/bin/env bash
set -euo pipefail

# Deploy Shadowbrook dev environment to Azure
# This script is a helper for local deployment testing
# Production deployments should use the GitHub Actions workflow

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BICEP_DIR="$REPO_ROOT/infra/bicep"

# Configuration
RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-shadowbrook-dev-rg}"
LOCATION="${AZURE_LOCATION:-eastus}"
ENVIRONMENT="${ENVIRONMENT:-dev}"
ACR_NAME="${ACR_NAME:-shadowbrookacr}"
IMAGE_TAG="${IMAGE_TAG:-latest}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
  echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
  echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
  echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
if ! command -v az &> /dev/null; then
  log_error "Azure CLI is not installed. Install from https://aka.ms/azure-cli"
  exit 1
fi

if ! az account show &> /dev/null; then
  log_error "Not logged in to Azure. Run 'az login' first."
  exit 1
fi

# Check for required secrets
if [ -z "${SQL_ADMIN_LOGIN:-}" ]; then
  log_error "SQL_ADMIN_LOGIN environment variable is required"
  exit 1
fi

if [ -z "${SQL_ADMIN_PASSWORD:-}" ]; then
  log_error "SQL_ADMIN_PASSWORD environment variable is required"
  exit 1
fi

log_info "Deploying Shadowbrook dev environment"
log_info "Resource Group: $RESOURCE_GROUP"
log_info "Location: $LOCATION"
log_info "Environment: $ENVIRONMENT"

# Create resource group
log_info "Creating resource group..."
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --output none

# Create ACR if it doesn't exist
log_info "Ensuring Azure Container Registry exists..."
if ! az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
  log_info "Creating Azure Container Registry..."
  az acr create \
    --name "$ACR_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --sku Basic \
    --admin-enabled false \
    --output none
else
  log_info "ACR already exists, skipping creation"
fi

# Build and push Docker image
log_info "Building and pushing Docker image..."
cd "$REPO_ROOT"
az acr build \
  --registry "$ACR_NAME" \
  --image "shadowbrook:$IMAGE_TAG" \
  --file Dockerfile \
  .

# Deploy Bicep template
log_info "Deploying infrastructure..."
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$BICEP_DIR/main.bicep" \
  --parameters environment="$ENVIRONMENT" \
  --parameters location="$LOCATION" \
  --parameters sqlAdminLogin="$SQL_ADMIN_LOGIN" \
  --parameters sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
  --parameters acrName="$ACR_NAME" \
  --parameters imageTag="$IMAGE_TAG" \
  --output none

# Grant ACR pull access to Container App
log_info "Granting ACR pull access to Container App..."
PRINCIPAL_ID=$(az containerapp show \
  --name "shadowbrook-app-$ENVIRONMENT" \
  --resource-group "$RESOURCE_GROUP" \
  --query identity.principalId \
  --output tsv)

ACR_ID=$(az acr show \
  --name "$ACR_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)

az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role AcrPull \
  --scope "$ACR_ID" \
  --output none

# Get deployment outputs
log_info "Retrieving deployment info..."
APP_URL=$(az containerapp show \
  --name "shadowbrook-app-$ENVIRONMENT" \
  --resource-group "$RESOURCE_GROUP" \
  --query properties.configuration.ingress.fqdn \
  --output tsv)

SQL_SERVER=$(az sql server show \
  --name "shadowbrook-sql-$ENVIRONMENT" \
  --resource-group "$RESOURCE_GROUP" \
  --query fullyQualifiedDomainName \
  --output tsv)

log_info "Deployment complete!"
echo ""
echo "App URL: https://$APP_URL"
echo "SQL Server: $SQL_SERVER"
echo "Database: shadowbrook-db-$ENVIRONMENT"
echo "Resource Group: $RESOURCE_GROUP"
echo ""
log_info "Waiting for app to be ready..."
for i in {1..30}; do
  if curl -f -s "https://$APP_URL/health" > /dev/null 2>&1; then
    log_info "App is ready and responding!"
    exit 0
  fi
  echo "Attempt $i: App not ready yet, waiting..."
  sleep 10
done

log_warn "App deployment completed but health check is not responding yet."
log_warn "Check the Container App logs in Azure Portal."

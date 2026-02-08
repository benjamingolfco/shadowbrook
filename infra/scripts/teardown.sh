#!/usr/bin/env bash
set -euo pipefail

# Teardown Shadowbrook dev environment
# Deletes the entire resource group and all resources within it

# Configuration
RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-shadowbrook-dev-rg}"

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

# Confirm deletion
log_warn "This will DELETE the resource group '$RESOURCE_GROUP' and ALL resources within it."
log_warn "This action CANNOT be undone."
echo ""
read -p "Type 'yes' to confirm deletion: " -r
echo ""

if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
  log_info "Teardown cancelled."
  exit 0
fi

# Check if resource group exists
if ! az group show --name "$RESOURCE_GROUP" &> /dev/null; then
  log_warn "Resource group '$RESOURCE_GROUP' does not exist. Nothing to delete."
  exit 0
fi

# Delete resource group
log_info "Deleting resource group '$RESOURCE_GROUP'..."
az group delete \
  --name "$RESOURCE_GROUP" \
  --yes \
  --no-wait

log_info "Deletion initiated. This will take several minutes to complete."
log_info "You can check the status in the Azure Portal or with: az group show --name $RESOURCE_GROUP"

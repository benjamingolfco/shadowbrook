#!/usr/bin/env bash
set -euo pipefail

# Teardown Shadowbrook environment
# By default, only deletes the environment resource group (preserves shared ACR).
# Use --shared to also delete the shared resource group.
#
# Usage: ./teardown.sh [--shared]

# Configuration
RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-shadowbrook-dev-rg}"
SHARED_RESOURCE_GROUP="shadowbrook-shared-rg"
DELETE_SHARED=false

if [ "${1:-}" == "--shared" ]; then
  DELETE_SHARED=true
fi

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

# Confirm environment deletion
log_warn "This will DELETE the resource group '$RESOURCE_GROUP' and ALL resources within it."
if [ "$DELETE_SHARED" = true ]; then
  log_warn "This will ALSO DELETE the shared resource group '$SHARED_RESOURCE_GROUP' (ACR)."
fi
log_warn "This action CANNOT be undone."
echo ""
read -p "Type 'yes' to confirm deletion: " -r
echo ""

if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
  log_info "Teardown cancelled."
  exit 0
fi

# Delete environment resource group
if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
  log_info "Deleting resource group '$RESOURCE_GROUP'..."
  az group delete \
    --name "$RESOURCE_GROUP" \
    --yes \
    --no-wait
  log_info "Deletion of '$RESOURCE_GROUP' initiated."
else
  log_warn "Resource group '$RESOURCE_GROUP' does not exist. Nothing to delete."
fi

# Delete shared resource group if requested
if [ "$DELETE_SHARED" = true ]; then
  if az group show --name "$SHARED_RESOURCE_GROUP" &> /dev/null; then
    log_info "Deleting shared resource group '$SHARED_RESOURCE_GROUP'..."
    az group delete \
      --name "$SHARED_RESOURCE_GROUP" \
      --yes \
      --no-wait
    log_info "Deletion of '$SHARED_RESOURCE_GROUP' initiated."
  else
    log_warn "Shared resource group '$SHARED_RESOURCE_GROUP' does not exist. Nothing to delete."
  fi
else
  log_warn "Shared resource group '$SHARED_RESOURCE_GROUP' (ACR) was NOT deleted."
  log_warn "To also delete shared resources, run: $0 --shared"
fi

log_info "Deletion may take several minutes to complete."
log_info "Check status with: az group show --name $RESOURCE_GROUP"

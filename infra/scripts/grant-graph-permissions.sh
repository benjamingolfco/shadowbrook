#!/usr/bin/env bash
set -euo pipefail

# Shadowbrook - Grant Microsoft Graph API permissions to managed identity
# Usage: ./grant-graph-permissions.sh <environment>
#
# Grants the User.Invite.All application permission to the environment's
# user-assigned managed identity. This is required for sending Entra External ID
# invitations via the Microsoft Graph API.
#
# Prerequisites:
#   - Azure CLI (az) installed and logged in
#   - Caller must be Global Admin or Privileged Role Administrator
#   - Managed identity must already exist (run deploy.sh first)
#
# This is idempotent — re-running will fail harmlessly if the assignment exists.

if [ $# -eq 0 ]; then
  echo "Usage: $0 <environment>"
  echo "  environment: test, staging, prod"
  exit 1
fi

ENVIRONMENT=$1
IDENTITY_NAME="id-shadowbrook-${ENVIRONMENT}"
RESOURCE_GROUP="shadowbrook-${ENVIRONMENT}-rg"
GRAPH_APP_ID="00000003-0000-0000-c000-000000000000"

echo "==> Granting Graph API permissions for ${IDENTITY_NAME} in ${RESOURCE_GROUP}"

# 1. Get the managed identity's principal ID
echo "  Fetching managed identity principal ID..."
MI_PRINCIPAL_ID=$(az identity show \
  --name "$IDENTITY_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query principalId -o tsv)

if [ -z "$MI_PRINCIPAL_ID" ]; then
  echo "ERROR: Could not find managed identity ${IDENTITY_NAME} in ${RESOURCE_GROUP}"
  echo "       Run deploy.sh first to create the infrastructure."
  exit 1
fi
echo "  Principal ID: ${MI_PRINCIPAL_ID}"

# 2. Get the Microsoft Graph service principal object ID
echo "  Fetching Microsoft Graph service principal..."
GRAPH_SP_ID=$(az ad sp show --id "$GRAPH_APP_ID" --query id -o tsv)
echo "  Graph SP ID: ${GRAPH_SP_ID}"

# 3. Get the User.Invite.All app role ID
echo "  Looking up User.Invite.All app role..."
ROLE_ID=$(az ad sp show --id "$GRAPH_APP_ID" \
  --query "appRoles[?value=='User.Invite.All'].id" -o tsv)

if [ -z "$ROLE_ID" ]; then
  echo "ERROR: Could not find User.Invite.All app role on Microsoft Graph service principal"
  exit 1
fi
echo "  App Role ID: ${ROLE_ID}"

# 4. Grant the app role assignment
echo "  Assigning User.Invite.All to ${IDENTITY_NAME}..."
az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/${GRAPH_SP_ID}/appRoleAssignments" \
  --body "{
    \"principalId\": \"${MI_PRINCIPAL_ID}\",
    \"resourceId\": \"${GRAPH_SP_ID}\",
    \"appRoleId\": \"${ROLE_ID}\"
  }" \
  --output none

echo ""
echo "==> Done. User.Invite.All granted to ${IDENTITY_NAME}."
echo "    Verify in Entra ID > Enterprise applications > ${IDENTITY_NAME} > Permissions"

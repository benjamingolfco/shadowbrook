#!/usr/bin/env bash
set -euo pipefail

# Shadowbrook - Set up GitHub Actions OIDC authentication
# Usage: ./setup-github-oidc.sh
#
# Creates (or updates) an Entra app registration with federated credentials
# for GitHub Actions OIDC, creates the service principal, grants RBAC, and
# sets the required GitHub repo secrets.
#
# What it does:
#   1. Creates app registration "shadowbrook-github-actions" (idempotent)
#   2. Creates service principal for the app (idempotent)
#   3. Adds federated credentials for main branch, PRs, and environment (idempotent)
#   4. Grants Contributor on shared + test resource groups (idempotent)
#   5. Grants AcrPush on the container registry (idempotent)
#   6. Sets GitHub repo secrets: AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID
#
# Prerequisites:
#   - Azure CLI (az) installed and logged in as Owner/User Access Admin
#   - GitHub CLI (gh) installed and authenticated
#   - Infrastructure deployed (resource groups + ACR must exist)
#
# Idempotent — safe to re-run after subscription transfer or credential rotation.

REPO="benjamingolfco/shadowbrook"
APP_NAME="shadowbrook-github-actions"
SUBSCRIPTION_ID="37109c89-82e6-4907-8cd1-ca80800d0730"
TENANT_ID=$(az account show --query tenantId -o tsv)

echo "==> Setting up GitHub Actions OIDC for ${REPO}"
echo "    Tenant:       ${TENANT_ID}"
echo "    Subscription: ${SUBSCRIPTION_ID}"

# 1. Create or find the app registration
echo ""
echo "==> Step 1: App registration"
APP_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv 2>/dev/null)

if [ -z "$APP_ID" ]; then
  echo "  Creating app registration: ${APP_NAME}..."
  APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
  echo "  Created. App (client) ID: ${APP_ID}"
else
  echo "  Already exists. App (client) ID: ${APP_ID}"
fi

# 2. Create or find the service principal
echo ""
echo "==> Step 2: Service principal"
SP_ID=$(az ad sp list --filter "appId eq '${APP_ID}'" --query "[0].id" -o tsv 2>/dev/null)

if [ -z "$SP_ID" ]; then
  echo "  Creating service principal..."
  SP_ID=$(az ad sp create --id "$APP_ID" --query id -o tsv)
  echo "  Created. Object ID: ${SP_ID}"
else
  echo "  Already exists. Object ID: ${SP_ID}"
fi

# 3. Add federated credentials (one per scenario)
echo ""
echo "==> Step 3: Federated credentials"

add_federated_credential() {
  local name=$1
  local subject=$2
  local description=$3

  # Check if credential already exists
  EXISTING=$(az ad app federated-credential list --id "$APP_ID" --query "[?name=='${name}'].name" -o tsv 2>/dev/null)

  if [ -n "$EXISTING" ]; then
    echo "  [${name}] Already exists, skipping"
  else
    echo "  [${name}] Creating..."
    az ad app federated-credential create --id "$APP_ID" --parameters "{
      \"name\": \"${name}\",
      \"issuer\": \"https://token.actions.githubusercontent.com\",
      \"subject\": \"${subject}\",
      \"audiences\": [\"api://AzureADTokenExchange\"],
      \"description\": \"${description}\"
    }" --output none
    echo "  [${name}] Created"
  fi
}

add_federated_credential \
  "github-main" \
  "repo:${REPO}:ref:refs/heads/main" \
  "GitHub Actions - main branch"

add_federated_credential \
  "github-pull-request" \
  "repo:${REPO}:pull_request" \
  "GitHub Actions - pull requests"

add_federated_credential \
  "github-env-test" \
  "repo:${REPO}:environment:test" \
  "GitHub Actions - test environment"

# 4. Grant RBAC on resource groups
echo ""
echo "==> Step 4: RBAC role assignments"

grant_role() {
  local role=$1
  local scope=$2
  local label=$3

  # Check if assignment already exists
  EXISTING=$(az role assignment list \
    --assignee "$SP_ID" \
    --role "$role" \
    --scope "$scope" \
    --query "[0].id" -o tsv 2>/dev/null)

  if [ -n "$EXISTING" ]; then
    echo "  [${label}] Already assigned, skipping"
  else
    echo "  [${label}] Assigning ${role}..."
    az role assignment create \
      --assignee-object-id "$SP_ID" \
      --assignee-principal-type ServicePrincipal \
      --role "$role" \
      --scope "$scope" \
      --output none
    echo "  [${label}] Assigned"
  fi
}

SHARED_RG="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/shadowbrook-shared-rg"
TEST_RG="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/shadowbrook-test-rg"
ACR_ID="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/shadowbrook-shared-rg/providers/Microsoft.ContainerRegistry/registries/shadowbrookacr"

grant_role "Contributor" "$SHARED_RG" "Contributor on shared-rg"
grant_role "Contributor" "$TEST_RG" "Contributor on test-rg"
grant_role "AcrPush" "$ACR_ID" "AcrPush on ACR"

# 5. Set GitHub repo secrets
echo ""
echo "==> Step 5: GitHub repo secrets"

echo "  Setting AZURE_CLIENT_ID..."
gh secret set AZURE_CLIENT_ID --repo "$REPO" --body "$APP_ID"

echo "  Setting AZURE_TENANT_ID..."
gh secret set AZURE_TENANT_ID --repo "$REPO" --body "$TENANT_ID"

echo "  Setting AZURE_SUBSCRIPTION_ID..."
gh secret set AZURE_SUBSCRIPTION_ID --repo "$REPO" --body "$SUBSCRIPTION_ID"

echo ""
echo "==> Done. GitHub Actions OIDC configured."
echo "    App registration: ${APP_NAME}"
echo "    Client ID:        ${APP_ID}"
echo "    Tenant ID:        ${TENANT_ID}"
echo "    Subscription ID:  ${SUBSCRIPTION_ID}"
echo ""
echo "    RBAC:"
echo "      Contributor on shadowbrook-shared-rg"
echo "      Contributor on shadowbrook-test-rg"
echo "      AcrPush on shadowbrookacr"
echo ""
echo "    To add more environments, add resource groups to this script."

#!/usr/bin/env bash
set -euo pipefail

# Teeforce - Set up Entra ID app registrations for API and SPA
# Usage: ./setup-entra-apps.sh <environment> <spa-redirect-uri>
# Example: ./setup-entra-apps.sh test https://purple-field-0a3932a0f.4.azurestaticapps.net
#
# What it does:
#   1. Creates (or finds) API app registration "teeforce-api-{env}"
#      - signInAudience: AzureADMyOrg
#      - Exposes access_as_user delegated scope (stable GUID)
#      - Sets identifierUris to api://{apiAppId}
#      - Requires Microsoft Graph User.Read
#   2. Creates (or finds) SPA app registration "teeforce-spa-{env}"
#      - signInAudience: AzureADMyOrg
#      - SPA redirect URIs: <spa-redirect-uri> + http://localhost:3000
#      - Requires API access_as_user + Microsoft Graph User.Read
#   3. Pre-authorizes the SPA on the API app
#   4. Prints config values for appsettings.json and .env files
#
# Prerequisites:
#   - Azure CLI (az) installed and logged in with permission to create app registrations
#
# Idempotent — safe to re-run. Existing apps are found by display name and updated in place.

# ---------------------------------------------------------------------------
# Args
# ---------------------------------------------------------------------------

if [ $# -ne 2 ]; then
  echo "Usage: $0 <environment> <spa-redirect-uri>"
  echo "Example: $0 test https://purple-field-0a3932a0f.4.azurestaticapps.net"
  exit 1
fi

ENV="$1"
SPA_REDIRECT_URI="$2"

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

TENANT_ID="f74e8993-5f31-49cb-8772-a20a7f0cf2b6"

API_APP_NAME="teeforce-api-${ENV}"
SPA_APP_NAME="teeforce-spa-${ENV}"

# Stable scope ID for access_as_user — same GUID across all environments so
# re-runs and environment comparisons remain deterministic.
ACCESS_AS_USER_SCOPE_ID="a1b2c3d4-0001-4000-8000-100000000001"

# Microsoft Graph resource and User.Read scope
GRAPH_APP_ID="00000003-0000-0000-c000-000000000000"
GRAPH_USER_READ_ID="e1fe6dd8-ba31-4d61-89e7-88639da4683d"

echo "==> Setting up Entra ID app registrations"
echo "    Environment:      ${ENV}"
echo "    Tenant ID:        ${TENANT_ID}"
echo "    SPA redirect URI: ${SPA_REDIRECT_URI}"

# ---------------------------------------------------------------------------
# Step 1: API app registration
# ---------------------------------------------------------------------------

echo ""
echo "==> Step 1: API app registration (${API_APP_NAME})"

API_APP_ID=$(az ad app list --display-name "$API_APP_NAME" --query "[0].appId" -o tsv 2>/dev/null)

if [ -z "$API_APP_ID" ]; then
  echo "  Creating app registration: ${API_APP_NAME}..."
  API_APP_ID=$(az ad app create \
    --display-name "$API_APP_NAME" \
    --sign-in-audience "AzureADMyOrg" \
    --query appId -o tsv)
  echo "  Created. App (client) ID: ${API_APP_ID}"
else
  echo "  Already exists. App (client) ID: ${API_APP_ID}"
fi

API_IDENTIFIER_URI="api://${API_APP_ID}"

# Set identifierUris — safe to re-apply, az ad app update is idempotent for this
echo "  Setting identifierUris to ${API_IDENTIFIER_URI}..."
az ad app update \
  --id "$API_APP_ID" \
  --identifier-uris "$API_IDENTIFIER_URI" \
  --output none

# Expose the access_as_user scope.
# az ad app update --set cannot create the api property on a fresh app, so we
# use az rest to PATCH the application object via the Graph API directly.
echo "  Configuring access_as_user scope (ID: ${ACCESS_AS_USER_SCOPE_ID})..."
az rest --method PATCH \
  --uri "https://graph.microsoft.com/v1.0/applications(appId='${API_APP_ID}')" \
  --headers "Content-Type=application/json" \
  --body "{
    \"api\": {
      \"oauth2PermissionScopes\": [{
        \"id\": \"${ACCESS_AS_USER_SCOPE_ID}\",
        \"value\": \"access_as_user\",
        \"type\": \"User\",
        \"isEnabled\": true,
        \"adminConsentDisplayName\": \"Access Teeforce API\",
        \"adminConsentDescription\": \"Allow the application to access Teeforce API on behalf of the signed-in user\",
        \"userConsentDisplayName\": \"Access Teeforce API\",
        \"userConsentDescription\": \"Allow the application to access Teeforce API on behalf of the signed-in user\"
      }]
    }
  }" \
  --output none

# Require Microsoft Graph User.Read
echo "  Setting required resource access (Graph User.Read)..."
az ad app update \
  --id "$API_APP_ID" \
  --required-resource-accesses "[{
    \"resourceAppId\": \"${GRAPH_APP_ID}\",
    \"resourceAccess\": [{
      \"id\": \"${GRAPH_USER_READ_ID}\",
      \"type\": \"Scope\"
    }]
  }]" \
  --output none

echo "  API app registration configured."

# ---------------------------------------------------------------------------
# Step 2: SPA app registration
# ---------------------------------------------------------------------------

echo ""
echo "==> Step 2: SPA app registration (${SPA_APP_NAME})"

SPA_APP_ID=$(az ad app list --display-name "$SPA_APP_NAME" --query "[0].appId" -o tsv 2>/dev/null)

if [ -z "$SPA_APP_ID" ]; then
  echo "  Creating app registration: ${SPA_APP_NAME}..."
  SPA_APP_ID=$(az ad app create \
    --display-name "$SPA_APP_NAME" \
    --sign-in-audience "AzureADMyOrg" \
    --query appId -o tsv)
  echo "  Created. App (client) ID: ${SPA_APP_ID}"
else
  echo "  Already exists. App (client) ID: ${SPA_APP_ID}"
fi

# Set SPA redirect URIs (spa.redirectUris — the MSAL SPA registration type,
# distinct from web.redirectUris; enables auth code + PKCE without a client secret).
# Uses az rest since --set cannot create the spa property on a fresh app.
echo "  Setting SPA redirect URIs..."
az rest --method PATCH \
  --uri "https://graph.microsoft.com/v1.0/applications(appId='${SPA_APP_ID}')" \
  --headers "Content-Type=application/json" \
  --body "{
    \"spa\": {
      \"redirectUris\": [\"${SPA_REDIRECT_URI}\", \"http://localhost:3000\"]
    }
  }" \
  --output none

# Require both the API's access_as_user scope and Microsoft Graph User.Read
echo "  Setting required resource access (API scope + Graph User.Read)..."
az ad app update \
  --id "$SPA_APP_ID" \
  --required-resource-accesses "[
    {
      \"resourceAppId\": \"${API_APP_ID}\",
      \"resourceAccess\": [{
        \"id\": \"${ACCESS_AS_USER_SCOPE_ID}\",
        \"type\": \"Scope\"
      }]
    },
    {
      \"resourceAppId\": \"${GRAPH_APP_ID}\",
      \"resourceAccess\": [{
        \"id\": \"${GRAPH_USER_READ_ID}\",
        \"type\": \"Scope\"
      }]
    }
  ]" \
  --output none

echo "  SPA app registration configured."

# ---------------------------------------------------------------------------
# Step 3: Pre-authorize the SPA on the API
# ---------------------------------------------------------------------------

echo ""
echo "==> Step 3: Pre-authorizing SPA on API"

# Set preAuthorizedApplications so the SPA is pre-consented for access_as_user.
# This replaces the full list — safe to re-run since we always write the
# complete intended state. Uses az rest since --set may fail if the api
# property was just created.
echo "  Setting preAuthorizedApplications on API app..."
az rest --method PATCH \
  --uri "https://graph.microsoft.com/v1.0/applications(appId='${API_APP_ID}')" \
  --headers "Content-Type=application/json" \
  --body "{
    \"api\": {
      \"preAuthorizedApplications\": [{
        \"appId\": \"${SPA_APP_ID}\",
        \"delegatedPermissionIds\": [\"${ACCESS_AS_USER_SCOPE_ID}\"]
      }]
    }
  }" \
  --output none

echo "  SPA (${SPA_APP_ID}) pre-authorized for access_as_user."

# ---------------------------------------------------------------------------
# Step 4: Summary
# ---------------------------------------------------------------------------

API_SCOPE_URI="${API_IDENTIFIER_URI}/access_as_user"

echo ""
echo "==> Done. Entra ID app registrations configured for '${ENV}'."
echo ""
echo "    API app:  ${API_APP_NAME}"
echo "    App ID:   ${API_APP_ID}"
echo ""
echo "    SPA app:  ${SPA_APP_NAME}"
echo "    App ID:   ${SPA_APP_ID}"
echo ""
echo "    API scope URI: ${API_SCOPE_URI}"
echo ""
echo "    Config values to update:"
echo ""
echo "    appsettings.json (or appsettings.Test.json) — AzureAd section:"
echo "      ClientId:  ${API_APP_ID}"
echo "      Audience:  ${API_IDENTIFIER_URI}"
echo "      TenantId:  ${TENANT_ID}"
echo ""
echo "    .env (or Azure Static Web App environment variables) — SPA:"
echo "      VITE_ENTRA_CLIENT_ID=${SPA_APP_ID}"
echo "      VITE_ENTRA_AUTHORITY=https://login.microsoftonline.com/${TENANT_ID}"
echo "      VITE_API_SCOPE=${API_SCOPE_URI}"

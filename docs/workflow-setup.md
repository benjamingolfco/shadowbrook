# GitHub Actions Workflow Setup

Steps to enable CI/CD workflows for Shadowbrook.

## Prerequisites

- Azure CLI installed and authenticated (`az login`)
- GitHub CLI installed and authenticated (`gh auth login`)
- Dev infrastructure already deployed (`./infra/scripts/deploy.sh dev`)

## 1. Register a Microsoft Entra ID Application (OIDC)

GitHub Actions authenticates to Azure via OIDC — no stored credentials.

```bash
# Create the app registration
az ad app create --display-name "shadowbrook-github-actions"

# Note the appId from the output — this is your AZURE_CLIENT_ID
APP_ID=<appId from output>

# Create a service principal
az ad sp create --id $APP_ID

# Get your tenant and subscription IDs
az account show --query '{tenantId:tenantId, subscriptionId:id}' --output table
```

## 2. Add Federated Identity Credentials

One credential per trigger type (PR, push to main, manual dispatch).

```bash
# For pull requests
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "github-pr",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:benjamingolfco/shadowbrook:pull_request",
  "audiences": ["api://AzureADTokenExchange"]
}'

# For push to main
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:benjamingolfco/shadowbrook:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

# For manual workflow dispatch
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "github-dispatch",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:benjamingolfco/shadowbrook:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

## 3. Grant Azure RBAC Permissions

The service principal needs Contributor on the resource groups and AcrPush on the registry.

```bash
SUBSCRIPTION_ID=$(az account show --query id --output tsv)

# Contributor on dev resource group
az role assignment create \
  --assignee $APP_ID \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/shadowbrook-dev-rg

# Contributor on shared resource group (for Bicep infra deployments)
az role assignment create \
  --assignee $APP_ID \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/shadowbrook-shared-rg

# AcrPush on the container registry (for building/pushing images)
ACR_ID=$(az acr show --name shadowbrookacr --query id --output tsv)
az role assignment create \
  --assignee $APP_ID \
  --role AcrPush \
  --scope $ACR_ID
```

## 4. Get SWA Deployment Token

```bash
az staticwebapp secrets list \
  --name shadowbrook-swa-dev \
  --resource-group shadowbrook-dev-rg \
  --query properties.apiKey \
  --output tsv
```

## 5. Add GitHub Secrets

Go to **Settings > Secrets and variables > Actions** in the repo, or use the CLI:

```bash
# Azure OIDC (from steps 1-2)
gh secret set AZURE_CLIENT_ID --body "<appId>"
gh secret set AZURE_TENANT_ID --body "<tenantId>"
gh secret set AZURE_SUBSCRIPTION_ID --body "<subscriptionId>"

# SQL credentials (same as used for deploy.sh)
gh secret set SQL_ADMIN_LOGIN --body "<your-sql-admin-username>"
gh secret set SQL_ADMIN_PASSWORD --body "<your-sql-admin-password>"

# SWA deployment token (from step 4)
gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN --body "<token>"
```

## 6. Copy Workflow Files

The workflow files in `.github/workflows/` must be on the default branch (main) for triggers to work. Merge the branch containing these files to main.

## Workflow Overview

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `pr.yml` | Pull request | CI + deploy API to dev + SWA preview |
| `deploy-dev.yml` | Push to main, manual | CI + deploy infra/API/web to dev |
| `deploy-infra.yml` | Called by orchestrators | Bicep infrastructure deployment |
| `deploy-api.yml` | Called by orchestrators | Docker build + Container App deploy |
| `deploy-web.yml` | Called by orchestrators | React build + SWA deploy |

### PR Flow

```
PR opened/updated
  → ci (build, test, lint — 3 parallel jobs)
  → detect-changes (which directories changed?)
  → deploy-api (if src/api changed, after CI passes)
  → deploy-web (if src/web changed, after CI + API)

PR closed
  → cleanup-web (tear down SWA preview)
```

### Main Branch Flow

```
Push to main
  → ci (build, test, lint)
  → detect-changes
  → infra (if infra/bicep changed, after CI)
  → api (if src/api changed, after CI + infra)
  → web (if src/web changed, after CI + api)
```

## Secrets Summary

| Secret | Required By | Source |
|--------|-------------|--------|
| `AZURE_CLIENT_ID` | All deploy workflows | Entra ID app registration |
| `AZURE_TENANT_ID` | All deploy workflows | `az account show` |
| `AZURE_SUBSCRIPTION_ID` | All deploy workflows | `az account show` |
| `SQL_ADMIN_LOGIN` | deploy-infra.yml | You choose |
| `SQL_ADMIN_PASSWORD` | deploy-infra.yml | You choose |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | deploy-web.yml | Azure portal or CLI |

## Troubleshooting

**OIDC login fails with "AADSTS70021"**
The federated credential subject doesn't match. Check that the credential matches the trigger type (pull_request vs ref:refs/heads/main).

**ACR build fails with 403**
The service principal needs AcrPush on the registry, not just Contributor on the resource group.

**SWA deploy fails with "No deployment token"**
The `AZURE_STATIC_WEB_APPS_API_TOKEN` secret is missing or expired. Regenerate from Azure portal.

**Health check fails after Container App update**
The container may need more time to start. Check logs:
```bash
az containerapp logs show \
  --name shadowbrook-app-dev \
  --resource-group shadowbrook-dev-rg \
  --follow
```

# Key Vault + Telnyx Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Azure Key Vault to the infrastructure and configure the Container App to read Telnyx SMS credentials from it via native Key Vault secret references.

**Architecture:** A new Key Vault Bicep module creates the vault and grants the existing managed identity `Key Vault Secrets User` access. The Container App module gains two new Key Vault-backed secrets (`telnyx-api-key`, `telnyx-from-number`) exposed as environment variables that .NET binds to `TelnyxOptions`. Secrets are set manually in Key Vault after infra deploys.

**Tech Stack:** Azure Bicep, Azure Key Vault (`2024-11-01`), Azure Container Apps (`2025-01-01`), Azure RBAC (`2022-04-01`)

**Worktree:** `.worktrees/issue/notification-service` (branch: `issue/notification-service`)

---

### Task 1: Create Key Vault Bicep Module

**Files:**
- Create: `infra/bicep/modules/key-vault.bicep`

This module creates the Key Vault and grants the managed identity read access to secrets. It follows the same pattern as `acr-role-assignment.bicep` for the role assignment.

- [ ] **Step 1: Create the Key Vault module**

```bicep
// Azure Key Vault Bicep module
// Creates a Key Vault with RBAC authorization and grants the Container App's
// managed identity the Key Vault Secrets User role for reading secrets at runtime.

@description('Environment name (test, staging, prod)')
param environment string

@description('Azure region for resources')
param location string

@description('Principal ID of the managed identity to grant Key Vault Secrets User access')
param managedIdentityPrincipalId string

var keyVaultName = 'kv-teeforce-${environment}'

// Key Vault with RBAC authorization (not access policies)
resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: keyVaultName
  location: location
  tags: {
    environment: environment
    workload: 'teeforce'
  }
  properties: {
    tenantId: tenant().tenantId
    sku: {
      name: 'standard'
      family: 'A'
    }
    enableRbacAuthorization: true
  }
}

// Built-in Key Vault Secrets User role definition
// See: https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#key-vault-secrets-user
resource kvSecretsUserRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '4633458b-17de-408a-b874-0445c86b69e6'
}

// Key Vault Secrets User role assignment — allows managed identity to read secrets
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, managedIdentityPrincipalId, kvSecretsUserRoleDefinition.id)
  scope: keyVault
  properties: {
    principalId: managedIdentityPrincipalId
    roleDefinitionId: kvSecretsUserRoleDefinition.id
    principalType: 'ServicePrincipal'
  }
}

@description('The name of the Key Vault')
output name string = keyVault.name

@description('The URI of the Key Vault')
output vaultUri string = keyVault.properties.vaultUri
```

- [ ] **Step 2: Verify Bicep compiles**

Run: `az bicep build --file infra/bicep/modules/key-vault.bicep`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add infra/bicep/modules/key-vault.bicep
git commit -m "feat: add Key Vault Bicep module with RBAC and secrets user role"
```

---

### Task 2: Add Key Vault References to Container App Module

**Files:**
- Modify: `infra/bicep/modules/container-app.bicep`

Add a `keyVaultName` parameter and two Key Vault-backed secrets with corresponding environment variables.

- [ ] **Step 1: Add the keyVaultName parameter**

Add after the `corsOriginPattern` parameter (line 41):

```bicep
@description('Name of the Key Vault containing external service secrets')
param keyVaultName string
```

- [ ] **Step 2: Add Key Vault secret references to the secrets array**

Add two new entries to the `secrets` array (after line 78, the `app-insights-connection-string` entry):

```bicep
        {
          name: 'telnyx-api-key'
          keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/telnyx-api-key'
          identity: userAssignedIdentityId
        }
        {
          name: 'telnyx-from-number'
          keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/telnyx-from-number'
          identity: userAssignedIdentityId
        }
```

- [ ] **Step 3: Add Telnyx environment variables**

Add two new entries to the `env` array (after the `AzureAd__ManagedIdentityClientId` entry, line 118):

```bicep
            {
              name: 'Telnyx__ApiKey'
              secretRef: 'telnyx-api-key'
            }
            {
              name: 'Telnyx__FromNumber'
              secretRef: 'telnyx-from-number'
            }
```

- [ ] **Step 4: Verify Bicep compiles**

Run: `az bicep build --file infra/bicep/modules/container-app.bicep`
Expected: No errors (module compiles in isolation with unresolved parameters)

- [ ] **Step 5: Commit**

```bash
git add infra/bicep/modules/container-app.bicep
git commit -m "feat: add Telnyx Key Vault secret references to Container App"
```

---

### Task 3: Wire Key Vault Module into main.bicep

**Files:**
- Modify: `infra/bicep/main.bicep`

Add the Key Vault module deployment and pass its name to the Container App module.

- [ ] **Step 1: Update the deployment order comment**

Replace lines 8-17 (the deployment order comment):

```bicep
// Deployment order (with explicit dependsOn):
// 0. environmentRg    — Resource group (created first)
// 1. managedIdentity   — user-assigned identity (independent)
// 2. staticWebApp      — SWA for React frontend (independent)
// 3. logAnalytics      — Log Analytics workspace (independent)
// 4. database          — Azure SQL with Entra-only auth (depends on managedIdentity)
// 5. appInsights       — depends on logAnalytics
// 6. containerAppEnv   — depends on logAnalytics (appLogsConfiguration)
// 7. keyVault          — Key Vault for external service secrets (depends on managedIdentity)
// 8. acrRoleAssignment — depends on managedIdentity (deployed to shared RG)
// 9. containerApp      — depends on acrRoleAssignment, containerAppEnv, database, appInsights, keyVault
```

- [ ] **Step 2: Add Key Vault module deployment**

Add after the `acrRoleAssignment` module block (after line 126), before the Container App section:

```bicep
// Key Vault for external service secrets (Telnyx, future providers)
module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault-deployment'
  scope: environmentRg
  params: {
    environment: environment
    location: location
    managedIdentityPrincipalId: managedIdentity.outputs.principalId
  }
}
```

- [ ] **Step 3: Pass keyVaultName to the Container App module**

Add `keyVaultName` to the `containerApp` module's params block (after the `corsOriginPattern` param):

```bicep
    keyVaultName: keyVault.outputs.name
```

- [ ] **Step 4: Verify full Bicep compiles**

Run: `az bicep build --file infra/bicep/main.bicep`
Expected: No errors

- [ ] **Step 5: Commit**

```bash
git add infra/bicep/main.bicep
git commit -m "feat: wire Key Vault module into main.bicep and pass to Container App"
```

---

### Task 4: Update Infrastructure Documentation

**Files:**
- Modify: `infra/README.md`

- [ ] **Step 1: Add Key Vault to the Per-Environment Resources list**

Add after the Application Insights bullet (around line 20):

```markdown
- **Azure Key Vault**: `kv-teeforce-{env}` — stores external service secrets (Telnyx API key, etc.)
```

- [ ] **Step 2: Add Key Vault to the naming convention examples**

Add to the naming examples list (around line 35):

```markdown
- `kv-teeforce-test` - Key Vault
```

- [ ] **Step 3: Update the deployment order list**

Replace the Phase 2 deployment order list (lines 50-58) to include Key Vault:

```markdown
1. **Resource group** — `teeforce-{env}-rg`
2. **managedIdentity** — User-assigned identity (independent)
3. **staticWebApp** — SWA for React frontend (independent)
4. **logAnalytics** — Log Analytics workspace (independent)
5. **database** — Azure SQL with Entra-only auth (depends on managedIdentity)
6. **appInsights** — Application Insights (depends on logAnalytics)
7. **containerAppEnv** — Container Apps Environment (depends on logAnalytics)
8. **keyVault** — Key Vault for external service secrets (depends on managedIdentity)
9. **acrRoleAssignment** — AcrPull role (depends on managedIdentity, deployed to shared RG)
10. **containerApp** — Container App (depends on acrRoleAssignment, containerAppEnv, database, appInsights, keyVault)
```

- [ ] **Step 4: Add Key Vault first-time setup section**

Add a new section after the "Local Deployment" section (after line 99):

```markdown
### Key Vault Secrets (First-Time Setup)

After deploying infrastructure to a new environment for the first time, seed the Key Vault secrets that the Container App references. The Container App validates Key Vault references at deploy time, so secrets must exist before the Container App can start.

```bash
# Seed Telnyx secrets (replace with real values when available)
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-api-key --value placeholder
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-from-number --value placeholder
```

After initial setup, update secrets with real values:

```bash
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-api-key --value <YOUR_API_KEY>
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-from-number --value <YOUR_FROM_NUMBER>
```

Subsequent infra deploys do not require manual intervention — secrets persist in Key Vault.
```

- [ ] **Step 5: Add Key Vault to the Bicep Structure tree**

Add to the modules list in the Bicep Structure section (around line 140):

```markdown
    ├── key-vault.bicep               # Azure Key Vault + Secrets User role assignment
```

- [ ] **Step 6: Commit**

```bash
git add infra/README.md
git commit -m "docs: add Key Vault to infrastructure documentation"
```

---

### Task 5: Deploy and Verify

This task is manual — the engineer deploys to the test environment and verifies the Key Vault integration works end-to-end.

- [ ] **Step 1: Deploy infrastructure**

Run the infra deployment to create the Key Vault:

```bash
# Via GitHub Actions: trigger "Deploy Infrastructure" workflow for test environment
# Or locally:
./infra/scripts/deploy.sh test
```

If the Container App deployment fails because Key Vault secrets don't exist yet (expected on first deploy), proceed to step 2.

- [ ] **Step 2: Seed placeholder secrets**

```bash
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-api-key --value placeholder
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-from-number --value placeholder
```

- [ ] **Step 3: Re-deploy infrastructure**

Re-run the infra deployment. The Container App should now resolve the Key Vault references:

```bash
./infra/scripts/deploy.sh test
```

- [ ] **Step 4: Verify Container App starts**

```bash
# Check health endpoint
curl https://teeforce-app-test.wittywave-545ed3d5.eastus2.azurecontainerapps.io/health

# Check container app env vars are set
az containerapp show \
  --name teeforce-app-test \
  --resource-group teeforce-test-rg \
  --query "properties.template.containers[0].env[?contains(name, 'Telnyx')]"
```

Expected: Health returns 200, Telnyx env vars are present with `secretRef` references.

- [ ] **Step 5: Set real Telnyx credentials**

Once you have your Telnyx API key and phone number:

```bash
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-api-key --value <YOUR_TELNYX_API_KEY>
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-from-number --value <YOUR_TELNYX_FROM_NUMBER>
```

The Container App picks up new secret values on the next revision deployment (next `deploy-api` run). To force an immediate refresh, create a new revision:

```bash
az containerapp revision restart \
  --name teeforce-app-test \
  --resource-group teeforce-test-rg \
  --revision $(az containerapp revision list --name teeforce-app-test --resource-group teeforce-test-rg --query "[0].name" -o tsv)
```

- [ ] **Step 6: Send a test SMS**

Trigger a flow that sends an SMS (e.g., create a booking) and verify the message is delivered via Telnyx.

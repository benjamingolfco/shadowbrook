# Shadowbrook Infrastructure

Azure infrastructure for the Shadowbrook tee time booking platform. **Bicep is the single source of truth** for all Azure resources.

## Architecture

### Shared Resources (`shadowbrook-shared-rg`)

Resources deployed once and reused across all environments:

- **Azure Container Registry**: `shadowbrookacr` — stores container images promoted across environments

### Per-Environment Resources (`shadowbrook-{env}-rg`)

- **Azure Container Apps**: Hosts .NET API container
- **Azure Static Web Apps**: Hosts React SPA frontend (Free tier)
- **Azure SQL Database**: Basic tier (5 DTU) for development data
- **User-Assigned Managed Identity**: ACR pull access for the Container App

**Estimated monthly cost:** ~$10-15 (dev environment)

### Resource Naming Convention

`shadowbrook-{resource}-{environment}`

Examples:
- `shadowbrookacr` - Container Registry (shared)
- `shadowbrook-app-dev` - Container App (dev)
- `shadowbrook-sql-dev` - SQL Server (dev)
- `shadowbrook-db-dev` - SQL Database (dev)
- `id-shadowbrook-dev` - Managed Identity (dev)

### Deployment Order

Both phases use subscription-level deployments (`az deployment sub create`). Bicep creates the resource groups — they are not created separately by scripts.

**Phase 1 — Shared infrastructure:**
1. **Resource group** — `shadowbrook-shared-rg`
2. **registry** — Azure Container Registry

**Phase 2 — Environment infrastructure:**
1. **Resource group** — `shadowbrook-{env}-rg`
2. **database** — Azure SQL (independent)
3. **staticWebApp** — SWA for React frontend (independent)
4. **managedIdentity** — User-assigned identity (independent)
5. **acrRoleAssignment** — AcrPull role (depends on identity, deployed to shared RG)
6. **containerApp** — App + environment (depends on role assignment + database)

The environment deployment references the shared ACR cross-resource-group via Bicep's
`existing` resource + `scope: resourceGroup(...)` pattern.

## Deployment

### Prerequisites

1. **Azure CLI**: Install from https://aka.ms/azure-cli
2. **Azure Subscription**: Active Azure subscription
3. **Permissions**: Contributor + User Access Administrator role on the subscription
4. **Secrets**: SQL admin credentials

### GitHub Actions (Recommended)

Deploy via GitHub Actions workflow:

1. Configure GitHub Secrets:
   ```
   AZURE_CLIENT_ID - Azure service principal client ID
   AZURE_TENANT_ID - Azure tenant ID
   AZURE_SUBSCRIPTION_ID - Azure subscription ID
   SQL_ADMIN_LOGIN - SQL Server admin username
   SQL_ADMIN_PASSWORD - SQL Server admin password
   ```

2. Trigger deployment:
   - Go to Actions -> Deploy to Dev Environment
   - Click "Run workflow"
   - Optionally specify an image tag
   - Click "Run workflow"

### Local Deployment

Deploy from your local machine:

```bash
# Set required environment variables
export SQL_ADMIN_LOGIN="sqladmin"
export SQL_ADMIN_PASSWORD="YourSecurePassword123!"

# Login to Azure
az login

# Run deployment script (deploys shared + environment)
./infra/scripts/deploy.sh dev
```

#### What-if Mode

Preview changes without deploying:

```bash
./infra/scripts/deploy.sh dev --what-if
```

## Teardown

To delete the dev environment and stop incurring costs:

```bash
# Delete environment only (preserves shared ACR)
./infra/scripts/teardown.sh

# Delete environment AND shared resources (ACR)
./infra/scripts/teardown.sh --shared
```

By default, teardown only deletes the environment resource group (`shadowbrook-dev-rg`).
The shared resource group (`shadowbrook-shared-rg`) with ACR is preserved since it's
used across environments. Use the `--shared` flag to explicitly delete shared resources too.

**Note:** Deletion is asynchronous and takes several minutes to complete.

## Infrastructure as Code

### Bicep Structure

```
infra/bicep/
├── main.bicep                        # Environment orchestration (subscription-scoped)
├── shared.bicep                      # Shared infrastructure (subscription-scoped)
├── parameters.dev.bicepparam         # Dev environment parameters
├── parameters.shared.bicepparam      # Shared infrastructure parameters
└── modules/                          # Resource modules
    ├── database.bicep                # Azure SQL Server and Database
    ├── registry.bicep                # Azure Container Registry
    ├── managed-identity.bicep        # User-assigned managed identity
    ├── acr-role-assignment.bicep     # AcrPull role assignment
    ├── container-app.bicep           # Container Apps Environment and App
    └── static-web-app.bicep          # Azure Static Web Apps (React frontend)
```

Parameter files use `.bicepparam` format with `readEnvironmentVariable()` for secrets — no credentials are stored in source control.

### Modifying Infrastructure

1. Edit the Bicep files in `infra/bicep/modules/`
2. Verify compilation:
   ```bash
   az bicep build --file infra/bicep/shared.bicep
   az bicep build --file infra/bicep/main.bicep
   ```
3. Preview changes:
   ```bash
   ./infra/scripts/deploy.sh dev --what-if
   ```
4. Commit and deploy via GitHub Actions

## Troubleshooting

### Container App not starting

Check logs in Azure Portal:
1. Navigate to Container Apps -> shadowbrook-app-dev
2. Click "Log stream" or "Logs"
3. Look for startup errors

### Database connection errors

Verify connection string:
```bash
az containerapp show \
  --name shadowbrook-app-dev \
  --resource-group shadowbrook-dev-rg \
  --query properties.template.containers[0].env
```

### ACR pull authentication errors

The user-assigned managed identity should have AcrPull access automatically
via Bicep. The role assignment is deployed to the shared resource group where
ACR lives. If issues persist, verify the role assignment:

```bash
az role assignment list \
  --scope $(az acr show --name shadowbrookacr --resource-group shadowbrook-shared-rg --query id -o tsv) \
  --role AcrPull \
  --output table
```

## Cost Management

### Scale to Zero

The Container App is configured to scale to 0 replicas when idle, reducing compute costs during periods of inactivity.

### Manual Shutdown

To completely stop the environment without deleting resources:

```bash
az containerapp update \
  --name shadowbrook-app-dev \
  --resource-group shadowbrook-dev-rg \
  --min-replicas 0 \
  --max-replicas 0
```

To restart:

```bash
az containerapp update \
  --name shadowbrook-app-dev \
  --resource-group shadowbrook-dev-rg \
  --min-replicas 0 \
  --max-replicas 1
```

## Monitoring

View logs and metrics:

```bash
# Stream live logs
az containerapp logs show \
  --name shadowbrook-app-dev \
  --resource-group shadowbrook-dev-rg \
  --follow
```

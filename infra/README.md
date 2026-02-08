# Shadowbrook Infrastructure

Azure infrastructure for the Shadowbrook tee time booking platform.

## Architecture

### Dev Environment

- **Azure Container Apps**: Hosts .NET API + React frontend in a single container
- **Azure SQL Database**: Basic tier (5 DTU) for development data
- **Azure Container Registry**: Stores container images
- **Log Analytics**: Container app logs and monitoring

**Estimated monthly cost:** ~$10-15

### Resource Naming Convention

`shadowbrook-{resource}-{environment}`

Examples:
- `shadowbrook-app-dev` - Container App (dev)
- `shadowbrook-sql-dev` - SQL Server (dev)
- `shadowbrook-db-dev` - SQL Database (dev)

## Deployment

### Prerequisites

1. **Azure CLI**: Install from https://aka.ms/azure-cli
2. **Azure Subscription**: Active Azure subscription
3. **Permissions**: Contributor role on the subscription
4. **Secrets**: SQL admin credentials

### GitHub Actions (Recommended)

Deploy via GitHub Actions workflow:

1. **Enable the workflow** (one-time setup by repository owner):
   ```bash
   # Copy the workflow file to .github/workflows/
   cp infra/deploy-dev-workflow.yml .github/workflows/deploy-dev.yml
   git add .github/workflows/deploy-dev.yml
   git commit -m "chore: add dev environment deployment workflow"
   git push
   ```

2. Configure GitHub Secrets:
   ```
   AZURE_CLIENT_ID - Azure service principal client ID
   AZURE_TENANT_ID - Azure tenant ID
   AZURE_SUBSCRIPTION_ID - Azure subscription ID
   SQL_ADMIN_LOGIN - SQL Server admin username
   SQL_ADMIN_PASSWORD - SQL Server admin password
   ```

3. Trigger deployment:
   - Go to Actions → Deploy to Dev Environment
   - Click "Run workflow"
   - Select branch and options
   - Click "Run workflow"

### Local Deployment

Deploy from your local machine:

```bash
# Set required environment variables
export SQL_ADMIN_LOGIN="sqladmin"
export SQL_ADMIN_PASSWORD="YourSecurePassword123!"
export AZURE_RESOURCE_GROUP="shadowbrook-dev-rg"
export AZURE_LOCATION="eastus"

# Login to Azure
az login

# Run deployment script
./infra/scripts/deploy.sh
```

## Teardown

To delete the dev environment and stop incurring costs:

```bash
# Delete all resources
./infra/scripts/teardown.sh
```

This will:
- Prompt for confirmation
- Delete the entire resource group
- Remove all resources (Container App, SQL Database, ACR, etc.)

**Note:** Deletion is asynchronous and takes several minutes to complete.

## Infrastructure as Code

### Bicep Modules

- `main.bicep` - Main orchestration template
- `database.bicep` - Azure SQL Server and Database
- `container-app.bicep` - Container Apps Environment and App
- `parameters.dev.json` - Dev environment parameters

### Modifying Infrastructure

1. Edit the Bicep files in `infra/bicep/`
2. Test locally with Azure CLI:
   ```bash
   az deployment group create \
     --resource-group shadowbrook-dev-rg \
     --template-file infra/bicep/main.bicep \
     --parameters @infra/bicep/parameters.dev.json
   ```
3. Commit changes and deploy via GitHub Actions

## Troubleshooting

### Container App not starting

Check logs in Azure Portal:
1. Navigate to Container Apps → shadowbrook-app-dev
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

Grant ACR pull access manually:
```bash
PRINCIPAL_ID=$(az containerapp show \
  --name shadowbrook-app-dev \
  --resource-group shadowbrook-dev-rg \
  --query identity.principalId \
  --output tsv)

ACR_ID=$(az acr show \
  --name shadowbrookacr \
  --resource-group shadowbrook-dev-rg \
  --query id \
  --output tsv)

az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role AcrPull \
  --scope $ACR_ID
```

## Cost Management

### Scale to Zero

The Container App is configured to scale to 0 replicas when idle, reducing compute costs during periods of inactivity.

### Manual Shutdown

To completely stop the environment without deleting resources:

```bash
# Stop the Container App
az containerapp update \
  --name shadowbrook-app-dev \
  --resource-group shadowbrook-dev-rg \
  --min-replicas 0 \
  --max-replicas 0
```

To restart:

```bash
# Resume the Container App
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

# View metrics in Azure Portal
# Navigate to: Container Apps → shadowbrook-app-dev → Metrics
```

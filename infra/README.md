# Teeforce Infrastructure

Azure infrastructure for the Teeforce tee time booking platform. **Bicep is the single source of truth** for all Azure resources.

## Architecture

### Shared Resources (`teeforce-shared-rg`)

Resources deployed once and reused across all environments:

- **Azure Container Registry**: `teeforceacr` — stores container images promoted across environments

### Per-Environment Resources (`teeforce-{env}-rg`)

- **Azure Container Apps**: Hosts .NET API container
- **Azure Static Web Apps**: Hosts React SPA frontend (Free tier)
- **Azure SQL Database**: Basic tier (5 DTU) for development data
- **User-Assigned Managed Identity**: ACR pull access + SQL Server admin (Entra-only auth)
- **Log Analytics Workspace**: `teeforce-logs-{env}` — shared log sink for App Insights and Container Apps
- **Application Insights**: `teeforce-insights-{env}` — APM linked to Log Analytics workspace

**Estimated monthly cost:** ~$10-15 per environment

### Resource Naming Convention

`teeforce-{resource}-{environment}`

Examples (test environment):
- `teeforceacr` - Container Registry (shared)
- `teeforce-app-test` - Container App
- `teeforce-sql-test` - SQL Server
- `teeforce-db-test` - SQL Database
- `id-teeforce-test` - Managed Identity
- `teeforce-logs-test` - Log Analytics Workspace
- `teeforce-insights-test` - Application Insights

### SQL Authentication

All environments use **Entra-only authentication** — the Container App's user-assigned managed identity is set as the SQL Server AD admin at creation time, and the connection string uses `Authentication=Active Directory Managed Identity`. No SQL admin credentials are needed.

### Deployment Order

Both phases use subscription-level deployments (`az deployment sub create`). Bicep creates the resource groups — they are not created separately by scripts.

**Phase 1 — Shared infrastructure:**
1. **Resource group** — `teeforce-shared-rg`
2. **registry** — Azure Container Registry

**Phase 2 — Environment infrastructure:**
1. **Resource group** — `teeforce-{env}-rg`
2. **managedIdentity** — User-assigned identity (independent)
3. **staticWebApp** — SWA for React frontend (independent)
4. **logAnalytics** — Log Analytics workspace (independent)
5. **database** — Azure SQL with Entra-only auth (depends on managedIdentity)
6. **appInsights** — Application Insights (depends on logAnalytics)
7. **containerAppEnv** — Container Apps Environment (depends on logAnalytics)
8. **acrRoleAssignment** — AcrPull role (depends on managedIdentity, deployed to shared RG)
9. **containerApp** — Container App (depends on acrRoleAssignment, containerAppEnv, database, appInsights)

The environment deployment references the shared ACR cross-resource-group via Bicep's
`existing` resource + `scope: resourceGroup(...)` pattern.

## Deployment

### Prerequisites

1. **Azure CLI**: Install from https://aka.ms/azure-cli
2. **Azure Subscription**: Active Azure subscription
3. **Permissions**: Contributor + User Access Administrator role on the subscription
4. **Secrets**: Azure OIDC credentials (for GitHub Actions)

### GitHub Actions (Recommended)

Deploy via GitHub Actions workflow:

1. Configure GitHub Secrets:
   ```
   AZURE_CLIENT_ID - Azure service principal client ID
   AZURE_TENANT_ID - Azure tenant ID
   AZURE_SUBSCRIPTION_ID - Azure subscription ID
   ```

2. Trigger deployment:
   - Go to Actions -> Deploy to Test Environment
   - Click "Run workflow"
   - Optionally specify an image tag
   - Click "Run workflow"

### Local Deployment

Deploy from your local machine:

```bash
# Login to Azure
az login

# Run deployment script (deploys shared + environment)
./infra/scripts/deploy.sh test
```

#### What-if Mode

Preview changes without deploying:

```bash
./infra/scripts/deploy.sh test --what-if
```

## Teardown

To delete the test environment and stop incurring costs:

```bash
# Delete environment only (preserves shared ACR)
./infra/scripts/teardown.sh

# Delete environment AND shared resources (ACR)
./infra/scripts/teardown.sh --shared
```

By default, teardown only deletes the environment resource group (`teeforce-test-rg`).
The shared resource group (`teeforce-shared-rg`) with ACR is preserved since it's
used across environments. Use the `--shared` flag to explicitly delete shared resources too.

**Note:** Deletion is asynchronous and takes several minutes to complete.

## Infrastructure as Code

### Bicep Structure

```
infra/bicep/
├── main.bicep                        # Environment orchestration (subscription-scoped)
├── shared.bicep                      # Shared infrastructure (subscription-scoped)
├── parameters.test.bicepparam        # Test environment parameters
├── parameters.dev.bicepparam         # Dev environment parameters (inactive)
├── parameters.shared.bicepparam      # Shared infrastructure parameters
└── modules/                          # Resource modules
    ├── database.bicep                # Azure SQL Server and Database
    ├── registry.bicep                # Azure Container Registry
    ├── managed-identity.bicep        # User-assigned managed identity
    ├── acr-role-assignment.bicep     # AcrPull role assignment
    ├── container-app-env.bicep       # Container Apps Environment
    ├── container-app.bicep           # Container App (API)
    ├── static-web-app.bicep          # Azure Static Web Apps (React frontend)
    ├── log-analytics.bicep           # Log Analytics workspace
    └── app-insights.bicep            # Application Insights
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
   ./infra/scripts/deploy.sh test --what-if
   ```
4. Commit and deploy via GitHub Actions

## Troubleshooting

### Container App not starting

Check logs in Azure Portal:
1. Navigate to Container Apps -> teeforce-app-test
2. Click "Log stream" or "Logs"
3. Look for startup errors

### Database connection errors

Verify connection string:
```bash
az containerapp show \
  --name teeforce-app-test \
  --resource-group teeforce-test-rg \
  --query properties.template.containers[0].env
```

### ACR pull authentication errors

The user-assigned managed identity should have AcrPull access automatically
via Bicep. The role assignment is deployed to the shared resource group where
ACR lives. If issues persist, verify the role assignment:

```bash
az role assignment list \
  --scope $(az acr show --name teeforceacr --resource-group teeforce-shared-rg --query id -o tsv) \
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
  --name teeforce-app-test \
  --resource-group teeforce-test-rg \
  --min-replicas 0 \
  --max-replicas 0
```

To restart:

```bash
az containerapp update \
  --name teeforce-app-test \
  --resource-group teeforce-test-rg \
  --min-replicas 0 \
  --max-replicas 1
```

## Observability

### Stack Overview

| Concern | Solution | Where |
|---------|----------|-------|
| **Structured logging** | Serilog with `TenantId`/`MachineName`/`EnvironmentName` enrichment | App → OTel → App Insights |
| **Distributed tracing** | OpenTelemetry (ASP.NET Core, HttpClient, EF Core, Wolverine) | App Insights Transaction Search |
| **Infrastructure logs** | Container Apps built-in Log Analytics integration | Log Analytics workspace |
| **Health checks** | `/health` endpoint (DbContext connectivity check) | Container App startup/liveness/readiness probes |
| **Metrics** | OTel ASP.NET Core + HttpClient instrumentation | App Insights Metrics |

### How It Works

The app conditionally enables OpenTelemetry when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set (deployed environments only — local dev uses console logging only):

- **Tracing**: ASP.NET Core requests, outbound HTTP, EF Core queries, and Wolverine message dispatch/handling all produce linked spans
- **Metrics**: Request rates, durations, and HTTP client metrics exported to App Insights
- **Logging**: Serilog writes structured logs enriched with `TenantId` (from JWT claims), `MachineName`, and `EnvironmentName` — output goes to console (always) and via OTel to App Insights (when connected)

Wolverine traces are particularly valuable — they make event-driven flows visible as coherent traces:
```
GET /tee-times [200, 145ms]
  └── SQL: SELECT TeeTimeRequests [12ms]
  └── Wolverine.Dispatch: BookingCreated [2ms]
        └── Wolverine.Handle: BookingCreated [38ms]
              └── Wolverine.Handle: SendConfirmationSms [55ms]
```

### Key Files

| File | Purpose |
|------|---------|
| `src/backend/Teeforce.Api/Program.cs` | OTel + Serilog + health check registration |
| `src/backend/Teeforce.Api/Infrastructure/Observability/OrganizationIdEnricher.cs` | Serilog enricher that adds `OrganizationId` from JWT claims |
| `src/backend/Teeforce.Api/appsettings.json` | Serilog log level overrides (EF Core, ASP.NET Core, Wolverine) |
| `infra/bicep/modules/log-analytics.bicep` | Log Analytics workspace (0.1 GB/day cap) |
| `infra/bicep/modules/app-insights.bicep` | Application Insights linked to Log Analytics |
| `infra/bicep/modules/container-app.bicep` | Injects `APPLICATIONINSIGHTS_CONNECTION_STRING`, defines health probes |

### Viewing Logs and Traces

```bash
# Stream live container logs
az containerapp logs show \
  --name teeforce-app-test \
  --resource-group teeforce-test-rg \
  --follow

# Check health endpoint
curl https://teeforce-app-test.wittywave-545ed3d5.eastus2.azurecontainerapps.io/health
```

In the Azure Portal:
- **Application Insights → Transaction Search**: View distributed traces across HTTP requests and Wolverine message flows
- **Application Insights → Live Metrics**: Real-time request/failure rates
- **Application Insights → Failures**: Investigate exceptions with full stack traces and `TenantId` context
- **Log Analytics → Logs**: Run KQL queries across container and application logs

### Cost Control

- App Insights 5 GB/month free ingestion tier
- Log Analytics daily cap set to 0.1 GB (configurable in `log-analytics.bicep`)
- Adaptive sampling enabled by default (100% of failures, sampled successes)
- `minReplicas: 0` means zero telemetry during idle
- EF Core query logging suppressed in production (`Microsoft.EntityFrameworkCore.Database.Command: Warning`)

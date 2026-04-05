// Teeforce - Environment Infrastructure Orchestration
//
// Subscription-level deployment that creates the environment resource group
// and deploys environment-specific resources. ACR lives in the shared resource group
// (teeforce-shared-rg) and is referenced cross-RG via 'existing' resource.
// Deployed via: az deployment sub create
//
// Deployment order (with explicit dependsOn):
// 0. environmentRg    — Resource group (created first)
// 1. managedIdentity   — user-assigned identity (independent)
// 2. staticWebApp      — SWA for React frontend (independent)
// 3. logAnalytics      — Log Analytics workspace (independent)
// 4. database          — Azure SQL with Entra-only auth (depends on managedIdentity)
// 5. appInsights       — depends on logAnalytics
// 6. containerAppEnv   — depends on logAnalytics (appLogsConfiguration)
// 7. acrRoleAssignment — depends on managedIdentity (deployed to shared RG)
// 8. containerApp      — depends on acrRoleAssignment, containerAppEnv, database, appInsights

targetScope = 'subscription'

@description('Environment name (test, staging, prod)')
param environment string = 'test'

@description('Azure region for resources')
param location string = 'eastus2'

@description('Container image tag')
param imageTag string = 'latest'

@description('Name of the shared resource group containing ACR')
param sharedResourceGroup string = 'teeforce-shared-rg'

@description('Name of the shared Azure Container Registry')
param acrName string = 'teeforceacr'

@description('Name of the environment resource group')
param resourceGroupName string = 'teeforce-${environment}-rg'

// ============================================================================
// Resource Group
// ============================================================================

resource environmentRg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

// ============================================================================
// Shared resource references (cross-RG)
// ============================================================================

resource existingAcr 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: acrName
  scope: resourceGroup(sharedResourceGroup)
}

// ============================================================================
// Independent resources (deployed in parallel)
// ============================================================================

// Azure SQL Database
module database 'modules/database.bicep' = {
  name: 'database-deployment'
  scope: environmentRg
  params: {
    environment: environment
    location: location
    managedIdentityPrincipalId: managedIdentity.outputs.principalId
    managedIdentityName: 'id-teeforce-${environment}'
  }
}

// Azure Static Web App (React frontend)
module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'static-web-app-deployment'
  scope: environmentRg
  params: {
    environment: environment
    location: location
  }
}

// User-assigned managed identity for Container App
module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managed-identity-deployment'
  scope: environmentRg
  params: {
    environment: environment
    location: location
  }
}

// Log Analytics Workspace (observability sink for App Insights + Container Apps)
module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-analytics-deployment'
  scope: environmentRg
  params: {
    environment: environment
    location: location
  }
}

// Application Insights (linked to Log Analytics workspace)
module appInsights 'modules/app-insights.bicep' = {
  name: 'app-insights-deployment'
  scope: environmentRg
  params: {
    environment: environment
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
  }
}

// ============================================================================
// Role assignments (deployed after identity exists, scoped to shared RG)
// ============================================================================

// AcrPull role assignment — identity can pull from ACR before Container App exists
module acrRoleAssignment 'modules/acr-role-assignment.bicep' = {
  name: 'acr-role-assignment-deployment'
  scope: resourceGroup(sharedResourceGroup)
  params: {
    acrName: acrName
    containerAppPrincipalId: managedIdentity.outputs.principalId
  }
}

// ============================================================================
// Container Apps Environment (independent — can host multiple apps)
// ============================================================================

module containerAppEnv 'modules/container-app-env.bicep' = {
  name: 'container-app-env-deployment'
  scope: environmentRg
  params: {
    environment: environment
    location: location
    logAnalyticsWorkspaceCustomerId: logAnalytics.outputs.customerId
    logAnalyticsSharedKey: logAnalytics.outputs.sharedKey
  }
}

// ============================================================================
// Container App (deployed last — all permissions in place)
// ============================================================================

module containerApp 'modules/container-app.bicep' = {
  name: 'container-app-deployment'
  scope: environmentRg
  dependsOn: [
    acrRoleAssignment
  ]
  params: {
    environment: environment
    location: location
    containerAppEnvId: containerAppEnv.outputs.id
    containerRegistryLoginServer: existingAcr.properties.loginServer
    userAssignedIdentityId: managedIdentity.outputs.id
    imageTag: imageTag
    sqlConnectionString: 'Server=tcp:${database.outputs.sqlServerFqdn},1433;Initial Catalog=${database.outputs.databaseName};Authentication=Active Directory Managed Identity;User Id=${managedIdentity.outputs.clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
    appInsightsConnectionString: appInsights.outputs.connectionString
    frontendUrl: 'https://${staticWebApp.outputs.defaultHostname}'
    corsOrigin: 'https://${staticWebApp.outputs.defaultHostname}'
    // Match main hostname and PR staging URLs (e.g., <name>-123.<region>.<slot>.azurestaticapps.net)
    managedIdentityClientId: managedIdentity.outputs.clientId
    corsOriginPattern: '^https://${split(staticWebApp.outputs.defaultHostname, '.')[0]}(-\\d+)?\\..*\\.azurestaticapps\\.net$'
  }
}

// ============================================================================
// Outputs
// ============================================================================

output appUrl string = containerApp.outputs.appUrl
output swaDefaultHostname string = staticWebApp.outputs.defaultHostname
output swaName string = staticWebApp.outputs.name
output sqlServerFqdn string = database.outputs.sqlServerFqdn
output databaseName string = database.outputs.databaseName
output registryLoginServer string = existingAcr.properties.loginServer
output appInsightsName string = appInsights.outputs.name
output logAnalyticsName string = logAnalytics.outputs.name

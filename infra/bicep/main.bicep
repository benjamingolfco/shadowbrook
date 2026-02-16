// Shadowbrook - Environment Infrastructure Orchestration
//
// Subscription-level deployment that creates the environment resource group
// and deploys environment-specific resources. ACR lives in the shared resource group
// (shadowbrook-shared-rg) and is referenced cross-RG via 'existing' resource.
// Deployed via: az deployment sub create
//
// Deployment order (with explicit dependsOn):
// 0. environmentRg    — Resource group (created first)
// 1. database          — Azure SQL (independent)
// 2. staticWebApp      — SWA for React frontend (independent)
// 3. managedIdentity   — user-assigned identity (independent)
// 4. containerAppEnv   — Container Apps Environment (independent)
// 5. acrRoleAssignment — depends on managedIdentity (deployed to shared RG)
// 6. containerApp      — depends on acrRoleAssignment, containerAppEnv, database

targetScope = 'subscription'

@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@description('Azure region for resources')
param location string = 'eastus2'

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('Container image tag')
param imageTag string = 'latest'

@description('Name of the shared resource group containing ACR')
param sharedResourceGroup string = 'shadowbrook-shared-rg'

@description('Name of the shared Azure Container Registry')
param acrName string = 'shadowbrookacr'

@description('Name of the environment resource group')
param resourceGroupName string = 'shadowbrook-${environment}-rg'

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
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
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
    sqlConnectionString: 'Server=tcp:${database.outputs.sqlServerFqdn},1433;Initial Catalog=${database.outputs.databaseName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
    corsOrigin: 'https://${staticWebApp.outputs.defaultHostname}'
    // Match main hostname and PR staging URLs (e.g., <name>-123.<region>.<slot>.azurestaticapps.net)
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

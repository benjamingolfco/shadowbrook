// SQL Server Entra AD (AAD) Administrator module
// Sets a user-assigned managed identity as the Azure SQL Server Entra AD admin.
// SQL admin credentials are preserved — Entra-only auth is NOT enforced.

@description('Name of the existing SQL Server resource')
param sqlServerName string

@description('Principal ID (object ID) of the managed identity to set as AD admin')
param managedIdentityPrincipalId string

@description('Display name of the managed identity (used as the AD admin login)')
param managedIdentityName string

// Reference existing SQL Server in this resource group
resource sqlServer 'Microsoft.Sql/servers@2023-08-01' existing = {
  name: sqlServerName
}

// Set the managed identity as the Entra AD administrator
// Name must be 'ActiveDirectory' — this is the fixed resource name for SQL AD admins
resource sqlAadAdmin 'Microsoft.Sql/servers/administrators@2023-08-01' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: managedIdentityName
    sid: managedIdentityPrincipalId
    tenantId: tenant().tenantId
  }
}

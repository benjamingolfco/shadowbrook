// Azure SQL Database module
// Creates SQL Server and database (Basic tier)
// Uses Entra-only auth — managed identity is the sole admin (no SQL credentials)

@description('Environment name (dev, staging, prod)')
param environment string

@description('Azure region for resources')
param location string

@description('Principal ID (object ID) of the managed identity to set as AD admin')
param managedIdentityPrincipalId string

@description('Display name of the managed identity (used as the AD admin login)')
param managedIdentityName string

// Resource names
var sqlServerName = 'teeforce-sql-${environment}'
var databaseName = 'teeforce-db-${environment}'

// Azure SQL Server — Entra-only authentication, no SQL admin credentials
resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: sqlServerName
  location: location
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: managedIdentityName
      principalType: 'Application'
      sid: managedIdentityPrincipalId
      tenantId: tenant().tenantId
    }
  }
}

// Firewall rule to allow Azure services
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Azure SQL Database - Basic tier (5 DTU, 2GB)
resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2GB
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
    readScale: 'Disabled'
    requestedBackupStorageRedundancy: 'Local'
  }
}

// Outputs
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name

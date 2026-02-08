// Main Bicep template for Shadowbrook dev environment
// Orchestrates Container Apps, Azure SQL, and supporting resources

@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@description('Azure region for resources')
param location string = resourceGroup().location

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('Azure Container Registry name for container images')
param acrName string

@description('Container image tag')
param imageTag string = 'latest'

// Deploy Azure SQL Database
module database 'database.bicep' = {
  name: 'database-deployment'
  params: {
    environment: environment
    location: location
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
  }
}

// Deploy Container Apps environment and app
module containerApp 'container-app.bicep' = {
  name: 'container-app-deployment'
  params: {
    environment: environment
    location: location
    acrName: acrName
    imageTag: imageTag
    sqlConnectionString: database.outputs.connectionString
  }
}

// Outputs
output appUrl string = containerApp.outputs.appUrl
output sqlServerFqdn string = database.outputs.sqlServerFqdn
output databaseName string = database.outputs.databaseName

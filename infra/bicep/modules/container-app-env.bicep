// Azure Container Apps Environment
// Shared environment that can host multiple Container Apps

@description('Environment name (dev, staging, prod)')
param environment string

@description('Azure region for resources')
param location string

var containerAppEnvName = 'shadowbrook-env-${environment}'

resource containerAppEnv 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: containerAppEnvName
  location: location
  properties: {
    zoneRedundant: false
  }
}

output id string = containerAppEnv.id
output name string = containerAppEnv.name

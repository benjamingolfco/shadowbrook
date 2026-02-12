// Azure Container Registry Bicep module
// Creates a shared ACR for storing container images across all environments

@description('Azure region for the container registry')
param location string

// ACR names must be globally unique, alphanumeric only
var name = 'shadowbrookacr'

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: name
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
  tags: {
    workload: 'shadowbrook'
  }
}

@description('The name of the container registry')
output name string = containerRegistry.name

@description('The resource ID of the container registry')
output id string = containerRegistry.id

@description('The login server URL for the container registry')
output loginServer string = containerRegistry.properties.loginServer

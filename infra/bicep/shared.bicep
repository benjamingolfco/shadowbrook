// Shadowbrook - Shared Infrastructure
//
// Resources deployed once and shared across all environments.
// Deployed to: shadowbrook-shared-rg

@description('Azure region for shared resources')
param location string = 'eastus'

module registry 'modules/registry.bicep' = {
  name: 'shared-registry-deployment'
  params: {
    location: location
  }
}

output registryName string = registry.outputs.name
output registryLoginServer string = registry.outputs.loginServer

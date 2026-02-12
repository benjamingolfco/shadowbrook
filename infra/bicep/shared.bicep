// Shadowbrook - Shared Infrastructure
//
// Subscription-level deployment that creates the shared resource group
// and deploys resources shared across all environments.
// Deployed via: az deployment sub create

targetScope = 'subscription'

@description('Azure region for shared resources')
param location string = 'eastus2'

@description('Name of the shared resource group')
param resourceGroupName string = 'shadowbrook-shared-rg'

// Create the shared resource group
resource sharedRg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

// Deploy shared resources into the resource group
module registry 'modules/registry.bicep' = {
  name: 'shared-registry-deployment'
  scope: sharedRg
  params: {
    location: location
  }
}

output registryName string = registry.outputs.name
output registryLoginServer string = registry.outputs.loginServer

// User-Assigned Managed Identity Bicep module
// Created independently so role assignments can exist BEFORE the Container App,
// solving the chicken-and-egg problem with ACR authentication on first deployment.

@description('Environment name (dev, staging, prod)')
param environment string

@description('Azure region for the managed identity')
param location string

var name = 'id-shadowbrook-${environment}'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: name
  location: location
  tags: {
    environment: environment
    workload: 'shadowbrook'
  }
}

@description('The resource ID of the managed identity')
output id string = managedIdentity.id

@description('The principal ID (object ID) of the managed identity')
output principalId string = managedIdentity.properties.principalId

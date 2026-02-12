// ACR Pull Role Assignment Bicep module
// Grants AcrPull access to a managed identity on the container registry.
// Deployed BEFORE the Container App so permissions exist on first deployment.

@description('Name of the Container Registry')
param acrName string

@description('Principal ID of the managed identity to grant AcrPull access')
param containerAppPrincipalId string

// Reference existing ACR in this resource group
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: acrName
}

// Built-in AcrPull role definition
// See: https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#acrpull
resource acrPullRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
}

// AcrPull role assignment - allows managed identity to pull images from ACR
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, containerAppPrincipalId, acrPullRoleDefinition.id)
  scope: containerRegistry
  properties: {
    principalId: containerAppPrincipalId
    roleDefinitionId: acrPullRoleDefinition.id
    principalType: 'ServicePrincipal'
  }
}

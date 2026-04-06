// Azure Key Vault Bicep module
// Creates a Key Vault with RBAC authorization and grants the Container App's
// managed identity the Key Vault Secrets User role for reading secrets at runtime.

@description('Environment name (test, staging, prod)')
param environment string

@description('Azure region for resources')
param location string

@description('Principal ID of the managed identity to grant Key Vault Secrets User access')
param managedIdentityPrincipalId string

var keyVaultName = 'kv-teeforce-${environment}'

// Key Vault with RBAC authorization (not access policies)
resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: keyVaultName
  location: location
  tags: {
    environment: environment
    workload: 'teeforce'
  }
  properties: {
    tenantId: tenant().tenantId
    sku: {
      name: 'standard'
      family: 'A'
    }
    enableRbacAuthorization: true
  }
}

// Built-in Key Vault Secrets User role definition
// See: https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#key-vault-secrets-user
resource kvSecretsUserRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '4633458b-17de-408a-b874-0445c86b69e6'
}

// Key Vault Secrets User role assignment — allows managed identity to read secrets
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, managedIdentityPrincipalId, kvSecretsUserRoleDefinition.id)
  scope: keyVault
  properties: {
    principalId: managedIdentityPrincipalId
    roleDefinitionId: kvSecretsUserRoleDefinition.id
    principalType: 'ServicePrincipal'
  }
}

@description('The name of the Key Vault')
output name string = keyVault.name

@description('The URI of the Key Vault')
output vaultUri string = keyVault.properties.vaultUri

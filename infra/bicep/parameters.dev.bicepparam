using './main.bicep'

// Non-sensitive environment configuration
// Note: imageTag is resolved at deploy time by the workflow (preserves running image)
param environment = 'dev'
param location = 'eastus2'

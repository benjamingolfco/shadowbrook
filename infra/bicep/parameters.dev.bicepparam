using './main.bicep'

// Non-sensitive environment configuration
// Note: imageTag is resolved at deploy time by the workflow (preserves running image)
param environment = 'dev'
param location = 'eastus2'

// Sensitive credentials — resolved from environment variables at deploy time.
// These map to @secure() parameters in main.bicep, so values never appear in logs.
param sqlAdminLogin = readEnvironmentVariable('SQL_ADMIN_LOGIN')
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')

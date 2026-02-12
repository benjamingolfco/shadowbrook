using './main.bicep'

// Non-sensitive environment configuration
param environment = 'dev'
param location = 'eastus'
param imageTag = 'latest'

// Sensitive credentials — resolved from environment variables at deploy time.
// These map to @secure() parameters in main.bicep, so values never appear in logs.
param sqlAdminLogin = readEnvironmentVariable('SQL_ADMIN_LOGIN')
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')

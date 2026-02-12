// Azure Static Web Apps Bicep module
// Hosts the React SPA frontend (Free tier)

@description('Environment name (dev, staging, prod)')
param environment string

@description('Azure region for resources')
param location string

// Resource names
var staticWebAppName = 'shadowbrook-web-${environment}'

// Static Web App
resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

// Outputs
output name string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname

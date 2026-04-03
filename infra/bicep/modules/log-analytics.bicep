// Azure Log Analytics Workspace
// Shared sink for App Insights and Container Apps Environment logs

@description('Environment name (dev, staging, prod)')
param environment string

@description('Azure region for resources')
param location string

@description('Daily ingestion cap in GB (must exceed actual daily volume or telemetry is silently dropped)')
param dailyCapGb string = '1'

var workspaceName = 'shadowbrook-logs-${environment}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json(dailyCapGb)
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

output id string = logAnalytics.id
output customerId string = logAnalytics.properties.customerId
@secure()
output sharedKey string = logAnalytics.listKeys().primarySharedKey
output name string = logAnalytics.name

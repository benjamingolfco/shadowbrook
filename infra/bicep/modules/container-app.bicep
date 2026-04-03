// Azure Container App Bicep module
// Deploys a Container App into an existing Container Apps Environment

@description('Environment name (dev, staging, prod)')
param environment string

@description('Azure region for resources')
param location string

@description('Resource ID of the Container Apps Environment')
param containerAppEnvId string

@description('Container registry login server URL (e.g., myregistry.azurecr.io)')
param containerRegistryLoginServer string

@description('Resource ID of the user-assigned managed identity for ACR pull')
param userAssignedIdentityId string

@description('Container image tag')
param imageTag string

@description('SQL connection string')
@secure()
param sqlConnectionString string

@description('Application Insights connection string')
@secure()
param appInsightsConnectionString string

@description('Allowed CORS origin (e.g., https://my-swa.azurestaticapps.net)')
param corsOrigin string

@description('Frontend URL used in outbound links (e.g., SMS messages with claim URLs)')
param frontendUrl string

@description('Client ID of the user-assigned managed identity (used for DefaultAzureCredential)')
param managedIdentityClientId string

@description('Regex pattern for allowed CORS origins (e.g., PR staging URLs). When set, takes precedence over corsOrigin.')
param corsOriginPattern string = ''

var containerAppName = 'shadowbrook-app-${environment}'

// Container App
resource containerApp 'Microsoft.App/containerApps@2025-01-01' = {
  name: containerAppName
  location: location
  properties: {
    environmentId: containerAppEnvId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        corsPolicy: {
          allowedOrigins: ['*']
          allowedMethods: ['*']
          allowedHeaders: ['*']
          maxAge: 3600
        }
      }
      registries: [
        {
          server: containerRegistryLoginServer
          identity: userAssignedIdentityId
        }
      ]
      secrets: [
        {
          name: 'sql-connection-string'
          value: sqlConnectionString
        }
        {
          name: 'app-insights-connection-string'
          value: appInsightsConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'shadowbrook-api'
          image: '${containerRegistryLoginServer}/shadowbrook:${imageTag}'
          resources: {
            cpu: json('0.75')
            memory: '1.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environment == 'prod' ? 'Production' : (environment == 'staging' ? 'Staging' : 'Test')
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection-string'
            }
            {
              name: 'App__FrontendUrl'
              value: frontendUrl
            }
            {
              name: 'Cors__AllowedOrigins__0'
              value: corsOrigin
            }
            {
              name: 'Cors__AllowedOriginPattern'
              value: corsOriginPattern
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'app-insights-connection-string'
            }
            {
              name: 'AzureAd__ManagedIdentityClientId'
              value: managedIdentityClientId
            }
            // GC tuning: keep heap within budget and return memory to OS aggressively.
            // GCHeapHardLimit is set to 640MiB (~60% of the 1Gi container limit),
            // leaving headroom for native memory, thread stacks, and Wolverine codegen.
            {
              name: 'DOTNET_GCConserveMemory'
              value: '7'
            }
            {
              name: 'DOTNET_GCHeapHardLimit'
              value: '671088640'
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              failureThreshold: 30
              timeoutSeconds: 5
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 30
              failureThreshold: 3
              timeoutSeconds: 5
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              failureThreshold: 3
              timeoutSeconds: 5
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
}

// Outputs
output appUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output containerAppName string = containerApp.name
output containerAppEnvId string = containerAppEnvId

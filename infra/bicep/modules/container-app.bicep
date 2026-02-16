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

@description('Allowed CORS origin (e.g., https://my-swa.azurestaticapps.net)')
param corsOrigin string

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
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
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
      ]
    }
    template: {
      containers: [
        {
          name: 'shadowbrook-api'
          image: '${containerRegistryLoginServer}/shadowbrook:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection-string'
            }
            {
              name: 'Cors__AllowedOrigins__0'
              value: corsOrigin
            }
            {
              name: 'Cors__AllowedOriginPattern'
              value: corsOriginPattern
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 30
              timeoutSeconds: 3
              failureThreshold: 3
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
              timeoutSeconds: 3
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
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

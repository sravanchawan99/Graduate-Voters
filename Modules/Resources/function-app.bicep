@description('Deployment region for the Function App resources.')
param location string

@description('Existing Storage account created by the main deployment.')
param storageAccountName string

@description('Function App name. Must be globally unique and use lowercase letters, numbers, and hyphens.')
param functionAppName string

@description('Existing Service Bus namespace created by the main deployment.')
param serviceBusNamespaceName string

@description('Service Bus queue consumed by the Function App.')
param queueName string

@description('Name of the Flex Consumption hosting plan.')
param functionPlanName string

@description('Name of the Application Insights component.')
param appInsightsName string

@description('Allowed browser origins for the Function App CORS configuration.')
param corsAllowedOrigins array

@description('Timer schedule for queuing pending voter exports, in NCRONTAB format.')
param voterExportSchedule string

var deploymentContainerName = 'function-releases'
var serviceBusAccessRuleId = resourceId(
  'Microsoft.ServiceBus/namespaces/authorizationRules',
  serviceBusNamespaceName,
  'FunctionAppAccess'
)
var serviceBusConnectionString = listKeys(serviceBusAccessRuleId, '2021-06-01-preview').primaryConnectionString
var storageKey = storageAccount.listKeys().keys[0].value
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageKey};EndpointSuffix=${environment().suffixes.storage}'
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' existing = {
  parent: storageAccount
  name: 'default'
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: deploymentContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = {
  name: serviceBusNamespaceName
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
  }
}

resource flexPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: functionPlanName
  location: location
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: flexPlan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      cors: {
        allowedOrigins: corsAllowedOrigins
      }
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'StorageConnectionString'
          value: storageConnectionString
        }
        {
          name: 'ServiceBusConnection'
          value: serviceBusConnectionString
        }
        {
          name: 'VoterQueueName'
          value: queueName
        }
        {
          name: 'VoterExportSchedule'
          value: voterExportSchedule
        }
        {
          name: 'VoterTableName'
          value: 'Voters'
        }
        {
          name: 'AdminTableName'
          value: 'AdminLogin'
        }
        {
          name: 'VoterBlobContainerName'
          value: 'voters'
        }
        {
          name: 'VoterBlobDirectory'
          value: 'export'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
      ]
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageAccount.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            type: 'StorageAccountConnectionString'
            storageAccountConnectionStringName: 'AzureWebJobsStorage'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 1
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '8.0'
      }
    }
  }
  dependsOn: [
    deploymentContainer
    serviceBusNamespace
  ]
}

output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output applicationInsightsName string = applicationInsights.name

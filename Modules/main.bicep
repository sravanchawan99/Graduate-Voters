targetScope = 'subscription'

param resourceGroupName string
param location string
param storageAccountName string
param serviceBusNamespaceName string
param queueName string
@description('Timer schedule for queuing pending voter exports, in NCRONTAB format.')
param voterExportSchedule string = '0 */5 * * * *'
param functionAppName string
param functionPlanName string
param appInsightsName string
@description('Name of the environment Static Web App. Must be globally unique.')
param staticWebAppName string
@description('Custom domain to bind after DNS verification. Leave empty to use the Azure-provided hostname.')
param customDomainName string
@description('Set to true after the domain ownership TXT record is in DNS.')
param configureCustomDomain bool = false

module rg './Resources/resource-group.bicep' = {
  name: 'resourceGroupDeployment'
  params: {
    resourceGroupName: resourceGroupName
    location: location
  }
}
module storage './Resources/storage-account.bicep' = {
  name: 'storageDeployment'
  scope: resourceGroup(resourceGroupName)
  params: {
    location: location
    storageAccountName: storageAccountName
  }
}
module serviceBus './Resources/servicebus.bicep' = {
  name: 'serviceBusDeployment'
  scope: resourceGroup(resourceGroupName)
  params: {
    location: location
    serviceBusNamespaceName: serviceBusNamespaceName
    queueName: queueName
  }
}

module functionApp './Resources/function-app.bicep' = {
  name: 'functionAppDeployment'
  scope: resourceGroup(resourceGroupName)
  params: {
    location: location
    storageAccountName: storageAccountName
    functionAppName: functionAppName
    serviceBusNamespaceName: serviceBusNamespaceName
    queueName: queueName
    voterExportSchedule: voterExportSchedule
    functionPlanName: functionPlanName
    appInsightsName: appInsightsName
    corsAllowedOrigins: empty(customDomainName)
      ? [
          'https://${staticWebApp.outputs.defaultHostname}'
          'http://localhost:4200'
        ]
      : [
          'https://${customDomainName}'
          'https://${staticWebApp.outputs.defaultHostname}'
        ]
  }
  dependsOn: [
    rg
    storage
    serviceBus
    staticWebApp
  ]
}

module staticWebApp './Resources/static-web-app.bicep' = {
  name: 'staticWebAppDeployment'
  scope: resourceGroup(resourceGroupName)
  params: {
    location: location
    staticWebAppName: staticWebAppName
    customDomainName: customDomainName
    configureCustomDomain: configureCustomDomain
  }
  dependsOn: [
    rg
  ]
}

output functionAppName string = functionApp.outputs.functionAppName
output functionAppUrl string = functionApp.outputs.functionAppUrl
output applicationInsightsName string = functionApp.outputs.applicationInsightsName
output staticWebAppName string = staticWebApp.outputs.staticWebAppName
output staticWebAppUrl string = 'https://${staticWebApp.outputs.defaultHostname}'

@description('Location for the Service Bus namespace')
param location string

@description('Globally unique Service Bus namespace name')
param serviceBusNamespaceName string

@description('Service Bus queue name')
param queueName string

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
  }
}

resource serviceBusQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: queueName
  properties: {
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P7D'
    deadLetteringOnMessageExpiration: true
    enablePartitioning: false
  }
}

// Dedicated app access: the Function App needs to send voter messages and
// listen to the export queue, but does not need namespace management rights.
resource functionAppAccess 'Microsoft.ServiceBus/namespaces/authorizationRules@2021-06-01-preview' = {
  parent: serviceBusNamespace
  name: 'FunctionAppAccess'
  properties: {
    rights: [
      'Listen'
      'Send'
    ]
  }
}

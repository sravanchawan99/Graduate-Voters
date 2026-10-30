@description('Location for Azure resources')
param location string

@description('Globally unique storage account name')
param storageAccountName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'

  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Enabled'
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
}

resource votersTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: 'Voters'
  parent: tableService
}

resource adminLoginTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: 'AdminLogin'
  parent: tableService
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
}

resource votersContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: 'voters'
  parent: blobService
  properties: {
    publicAccess: 'None'
  }
}

@description('Name of the Azure Static Web App. Must be globally unique.')
param staticWebAppName string

@description('Azure region used for the Static Web App resource.')
param location string

@description('Production custom domain to associate after DNS ownership is verified.')
param customDomainName string

@description('Set to true after the required DNS TXT ownership record has been created.')
param configureCustomDomain bool = false

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

resource customDomain 'Microsoft.Web/staticSites/customDomains@2023-12-01' = if (configureCustomDomain) {
  parent: staticWebApp
  name: customDomainName
  properties: {
    validationMethod: 'dns-txt-token'
  }
}

output staticWebAppName string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname

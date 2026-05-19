targetScope = 'resourceGroup'

@description('Azure region for all MiniPainterHub production resources.')
param location string = resourceGroup().location

@description('Environment suffix used in resource names and app settings.')
param environmentName string = 'prod'

@description('Globally unique App Service app name. The default is deterministic for this subscription and resource group.')
param webAppName string = toLower('app-mph-${environmentName}-${uniqueString(subscription().id, resourceGroup().name)}')

@description('App Service plan name.')
param appServicePlanName string = 'asp-minipainterhub-${environmentName}'

@description('Student-minimal App Service SKU.')
param appServiceSkuName string = 'B1'

@description('Student-minimal App Service SKU tier.')
param appServiceSkuTier string = 'Basic'

@description('Globally unique Azure SQL logical server name.')
param sqlServerName string = toLower('sql-mph-${environmentName}-${uniqueString(subscription().id, resourceGroup().name)}')

@description('Azure SQL database name.')
param sqlDatabaseName string = 'sqldb-minipainterhub-${environmentName}'

@description('Azure SQL administrator login.')
param sqlAdministratorLogin string = 'mphsqladmin'

@secure()
@description('Azure SQL administrator password.')
param sqlAdministratorPassword string

@description('Globally unique storage account name. Must be lower-case alphanumeric.')
param storageAccountName string = toLower('stmph${environmentName}${uniqueString(subscription().id, resourceGroup().name)}')

@description('Private blob container used for uploaded MiniPainterHub images.')
param imageContainerName string = 'minipainterhub-images'

@secure()
@description('JWT signing key used by the production app.')
param jwtKey string

@description('JWT issuer expected by the app.')
param jwtIssuer string = 'MiniPainterHubApi'

@description('JWT audience expected by the app.')
param jwtAudience string = 'MiniPainterHubClient'

@description('Whether production startup should seed an admin user.')
param seedAdminEnabled bool = false

@secure()
@description('Optional production seed admin email.')
param seedAdminEmail string = ''

@secure()
@description('Optional production seed admin password.')
param seedAdminPassword string = ''

var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdministratorLogin};Password=${sqlAdministratorPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

var requiredAppSettings = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'ConnectionStrings__DefaultConnection'
    value: sqlConnectionString
  }
  {
    name: 'Jwt__Key'
    value: jwtKey
  }
  {
    name: 'Jwt__Issuer'
    value: jwtIssuer
  }
  {
    name: 'Jwt__Audience'
    value: jwtAudience
  }
  {
    name: 'ImageStorage__AzureConnectionString'
    value: storageConnectionString
  }
  {
    name: 'ImageStorage__AzureContainer'
    value: imageContainerName
  }
  {
    name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
    value: 'false'
  }
]

var seedAdminAppSettings = seedAdminEnabled ? [
  {
    name: 'SeedAdmin__Enabled'
    value: 'true'
  }
  {
    name: 'SeedAdmin__Email'
    value: seedAdminEmail
  }
  {
    name: 'SeedAdmin__Password'
    value: seedAdminPassword
  }
] : [
  {
    name: 'SeedAdmin__Enabled'
    value: 'false'
  }
]

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    name: appServiceSkuName
    tier: appServiceSkuTier
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: concat(requiredAppSettings, seedAdminAppSettings)
    }
  }
}

resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    version: '12.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource imageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: imageContainerName
  properties: {
    publicAccess: 'None'
  }
}

output webAppName string = webApp.name
output webAppDefaultHostName string = webApp.properties.defaultHostName
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output storageAccountName string = storage.name

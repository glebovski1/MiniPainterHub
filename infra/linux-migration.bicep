targetScope = 'resourceGroup'

// Apply only after reviewing an incremental what-if and the approved operation.
// Paid scaling, identities, grants, app settings and dependency provisioning are
// separate operations; this template cannot create SQL or media resources.
@allowed(['rollandpaint-prod-ca'])
param candidateName string = 'rollandpaint-prod-ca'

@secure()
@description('Complete current config/web properties from the stopped candidate, preserved before overriding runtime fields. Never commit this snapshot.')
param currentSiteConfig object

@description('Resource group containing the retained SQL server.')
param sqlResourceGroup string
param sqlServerName string
param sqlDatabaseName string

@description('Resource group containing the retained media storage account.')
param mediaResourceGroup string
param mediaStorageAccountName string

resource candidate 'Microsoft.Web/sites@2023-12-01' existing = {
  name: candidateName
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' existing = {
  name: sqlServerName
  scope: resourceGroup(sqlResourceGroup)
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01' existing = {
  parent: sqlServer
  name: sqlDatabaseName
}

resource media 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: mediaStorageAccountName
  scope: resourceGroup(mediaResourceGroup)
}

resource runtime 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: candidate
  name: 'web'
  properties: union(currentSiteConfig, {
    linuxFxVersion: 'DOTNETCORE|10.0'
    appCommandLine: 'dotnet MiniPainterHub.Server.dll'
    alwaysOn: false
    ftpsState: 'Disabled'
    minTlsVersion: '1.2'
  })
}

output candidateId string = candidate.id
output retainedDatabaseId string = database.id
output retainedMediaId string = media.id

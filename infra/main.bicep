@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short prefix used to name resources.')
@minLength(3)
@maxLength(18)
param namePrefix string

@description('The globally unique App Service site name.')
param webAppName string = toLower('${namePrefix}-${uniqueString(resourceGroup().id, 'webapp')}')

@description('The App Service plan name.')
param appServicePlanName string = '${namePrefix}-plan'

@description('The Log Analytics workspace name.')
param logAnalyticsWorkspaceName string = '${namePrefix}-law'

@description('The Application Insights resource name.')
param appInsightsName string = '${namePrefix}-appi'

@description('The globally unique PostgreSQL flexible server name.')
param postgresServerName string = toLower('${namePrefix}-${uniqueString(resourceGroup().id, 'pgsql')}')

@description('The PostgreSQL database name used by Kaboom.')
param postgresDatabaseName string = 'kaboom'

@description('The PostgreSQL administrator login name.')
param postgresAdminLogin string = 'kaboomadmin'

@secure()
@description('The PostgreSQL administrator password.')
param postgresAdminPassword string

@description('App Service plan SKU name.')
@allowed([
  'B1'
  'B2'
  'B3'
  'S1'
  'S2'
  'S3'
  'P1v3'
  'P2v3'
  'P3v3'
])
param appServiceSkuName string = 'B1'

@description('App Service plan SKU tier.')
@allowed([
  'Basic'
  'Standard'
  'PremiumV3'
])
param appServiceSkuTier string = 'Basic'

@description('PostgreSQL compute SKU name.')
param postgresSkuName string = 'Standard_B1ms'

@description('PostgreSQL compute tier.')
@allowed([
  'Burstable'
  'GeneralPurpose'
  'MemoryOptimized'
])
param postgresSkuTier string = 'Burstable'

@description('PostgreSQL major version.')
@allowed([
  '14'
  '15'
  '16'
])
param postgresVersion string = '16'

@description('PostgreSQL storage size in GB.')
param postgresStorageSizeGB int = 32

@description('Public origin used by the app for OAuth return-url validation. Leave empty to use the Azure default hostname.')
param publicOrigin string = ''

@description('Optional Google OAuth client ID.')
param googleClientId string = ''

@secure()
@description('Optional Google OAuth client secret.')
param googleClientSecret string = ''

@description('Whether to create a GitHub deployment identity with an OIDC federated credential.')
param createGitHubDeploymentIdentity bool = true

@description('GitHub repository owner or organization, used for the OIDC trust relationship.')
param githubRepositoryOwner string = ''

@description('GitHub repository name, used for the OIDC trust relationship.')
param githubRepositoryName string = ''

@description('GitHub branch allowed to deploy through OIDC.')
param githubBranch string = 'main'

@description('Allow Azure services to connect to PostgreSQL. This is the simplest App Service connectivity path without private networking.')
param allowAzureServicesToPostgres bool = true

@description('Optional single IPv4 address to allow through the PostgreSQL firewall for admin access.')
param developerIpAddress string = ''

var appHostName = 'https://${webAppName}.azurewebsites.net'
var effectivePublicOrigin = empty(publicOrigin) ? appHostName : publicOrigin
var postgresConnectionString = 'Host=${postgresServerName}.postgres.database.azure.com;Port=5432;Database=${postgresDatabaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};Ssl Mode=Require'
var githubOidcSubject = 'repo:${githubRepositoryOwner}/${githubRepositoryName}:ref:refs/heads/${githubBranch}'
var contributorRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')

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

resource workspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ConnectionStrings__Kaboom'
          value: postgresConnectionString
        }
        {
          name: 'Application__PublicOrigin'
          value: effectivePublicOrigin
        }
        {
          name: 'Authentication__Google__ClientId'
          value: googleClientId
        }
        {
          name: 'Authentication__Google__ClientSecret'
          value: googleClientSecret
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2021-06-01' = {
  name: postgresServerName
  location: location
  sku: {
    name: postgresSkuName
    tier: postgresSkuTier
  }
  properties: {
    version: postgresVersion
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: postgresStorageSizeGB
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2021-06-01' = {
  parent: postgresServer
  name: postgresDatabaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource postgresAllowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2021-06-01' = if (allowAzureServicesToPostgres) {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource postgresAllowDeveloper 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2021-06-01' = if (!empty(developerIpAddress)) {
  parent: postgresServer
  name: 'AllowDeveloper'
  properties: {
    startIpAddress: developerIpAddress
    endIpAddress: developerIpAddress
  }
}

resource githubDeploymentIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = if (createGitHubDeploymentIdentity) {
  name: '${namePrefix}-github'
  location: location
}

resource githubOidc 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = if (createGitHubDeploymentIdentity && !empty(githubRepositoryOwner) && !empty(githubRepositoryName)) {
  parent: githubDeploymentIdentity
  name: 'github-main'
  properties: {
    audiences: [
      'api://AzureADTokenExchange'
    ]
    issuer: 'https://token.actions.githubusercontent.com'
    subject: githubOidcSubject
  }
}

resource githubDeploymentRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (createGitHubDeploymentIdentity) {
  name: guid(resourceGroup().id, githubDeploymentIdentity.id, contributorRoleDefinitionId)
  scope: resourceGroup()
  properties: {
    principalId: githubDeploymentIdentity!.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: contributorRoleDefinitionId
  }
}

output webAppName string = webApp.name
output webAppUrl string = appHostName
output publicOrigin string = effectivePublicOrigin
output postgresServerHost string = '${postgresServer.name}.postgres.database.azure.com'
output postgresDatabaseName string = postgresDatabase.name
output githubDeploymentClientId string = createGitHubDeploymentIdentity ? githubDeploymentIdentity!.properties.clientId : ''
output githubDeploymentPrincipalId string = createGitHubDeploymentIdentity ? githubDeploymentIdentity!.properties.principalId : ''
output azureTenantId string = tenant().tenantId
output azureSubscriptionId string = subscription().subscriptionId

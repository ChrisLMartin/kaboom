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

@description('The globally unique Key Vault name.')
param keyVaultName string = toLower('${take(namePrefix, 12)}${uniqueString(resourceGroup().id, 'kv')}')

@description('The globally unique PostgreSQL flexible server name.')
param postgresServerName string = toLower('${namePrefix}-${uniqueString(resourceGroup().id, 'pgsql')}')

@description('The PostgreSQL database name used by Kaboom.')
param postgresDatabaseName string = 'kaboom'

@description('The PostgreSQL administrator login name.')
param postgresAdminLogin string = 'kaboomadmin'

@secure()
@description('The PostgreSQL administrator password. Supply this from a secure pipeline secret or an untracked local parameter file.')
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
@description('Optional Google OAuth client secret. Supply this from a secure pipeline secret or an untracked local parameter file.')
param googleClientSecret string = ''

@description('Name of the virtual network used for App Service integration and private resources.')
param virtualNetworkName string = '${namePrefix}-vnet'

@description('CIDR for the virtual network.')
param virtualNetworkAddressPrefix string = '10.20.0.0/16'

@description('CIDR for the App Service VNet integration subnet.')
param appServiceSubnetPrefix string = '10.20.0.0/24'

@description('CIDR for the private endpoint subnet.')
param privateEndpointSubnetPrefix string = '10.20.1.0/24'

@description('CIDR for the PostgreSQL delegated subnet.')
param postgresSubnetPrefix string = '10.20.2.0/24'

var appHostName = 'https://${webAppName}.azurewebsites.net'
var effectivePublicOrigin = empty(publicOrigin) ? appHostName : publicOrigin
var postgresConnectionString = 'Host=${postgresServerName}.postgres.database.azure.com;Port=5432;Database=${postgresDatabaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};Ssl Mode=Require'
var keyVaultDnsSuffix = environment().suffixes.keyvaultDns
var keyVaultSecretsUserRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
var postgresAdminPasswordSecretName = 'postgres-admin-password'
var connectionStringSecretName = 'kaboom-connection-string'
var googleClientSecretName = 'google-client-secret'
var postgresPrivateDnsZoneName = 'private.postgres.database.azure.com'
var keyVaultPrivateDnsZoneName = 'privatelink.vaultcore.azure.net'

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: virtualNetworkName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        virtualNetworkAddressPrefix
      ]
    }
    subnets: [
      {
        name: 'appservice-integration'
        properties: {
          addressPrefix: appServiceSubnetPrefix
          delegations: [
            {
              name: 'appservice-delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      {
        name: 'private-endpoints'
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
      {
        name: 'postgres-delegated'
        properties: {
          addressPrefix: postgresSubnetPrefix
          delegations: [
            {
              name: 'postgres-delegation'
              properties: {
                serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
              }
            }
          ]
        }
      }
    ]
  }
}

resource appServiceIntegrationSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: virtualNetwork
  name: 'appservice-integration'
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: virtualNetwork
  name: 'private-endpoints'
}

resource postgresSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' existing = {
  parent: virtualNetwork
  name: 'postgres-delegated'
}

resource postgresPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: postgresPrivateDnsZoneName
  location: 'global'
}

resource postgresPrivateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: postgresPrivateDnsZone
  name: '${virtualNetworkName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource keyVaultPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: keyVaultPrivateDnsZoneName
  location: 'global'
}

resource keyVaultPrivateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: keyVaultPrivateDnsZone
  name: '${virtualNetworkName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

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

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: true
    sku: {
      family: 'A'
      name: 'standard'
    }
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'None'
      defaultAction: 'Deny'
    }
  }
}

resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${namePrefix}-kv-pe'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'keyvault'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource keyVaultPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: keyVaultPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'vaultcore'
        properties: {
          privateDnsZoneId: keyVaultPrivateDnsZone.id
        }
      }
    ]
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
    virtualNetworkSubnetId: appServiceIntegrationSubnet.id
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      linuxFxVersion: 'DOTNETCORE|10.0'
      vnetRouteAllEnabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
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
          value: empty(googleClientId)
            ? ''
            : '@Microsoft.KeyVault(SecretUri=https://${keyVault.name}.${keyVaultDnsSuffix}/secrets/${googleClientSecretName})'
        }
        {
          name: 'ConnectionStrings__Kaboom'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVault.name}.${keyVaultDnsSuffix}/secrets/${connectionStringSecretName})'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

resource webAppKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, keyVaultSecretsUserRoleDefinitionId)
  scope: keyVault
  properties: {
    principalId: webApp.identity.principalId!
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleDefinitionId
  }
}

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = {
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
    network: {
      delegatedSubnetResourceId: postgresSubnet.id
      privateDnsZoneArmResourceId: postgresPrivateDnsZone.id
    }
    storage: {
      storageSizeGB: postgresStorageSizeGB
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
  }
  dependsOn: [
    postgresPrivateDnsZoneLink
  ]
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: postgresServer
  name: postgresDatabaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource postgresAdminPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: postgresAdminPasswordSecretName
  properties: {
    value: postgresAdminPassword
  }
}

resource connectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: connectionStringSecretName
  properties: {
    value: postgresConnectionString
  }
}

resource googleClientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: googleClientSecretName
  properties: {
    value: googleClientSecret
  }
}

output webAppName string = webApp.name
output webAppUrl string = appHostName
output publicOrigin string = effectivePublicOrigin
output keyVaultName string = keyVault.name
output keyVaultUri string = 'https://${keyVault.name}.${keyVaultDnsSuffix}/'
output postgresServerHost string = '${postgresServer.name}.postgres.database.azure.com'
output postgresDatabaseName string = postgresDatabase.name

@description('Azure region for the GitHub deployment identity.')
param location string = resourceGroup().location

@description('Short prefix used to name the GitHub deployment identity.')
@minLength(3)
@maxLength(18)
param namePrefix string

@description('GitHub repository owner or organization.')
param githubRepositoryOwner string

@description('GitHub repository name.')
param githubRepositoryName string

@description('GitHub branch allowed to deploy.')
param githubBranch string = 'main'

var githubOidcSubject = 'repo:${githubRepositoryOwner}/${githubRepositoryName}:ref:refs/heads/${githubBranch}'
var contributorRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
var roleDefinitionId = contributorRoleDefinitionId

resource githubDeploymentIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-github'
  location: location
}

resource githubOidc 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: githubDeploymentIdentity
  name: 'github-${githubBranch}'
  properties: {
    audiences: [
      'api://AzureADTokenExchange'
    ]
    issuer: 'https://token.actions.githubusercontent.com'
    subject: githubOidcSubject
  }
}

resource githubDeploymentRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, githubDeploymentIdentity.id, roleDefinitionId)
  scope: resourceGroup()
  properties: {
    principalId: githubDeploymentIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: roleDefinitionId
  }
}

output githubDeploymentClientId string = githubDeploymentIdentity.properties.clientId
output githubDeploymentPrincipalId string = githubDeploymentIdentity.properties.principalId
output azureTenantId string = tenant().tenantId
output azureSubscriptionId string = subscription().subscriptionId
output githubOidcSubject string = githubOidcSubject

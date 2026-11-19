// One deployment identity: a user-assigned managed identity that GitHub Actions signs in as through
// OpenID Connect (ADR-046), restricted to one subject claim and one role in this resource group.
targetScope = 'resourceGroup'

@description('Name of the managed identity.')
param name string

@description('Subject claim of the GitHub token, for example repo:Owner/Repo:environment:test.')
param subject string

@description('Full resource id of the role definition assigned at this resource group.')
param roleDefinitionId string

param location string = resourceGroup().location

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
  tags: {
    app: 'thoughttree'
  }
}

resource githubCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-actions'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: subject
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, identity.id, roleDefinitionId)
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: roleDefinitionId
  }
}

output clientId string = identity.properties.clientId

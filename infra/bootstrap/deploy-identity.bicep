// One deployment identity: a user-assigned managed identity that GitHub Actions signs in as through OpenID
// Connect (ADR-046), restricted to one subject claim. Deployed into rg-thoughttree-identity, where no
// deployment identity has a role; its access to a site is granted by site-access.bicep.
targetScope = 'resourceGroup'

@description('Name of the managed identity.')
param name string

@description('Subject claim of the GitHub token, for example repo:Owner/Repo:environment:test.')
param subject string

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

output clientId string = identity.properties.clientId
output principalId string = identity.properties.principalId

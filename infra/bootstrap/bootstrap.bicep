// One-time setup, run by the owner (infra/README.md): the three resource groups and the three deployment
// identities that GitHub Actions signs in as through OpenID Connect. No secret is created or stored
// (ADR-046). The Static Web Apps themselves are created by the deploy workflows from main.bicep.
//
//   test       Contributor on rg-thoughttree-test, only for jobs in the GitHub environment "test"
//   production Contributor on rg-thoughttree-prod, only for jobs in the GitHub environment "production"
//   preview    custom role on rg-thoughttree-preview only (its own site, nothing of test or production); only
//              for jobs in the GitHub environment "preview", which runs from main (preview-deploy.yml), never
//              with pull request code.
targetScope = 'subscription'

@description('GitHub repository as owner/name, with the exact capitalisation GitHub uses.')
param repository string

@description('Azure region of the resource groups and identities.')
param location string = 'westeurope'

var contributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')

resource testGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-thoughttree-test'
  location: location
  tags: {
    app: 'thoughttree'
    environment: 'test'
  }
}

resource productionGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-thoughttree-prod'
  location: location
  tags: {
    app: 'thoughttree'
    environment: 'production'
  }
}

resource previewGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-thoughttree-preview'
  location: location
  tags: {
    app: 'thoughttree'
    environment: 'preview'
  }
}

resource previewRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(subscription().id, 'thoughttree-preview-deployer')
  properties: {
    roleName: 'ThoughtTree preview deployer'
    description: 'Deploys and removes pull request previews on the ThoughtTree preview site; nothing else.'
    type: 'CustomRole'
    permissions: [
      {
        actions: [
          'Microsoft.Resources/subscriptions/resourceGroups/read'
          'Microsoft.Resources/deployments/*'
          'Microsoft.Web/staticSites/read'
          'Microsoft.Web/staticSites/write'
          'Microsoft.Web/staticSites/listSecrets/action'
          'Microsoft.Web/staticSites/builds/read'
          'Microsoft.Web/staticSites/builds/delete'
        ]
        notActions: []
      }
    ]
    assignableScopes: [
      previewGroup.id
    ]
  }
}

module testIdentity 'deploy-identity.bicep' = {
  name: 'thoughttree-identity-test'
  scope: testGroup
  params: {
    name: 'id-thoughttree-test'
    subject: 'repo:${repository}:environment:test'
    roleDefinitionId: contributorRoleId
    location: location
  }
}

module productionIdentity 'deploy-identity.bicep' = {
  name: 'thoughttree-identity-production'
  scope: productionGroup
  params: {
    name: 'id-thoughttree-production'
    subject: 'repo:${repository}:environment:production'
    roleDefinitionId: contributorRoleId
    location: location
  }
}

module previewIdentity 'deploy-identity.bicep' = {
  name: 'thoughttree-identity-preview'
  scope: previewGroup
  params: {
    name: 'id-thoughttree-preview'
    subject: 'repo:${repository}:environment:preview'
    roleDefinitionId: previewRole.id
    location: location
  }
}

// Values for the GitHub repository variables (infra/README.md); identifiers, not secrets.
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_SUBSCRIPTION_ID string = subscription().subscriptionId
output AZURE_CLIENT_ID_TEST string = testIdentity.outputs.clientId
output AZURE_CLIENT_ID_PRODUCTION string = productionIdentity.outputs.clientId
output AZURE_CLIENT_ID_PREVIEW string = previewIdentity.outputs.clientId

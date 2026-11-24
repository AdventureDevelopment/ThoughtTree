// One-time setup, run by the owner (infra/README.md): the resource groups, the three deployment identities that
// GitHub Actions signs in as through OpenID Connect, and their access. No secret is created or stored (ADR-046).
// The Static Web Apps themselves are created by the deploy workflows from main.bicep.
//
// The identities and their federated credentials live in rg-thoughttree-identity, where no deployment identity
// has any role: a pipeline run cannot add a federated credential or a role assignment, so a change merged to
// main cannot give itself or another repository a way around the production approval. Each identity has only
// the custom role "ThoughtTree site deployer" on its own site's resource group:
//
//   test       rg-thoughttree-test, only for jobs in the GitHub environment "test" (only main)
//   production rg-thoughttree-prod, only for jobs in the GitHub environment "production" (owner's approval)
//   preview    rg-thoughttree-preview, only for jobs in the GitHub environment "preview" (preview-deploy.yml,
//              runs from main, never with pull request code)
//
// Changing this file changes nothing in Azure until the owner runs it again.
targetScope = 'subscription'

@description('GitHub repository as GitHub writes it into the OpenID Connect subject claim: owner@ownerId/name@repositoryId, for example AdventureDevelopment@322102914/ThoughtTree@1452386759 (infra/README.md).')
param repository string

@description('Azure region of the resource groups and identities.')
param location string = 'westeurope'

var environments = [
  {
    name: 'test'
    group: 'rg-thoughttree-test'
  }
  {
    name: 'production'
    group: 'rg-thoughttree-prod'
  }
  {
    name: 'preview'
    group: 'rg-thoughttree-preview'
  }
]

resource identityGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-thoughttree-identity'
  location: location
  tags: {
    app: 'thoughttree'
  }
}

resource siteGroups 'Microsoft.Resources/resourceGroups@2024-03-01' = [for environment in environments: {
  name: environment.group
  location: location
  tags: {
    app: 'thoughttree'
    environment: environment.name
  }
}]

// Exactly what the deploy workflows need: apply main.bicep (deployments, the site itself), read the deployment
// token, list and delete staging environments. No Microsoft.Authorization (role assignments, role definitions)
// and no Microsoft.ManagedIdentity (identities, federated credentials). There is no built-in role for Static
// Web Apps deployments; Contributor would allow both.
resource siteDeployerRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(subscription().id, 'thoughttree-site-deployer')
  properties: {
    roleName: 'ThoughtTree site deployer'
    description: 'Applies the ThoughtTree site template and uploads content to its Static Web App; nothing else.'
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
    assignableScopes: [for (environment, i) in environments: siteGroups[i].id]
  }
}

module identities 'deploy-identity.bicep' = [for environment in environments: {
  name: 'thoughttree-identity-${environment.name}'
  scope: identityGroup
  params: {
    name: 'id-thoughttree-${environment.name}'
    subject: 'repo:${repository}:environment:${environment.name}'
    location: location
  }
}]

module access 'site-access.bicep' = [for (environment, i) in environments: {
  name: 'thoughttree-access-${environment.name}'
  scope: siteGroups[i]
  params: {
    identityName: 'id-thoughttree-${environment.name}'
    principalId: identities[i].outputs.principalId
    roleDefinitionId: siteDeployerRole.id
  }
}]

// Values for the GitHub environment variables (infra/README.md); identifiers, not secrets.
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_SUBSCRIPTION_ID string = subscription().subscriptionId
output AZURE_CLIENT_ID_TEST string = identities[0].outputs.clientId
output AZURE_CLIENT_ID_PRODUCTION string = identities[1].outputs.clientId
output AZURE_CLIENT_ID_PREVIEW string = identities[2].outputs.clientId

// Grants one deployment identity the custom role "ThoughtTree site deployer" on one site's resource group, and
// nothing anywhere else (bootstrap.bicep).
targetScope = 'resourceGroup'

@description('Name of the managed identity; makes the assignment name stable across runs.')
param identityName string

@description('Principal id of the managed identity.')
param principalId string

@description('Full resource id of the role definition.')
param roleDefinitionId string

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, identityName, roleDefinitionId)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: roleDefinitionId
  }
}

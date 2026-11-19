// ThoughtTree hosting for one environment (ADR-046): the static client app on Azure Static Web Apps.
// Deployed by the deploy workflows with test.bicepparam or production.bicepparam; the two differ only in
// their parameter values. No telemetry or logging resource is deployed (ADR-046); the security headers
// come from staticwebapp.config.json in the client app (ADR-045).
targetScope = 'resourceGroup'

@description('Name of the environment; part of the resource name.')
@allowed([
  'test'
  'production'
])
param environmentName string

@description('Whether pull request previews (staging environments) may be created on this site.')
param previewsEnabled bool

@description('Azure region of the resource; the content itself is served from a global network.')
param location string = resourceGroup().location

resource site 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'stapp-thoughttree-${environmentName}'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
    stagingEnvironmentPolicy: previewsEnabled ? 'Enabled' : 'Disabled'
  }
  tags: {
    app: 'thoughttree'
    environment: environmentName
  }
}

output siteName string = site.name
output defaultHostname string = site.properties.defaultHostname

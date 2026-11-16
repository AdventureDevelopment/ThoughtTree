// ThoughtTree hosting for release 1: one Azure Static Web App per environment (ADR-046, ADR-047).
// Test and production use this template with different parameter files only (test.bicepparam,
// production.bicepparam); pull request previews are staging environments of the test site.
//
// Logging and telemetry (ADR-046): the template deploys no Application Insights, no Log Analytics
// workspace and no diagnostic settings. Without them Static Web Apps keeps no request logs that could
// hold IP addresses. Tests in tests/secure/ThoughtTree.Infrastructure.Rules keep it that way.
targetScope = 'resourceGroup'

@description('Environment name; test also hosts the pull request previews.')
@allowed([
  'test'
  'production'
])
param environmentName string

@description('Azure region of the site.')
param location string = resourceGroup().location

@description('Static Web Apps plan. Free allows three preview environments at a time, Standard ten.')
@allowed([
  'Free'
  'Standard'
])
param skuName string = 'Free'

@description('Whether pull request previews (staging environments) are allowed on this site.')
param allowPreviews bool

resource site 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'swa-thoughttree-${environmentName}'
  location: location
  tags: {
    app: 'thoughttree'
    environment: environmentName
  }
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    // Deployed from GitHub Actions with a token fetched at run time through OpenID Connect,
    // not through a repository link that stores a token in GitHub.
    provider: 'None'
    allowConfigFileUpdates: true
    stagingEnvironmentPolicy: allowPreviews ? 'Enabled' : 'Disabled'
    publicNetworkAccess: 'Enabled'
  }
}

output siteName string = site.name
output defaultHostname string = site.properties.defaultHostname

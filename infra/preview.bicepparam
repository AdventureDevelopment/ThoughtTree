// Preview environment: its own site, holding only the staging environments of open pull requests
// (docs/architecture.md section 7, D-51). Applied by preview-deploy.yml from main.
using 'main.bicep'

param environmentName = 'preview'
param previewsEnabled = true
param location = 'westeurope'

// Test environment: deployed after every successful build on main (docs/architecture.md section 7).
using 'main.bicep'

param environmentName = 'test'
param previewsEnabled = true
param location = 'westeurope'

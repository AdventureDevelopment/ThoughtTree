// Production environment: deployed only after the owner's manual approval (ADR-046).
using 'main.bicep'

param environmentName = 'production'
param previewsEnabled = false
param location = 'westeurope'

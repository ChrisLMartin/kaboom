using './main.bicep'

param namePrefix = 'kaboomprod'
param webAppName = 'kaboom-prod-example'
param postgresServerName = 'kaboom-prod-example-pg'
param postgresAdminLogin = 'kaboomadmin'
param postgresAdminPassword = 'replace-at-deploy-time'
param publicOrigin = 'https://kaboom-prod-example.azurewebsites.net'
param googleClientId = ''

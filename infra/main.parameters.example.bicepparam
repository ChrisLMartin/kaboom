using './main.bicep'

param namePrefix = 'kaboomdev'
param webAppName = 'kaboom-dev-example'
param postgresServerName = 'kaboom-dev-example-pg'
param postgresAdminLogin = 'kaboomadmin'
param postgresAdminPassword = 'replace-at-deploy-time'
param publicOrigin = 'https://kaboom-dev-example.azurewebsites.net'
param googleClientId = ''

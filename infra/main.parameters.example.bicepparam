using './main.bicep'

param namePrefix = 'kaboomprod'
param webAppName = 'kaboom-prod-example'
param postgresServerName = 'kaboom-prod-example-pg'
param postgresAdminLogin = 'kaboomadmin'
param publicOrigin = 'https://kaboom-prod-example.azurewebsites.net'
param googleClientId = ''

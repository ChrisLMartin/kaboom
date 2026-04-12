[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [Parameter(Mandatory = $true)]
    [string]$NamePrefix,

    [Parameter(Mandatory = $true)]
    [string]$WebAppName,

    [Parameter(Mandatory = $true)]
    [string]$PostgresServerName,

    [Parameter(Mandatory = $true)]
    [string]$PublicOrigin,

    [string]$PostgresAdminLogin = "kaboomadmin",
    [string]$GoogleClientId = "",
    [string]$GoogleClientSecret = "",
    [string]$PostgresAdminPassword = ""
)

$ErrorActionPreference = "Stop"

function New-StrongPassword {
    param([int]$Length = 32)

    $chars = @()
    $chars += 48..57
    $chars += 65..90
    $chars += 97..122
    $chars += 33,35,36,37,42,43,45,61

    return -join ($chars | Get-Random -Count $Length | ForEach-Object { [char]$_ })
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$templateFile = Join-Path $repoRoot "infra\main.bicep"

if ([string]::IsNullOrWhiteSpace($PostgresAdminPassword)) {
    $PostgresAdminPassword = New-StrongPassword
    $generatedPassword = $true
} else {
    $generatedPassword = $false
}

Write-Host "Signing into Azure if needed..." -ForegroundColor Cyan
az account show *> $null
if ($LASTEXITCODE -ne 0) {
    az login | Out-Null
}

Write-Host "Selecting subscription $SubscriptionId..." -ForegroundColor Cyan
az account set --subscription $SubscriptionId

Write-Host "Ensuring resource group $ResourceGroupName exists..." -ForegroundColor Cyan
az group create --name $ResourceGroupName --location $Location | Out-Null

Write-Host "Deploying private Azure infrastructure..." -ForegroundColor Cyan
$outputsJson = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file $templateFile `
    --parameters `
        namePrefix=$NamePrefix `
        webAppName=$WebAppName `
        postgresServerName=$PostgresServerName `
        postgresAdminLogin=$PostgresAdminLogin `
        publicOrigin=$PublicOrigin `
        googleClientId=$GoogleClientId `
        googleClientSecret=$GoogleClientSecret `
        postgresAdminPassword=$PostgresAdminPassword `
    --query properties.outputs `
    --output json

$outputs = $outputsJson | ConvertFrom-Json

Write-Host ""
Write-Host "Infrastructure deployment complete." -ForegroundColor Green
Write-Host ""
Write-Host "Azure outputs:" -ForegroundColor Yellow
Write-Host "AZURE_WEBAPP_NAME=$($outputs.webAppName.value)"
Write-Host "KABOOM_PUBLIC_ORIGIN=$($outputs.publicOrigin.value)"
Write-Host "KABOOM_POSTGRES_SERVER_NAME=$PostgresServerName"
Write-Host "KEY_VAULT_NAME=$($outputs.keyVaultName.value)"
Write-Host "KEY_VAULT_URI=$($outputs.keyVaultUri.value)"
Write-Host ""
Write-Host "Add these GitHub variables:" -ForegroundColor Yellow
Write-Host "AZURE_RESOURCE_GROUP=$ResourceGroupName"
Write-Host "AZURE_WEBAPP_NAME=$($outputs.webAppName.value)"
Write-Host "KABOOM_NAME_PREFIX=$NamePrefix"
Write-Host "KABOOM_POSTGRES_SERVER_NAME=$PostgresServerName"
Write-Host "KABOOM_PUBLIC_ORIGIN=$($outputs.publicOrigin.value)"
Write-Host "GOOGLE_CLIENT_ID=$GoogleClientId"
Write-Host ""
Write-Host "Add these GitHub secrets:" -ForegroundColor Yellow
Write-Host "POSTGRES_ADMIN_PASSWORD=$PostgresAdminPassword"
if (-not [string]::IsNullOrWhiteSpace($GoogleClientSecret)) {
    Write-Host "GOOGLE_CLIENT_SECRET=<the value you just supplied>"
}

if ($generatedPassword) {
    Write-Host ""
    Write-Host "A new PostgreSQL admin password was generated for this deployment." -ForegroundColor Yellow
    Write-Host "Store it immediately in a secure password manager and in the GitHub secret POSTGRES_ADMIN_PASSWORD." -ForegroundColor Yellow
}

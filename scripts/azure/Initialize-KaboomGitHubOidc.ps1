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
    [string]$GitHubOwner,

    [Parameter(Mandatory = $true)]
    [string]$GitHubRepository,

    [string]$GitHubBranch = "main"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$templateFile = Join-Path $repoRoot "infra\bootstrap\github-oidc.bicep"

Write-Host "Signing into Azure if needed..." -ForegroundColor Cyan
az account show *> $null
if ($LASTEXITCODE -ne 0) {
    az login | Out-Null
}

Write-Host "Selecting subscription $SubscriptionId..." -ForegroundColor Cyan
az account set --subscription $SubscriptionId

Write-Host "Ensuring resource group $ResourceGroupName exists..." -ForegroundColor Cyan
az group create --name $ResourceGroupName --location $Location | Out-Null

Write-Host "Deploying GitHub OIDC bootstrap..." -ForegroundColor Cyan
$outputsJson = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file $templateFile `
    --parameters `
        namePrefix=$NamePrefix `
        githubRepositoryOwner=$GitHubOwner `
        githubRepositoryName=$GitHubRepository `
        githubBranch=$GitHubBranch `
    --query properties.outputs `
    --output json

$outputs = $outputsJson | ConvertFrom-Json

Write-Host ""
Write-Host "Bootstrap complete." -ForegroundColor Green
Write-Host ""
Write-Host "Add these GitHub secrets:" -ForegroundColor Yellow
Write-Host "AZURE_CLIENT_ID=$($outputs.githubDeploymentClientId.value)"
Write-Host "AZURE_TENANT_ID=$($outputs.azureTenantId.value)"
Write-Host "AZURE_SUBSCRIPTION_ID=$($outputs.azureSubscriptionId.value)"
Write-Host ""
Write-Host "Keep using this resource group for infra:" -ForegroundColor Yellow
Write-Host "AZURE_RESOURCE_GROUP=$ResourceGroupName"
Write-Host ""
Write-Host "OIDC subject configured:" -ForegroundColor Yellow
Write-Host $outputs.githubOidcSubject.value

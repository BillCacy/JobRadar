# Builds/pushes the 5 service images to a container registry, then deploys main.bicep via
# main.bicepparam. PowerShell port of deploy.sh - see docs/azure-hosting.md for the full
# walkthrough.
#
# Secrets are never read from a committed file - main.bicepparam pulls every value from the
# environment via readEnvironmentVariable(), and this script loads .env into the process
# environment first (falling back to already-set environment variables) so it can supply them:
#   RESOURCE_GROUP, CONTAINER_REGISTRY, POSTGRES_ADMIN_PASSWORD (required)
#   LOCATION, IMAGE_TAG, ADZUNA_APP_ID, ADZUNA_APP_KEY, JOOBLE_API_KEY (optional)
#
# Usage (from the repo root, or anywhere):
#   .\deploy\azure\deploy.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path

# Unlike `docker compose`, neither PowerShell nor `az` auto-load .env - load it ourselves so the
# same file that configures docker-compose.yml can supply these too.
$envFile = Join-Path $repoRoot '.env'
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -eq '' -or $line.StartsWith('#')) { return }
        $idx = $line.IndexOf('=')
        if ($idx -lt 0) { return }
        $name = $line.Substring(0, $idx).Trim()
        $value = $line.Substring($idx + 1)
        [System.Environment]::SetEnvironmentVariable($name, $value)
    }
}

function Get-RequiredEnvVar([string]$name) {
    $value = [System.Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrEmpty($value)) {
        throw "Set $name in .env (or as an environment variable) first."
    }
    return $value
}

$resourceGroup = Get-RequiredEnvVar 'RESOURCE_GROUP'
$location = if ($env:LOCATION) { $env:LOCATION } else { 'eastus2' }
$containerRegistry = Get-RequiredEnvVar 'CONTAINER_REGISTRY'
$imageTag = if ($env:IMAGE_TAG) { $env:IMAGE_TAG } else { 'latest' }
# Not otherwise used in this script, but failing fast here beats spending several minutes
# building/pushing images only to have the bicepparam deployment reject a missing value.
Get-RequiredEnvVar 'POSTGRES_ADMIN_PASSWORD' | Out-Null

Set-Location $repoRoot

Write-Host "==> Building and pushing images to $containerRegistry (tag: $imageTag)"
$dockerfiles = [ordered]@{
    gateway       = 'src/Gateway/JobRadar.Gateway/Dockerfile'
    users         = 'src/Services/JobRadar.Users/Dockerfile'
    jobaggregator = 'src/Services/JobRadar.JobAggregator/Dockerfile'
    matching      = 'src/Services/JobRadar.Matching/Dockerfile'
    notifications = 'src/Services/JobRadar.Notifications/Dockerfile'
}

foreach ($svc in $dockerfiles.Keys) {
    $image = "$containerRegistry/jobradar-${svc}:${imageTag}"
    Write-Host "  - $image"
    docker build -f $dockerfiles[$svc] -t $image .
    if ($LASTEXITCODE -ne 0) { throw "docker build failed for $svc" }
    docker push $image
    if ($LASTEXITCODE -ne 0) { throw "docker push failed for $svc" }
}

Write-Host "==> Ensuring resource group $resourceGroup exists in $location"
az group create --name $resourceGroup --location $location --output none
if ($LASTEXITCODE -ne 0) { throw "az group create failed" }

Write-Host "==> Deploying main.bicep via main.bicepparam"
az deployment group create `
    --resource-group $resourceGroup `
    --template-file (Join-Path $repoRoot 'deploy/azure/main.bicep') `
    --parameters (Join-Path $repoRoot 'deploy/azure/main.bicepparam') `
    --query "properties.outputs" `
    --output json
if ($LASTEXITCODE -ne 0) { throw "az deployment group create failed" }

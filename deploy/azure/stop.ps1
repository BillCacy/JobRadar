# Pauses the deployed Azure backend to stop billing while you're not testing: scales all 5
# Container Apps to zero replicas and stops the Postgres Flexible Server. Nothing is deleted -
# run start.ps1 to bring it all back exactly as it was.
#
# Service Bus (Basic tier) and Log Analytics have no idle/compute cost, so there's nothing to
# stop for those - only Container Apps compute and Postgres compute bill while idle.
#
# Usage: .\deploy\azure\stop.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
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

$resourceGroup = $env:RESOURCE_GROUP
if ([string]::IsNullOrEmpty($resourceGroup)) {
    throw "Set RESOURCE_GROUP in .env (or as an environment variable) first."
}
$namePrefix = if ($env:NAME_PREFIX) { $env:NAME_PREFIX } else { 'jobradar' }

$apps = @(
    "$namePrefix-gateway",
    "$namePrefix-users",
    "$namePrefix-jobaggregator",
    "$namePrefix-matching",
    "$namePrefix-notifications"
)

Write-Host "==> Scaling all container apps to zero replicas in $resourceGroup"
foreach ($app in $apps) {
    Write-Host "  - $app"
    az containerapp update --resource-group $resourceGroup --name $app --min-replicas 0 --max-replicas 0 --output none
    if ($LASTEXITCODE -ne 0) { throw "Failed to scale down $app" }
}

Write-Host "==> Stopping Postgres Flexible Server"
$pgServer = az postgres flexible-server list --resource-group $resourceGroup --query "[0].name" -o tsv
if ([string]::IsNullOrEmpty($pgServer)) {
    Write-Warning "No Postgres Flexible Server found in $resourceGroup - skipping."
} else {
    Write-Host "  - $pgServer"
    az postgres flexible-server stop --resource-group $resourceGroup --name $pgServer --output none
    if ($LASTEXITCODE -ne 0) { throw "Failed to stop Postgres server $pgServer" }
}

Write-Host "==> Done. Run start.ps1 when you're ready to test again."
Write-Host "    Note: Azure auto-restarts a stopped Postgres server after 7 days regardless - if you're pausing longer than that, re-run stop.ps1 afterward."

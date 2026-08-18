# Resumes a backend paused by stop.ps1: starts the Postgres Flexible Server first (the
# container apps need it reachable on boot), then restores each Container App's normal scale
# settings. These min/max values must match the `scale` blocks in main.bicep.
#
# Usage: .\deploy\azure\start.ps1

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

$subscriptionId = az account show --query id -o tsv
if ($LASTEXITCODE -ne 0) { throw "Not logged in to Azure CLI - run 'az login' first." }

az group show --name $resourceGroup --output none
if ($LASTEXITCODE -ne 0) { throw "Resource group '$resourceGroup' not found (or you don't have access) - check RESOURCE_GROUP in .env." }

Write-Host "==> Starting Postgres Flexible Server"
$pgServer = az postgres flexible-server list --resource-group $resourceGroup --query "[0].name" -o tsv
if ([string]::IsNullOrEmpty($pgServer)) {
    Write-Warning "No Postgres Flexible Server found in $resourceGroup - skipping."
} else {
    $state = az postgres flexible-server show --resource-group $resourceGroup --name $pgServer --query "state" -o tsv
    if ($state -eq 'Ready') {
        Write-Host "  - $pgServer is already running - skipping."
    } elseif ($state -eq 'Stopped') {
        Write-Host "  - $pgServer (this can take a minute or two)"
        az postgres flexible-server start --resource-group $resourceGroup --name $pgServer --output none
        if ($LASTEXITCODE -ne 0) { throw "Failed to start Postgres server $pgServer" }
    } else {
        throw "Postgres server $pgServer is in '$state' state (expected 'Stopped' or 'Ready') - wait for it to settle and try again."
    }
}

Write-Host "==> Restoring container app scale settings"
# Must match main.bicep's scale.minReplicas/maxReplicas for each app - jobaggregator and
# notifications are pinned at exactly 1 (see docs/azure-hosting.md for why); the rest scale
# 0-2 on their own after this.
$scaleSettings = [ordered]@{
    "$namePrefix-gateway"       = @{ Min = 0; Max = 2 }
    "$namePrefix-users"         = @{ Min = 0; Max = 2 }
    "$namePrefix-jobaggregator" = @{ Min = 1; Max = 1 }
    "$namePrefix-matching"      = @{ Min = 0; Max = 2 }
    "$namePrefix-notifications" = @{ Min = 1; Max = 1 }
}

foreach ($app in $scaleSettings.Keys) {
    $s = $scaleSettings[$app]
    Write-Host "  - $app (min=$($s.Min), max=$($s.Max))"
    az containerapp update --resource-group $resourceGroup --name $app --min-replicas $s.Min --max-replicas $s.Max --output none
    if ($LASTEXITCODE -ne 0) { throw "Failed to restore scale for $app" }

    # Scale settings alone don't resume an app that was administratively stopped (e.g. via the
    # Portal's Stop button, or a previous stop.ps1 run) - that's a separate runningStatus flag
    # only reachable through this start action, not exposed as a plain `az containerapp` command.
    az rest --method post --url "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.App/containerApps/$app/start?api-version=2023-05-01" --output none
    if ($LASTEXITCODE -ne 0) { throw "Failed to start container app $app" }
}

Write-Host "==> Done. jobaggregator-service and notifications-service are pinned on and starting now;"
Write-Host "    gateway/users/matching will cold-start on the first request (a few seconds)."

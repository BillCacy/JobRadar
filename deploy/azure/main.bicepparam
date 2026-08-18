using 'main.bicep'

// Every value here is pulled from the environment at deploy time via readEnvironmentVariable -
// nothing secret is ever written to this file, so it's safe to commit. deploy.ps1/deploy.sh
// load .env into the process environment before calling `az`, which is what supplies these.
param namePrefix = readEnvironmentVariable('NAME_PREFIX', 'jobradar')
param location = readEnvironmentVariable('LOCATION', 'eastus2')
param containerRegistry = readEnvironmentVariable('CONTAINER_REGISTRY')
param imageTag = readEnvironmentVariable('IMAGE_TAG', 'latest')
param postgresAdminLogin = readEnvironmentVariable('POSTGRES_ADMIN_LOGIN', 'jobradar')
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
param adzunaAppId = readEnvironmentVariable('ADZUNA_APP_ID', '')
param adzunaAppKey = readEnvironmentVariable('ADZUNA_APP_KEY', '')
param adzunaCountry = readEnvironmentVariable('ADZUNA_COUNTRY', 'us')
param joobleApiKey = readEnvironmentVariable('JOOBLE_API_KEY', '')
param aggregatorPollIntervalMinutes = int(readEnvironmentVariable('AGGREGATOR_POLL_INTERVAL_MINUTES', '10'))

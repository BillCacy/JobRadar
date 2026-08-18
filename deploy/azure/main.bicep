// Deploys JobRadar's backend to Azure Container Apps at (near) the lowest cost that keeps
// every feature working - see docs/azure-hosting.md for the full component-by-component
// rationale and cost breakdown. Local development still uses docker-compose.yml (RabbitMQ,
// self-hosted Postgres); this template is the cloud counterpart, not a replacement for it.

@description('Short name used as a prefix for every resource. Keep it lowercase/short - it feeds into globally-unique names (Service Bus namespace, Postgres server).')
param namePrefix string = 'jobradar'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Container registry host + namespace images are pulled from, e.g. ghcr.io/yourusername. Use a public GHCR repo to avoid Azure Container Registry\'s ~$5/month Basic tier cost.')
param containerRegistry string

@description('Image tag to deploy for all five services.')
param imageTag string = 'latest'

@description('Postgres Flexible Server admin username.')
param postgresAdminLogin string = 'jobradar'

@secure()
@description('Postgres Flexible Server admin password.')
param postgresAdminPassword string

@description('Adzuna API app ID (leave blank to skip that connector).')
param adzunaAppId string = ''

@secure()
@description('Adzuna API app key (leave blank to skip that connector).')
param adzunaAppKey string = ''

param adzunaCountry string = 'us'

@secure()
@description('Jooble API key (leave blank to skip that connector).')
param joobleApiKey string = ''

param aggregatorPollIntervalMinutes int = 10

var uniqueSuffix = uniqueString(resourceGroup().id)
var containerCpu = json('0.25')
var containerMemory = '0.5Gi'

// ---------------------------------------------------------------------------
// Container Apps Environment
// ---------------------------------------------------------------------------

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${namePrefix}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${namePrefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Service Bus (Basic tier: no monthly minimum, queues only - see docs/azure-hosting.md for
// why the messaging code sends point-to-point to these queues instead of publishing to topics.)
// ---------------------------------------------------------------------------

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: '${namePrefix}-sb-${uniqueSuffix}'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

resource criteriaEventsQueue 'Microsoft.ServiceBus/namespaces/queues@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'jobaggregator-criteria-events'
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
  }
}

resource jobsFetchedQueue 'Microsoft.ServiceBus/namespaces/queues@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'matching-jobsfetched'
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
  }
}

resource jobMatchedQueue 'Microsoft.ServiceBus/namespaces/queues@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'notifications-jobmatched'
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
  }
}

var serviceBusConnectionString = listKeys('${serviceBusNamespace.id}/AuthorizationRules/RootManageSharedAccessKey', '2021-11-01').primaryConnectionString

// ---------------------------------------------------------------------------
// Postgres Flexible Server (Burstable B1ms - cheapest tier that still gives a real managed
// Postgres instance; self-hosting Postgres in a container costs about the same once you price
// the always-on compute, with none of the managed backups).
// ---------------------------------------------------------------------------

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: '${namePrefix}-pg-${uniqueSuffix}'
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: { storageSizeGB: 32 }
    backup: { backupRetentionDays: 7, geoRedundantBackup: 'Disabled' }
    highAvailability: { mode: 'Disabled' }
  }
}

// Public access restricted to Azure-hosted resources (the special 0.0.0.0-0.0.0.0 rule), rather
// than full VNet integration - keeps the template simple for a portfolio deploy. Tighten this to
// private VNet access before this ever holds real user data.
resource postgresFirewallAllowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource usersDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: 'usersdb'
}

resource aggregatorDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: 'aggregatordb'
}

resource matchingDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: 'matchingdb'
}

resource notificationsDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: 'notificationsdb'
}

// Bicep user-defined functions can't reference resources or secure params, so these are plain
// vars instead of a single pgConnectionString(database) helper.
var usersDbConnectionString = 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=usersdb;Username=${postgresAdminLogin};Password=${postgresAdminPassword};Ssl Mode=Require'
var aggregatorDbConnectionString = 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=aggregatordb;Username=${postgresAdminLogin};Password=${postgresAdminPassword};Ssl Mode=Require'
var matchingDbConnectionString = 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=matchingdb;Username=${postgresAdminLogin};Password=${postgresAdminPassword};Ssl Mode=Require'
var notificationsDbConnectionString = 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=notificationsdb;Username=${postgresAdminLogin};Password=${postgresAdminPassword};Ssl Mode=Require'

// ---------------------------------------------------------------------------
// Container Apps - one per service, plus the YARP gateway
// ---------------------------------------------------------------------------

resource usersServiceApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${namePrefix}-users'
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: { external: false, targetPort: 8080, transport: 'auto' }
      secrets: [
        { name: 'db-connection', value: usersDbConnectionString }
        { name: 'servicebus-connection', value: serviceBusConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: 'users-service'
          image: '${containerRegistry}/jobradar-users:${imageTag}'
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ConnectionStrings__UsersDb', secretRef: 'db-connection' }
            { name: 'Messaging__Transport', value: 'AzureServiceBus' }
            { name: 'AzureServiceBus__ConnectionString', secretRef: 'servicebus-connection' }
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          ]
        }
      ]
      // Plain HTTP CRUD API with no persistent state of its own - safe to scale to zero
      // between requests and back up on the next one.
      scale: { minReplicas: 0, maxReplicas: 2 }
    }
  }
  dependsOn: [ usersDb ]
}

resource jobAggregatorApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${namePrefix}-jobaggregator'
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: { external: false, targetPort: 8080, transport: 'auto' }
      secrets: [
        { name: 'db-connection', value: aggregatorDbConnectionString }
        { name: 'servicebus-connection', value: serviceBusConnectionString }
        { name: 'adzuna-app-key', value: adzunaAppKey }
        { name: 'jooble-api-key', value: joobleApiKey }
      ]
    }
    template: {
      containers: [
        {
          name: 'jobaggregator-service'
          image: '${containerRegistry}/jobradar-jobaggregator:${imageTag}'
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ConnectionStrings__AggregatorDb', secretRef: 'db-connection' }
            { name: 'Messaging__Transport', value: 'AzureServiceBus' }
            { name: 'AzureServiceBus__ConnectionString', secretRef: 'servicebus-connection' }
            { name: 'Aggregator__PollIntervalMinutes', value: string(aggregatorPollIntervalMinutes) }
            { name: 'Adzuna__AppId', value: adzunaAppId }
            { name: 'Adzuna__AppKey', secretRef: 'adzuna-app-key' }
            { name: 'Adzuna__Country', value: adzunaCountry }
            { name: 'Jooble__ApiKey', secretRef: 'jooble-api-key' }
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          ]
        }
      ]
      // Pinned at exactly one replica: the poll loop is an in-process Quartz.NET timer, not an
      // external trigger, so scaling to zero would stop polling entirely, and scaling out would
      // poll (and publish) every active watch multiple times over.
      scale: { minReplicas: 1, maxReplicas: 1 }
    }
  }
  dependsOn: [ aggregatorDb ]
}

resource matchingServiceApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${namePrefix}-matching'
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: { external: false, targetPort: 8080, transport: 'auto' }
      secrets: [
        { name: 'db-connection', value: matchingDbConnectionString }
        { name: 'servicebus-connection', value: serviceBusConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: 'matching-service'
          image: '${containerRegistry}/jobradar-matching:${imageTag}'
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ConnectionStrings__MatchingDb', secretRef: 'db-connection' }
            { name: 'Messaging__Transport', value: 'AzureServiceBus' }
            { name: 'AzureServiceBus__ConnectionString', secretRef: 'servicebus-connection' }
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          ]
        }
      ]
      // Pure queue consumer with no inbound HTTP traffic of its own - KEDA wakes it from zero
      // replicas when a message lands on matching-jobsfetched instead of paying for idle time.
      scale: {
        minReplicas: 0
        maxReplicas: 2
        rules: [
          {
            name: 'servicebus-queue-rule'
            custom: {
              type: 'azure-servicebus'
              metadata: {
                queueName: 'matching-jobsfetched'
                namespace: serviceBusNamespace.name
                messageCount: '5'
              }
              auth: [
                { secretRef: 'servicebus-connection', triggerParameter: 'connection' }
              ]
            }
          }
        ]
      }
    }
  }
  dependsOn: [ matchingDb ]
}

resource notificationsServiceApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${namePrefix}-notifications'
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: { external: false, targetPort: 8080, transport: 'auto' }
      secrets: [
        { name: 'db-connection', value: notificationsDbConnectionString }
        { name: 'servicebus-connection', value: serviceBusConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: 'notifications-service'
          image: '${containerRegistry}/jobradar-notifications:${imageTag}'
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ConnectionStrings__NotificationsDb', secretRef: 'db-connection' }
            { name: 'Messaging__Transport', value: 'AzureServiceBus' }
            { name: 'AzureServiceBus__ConnectionString', secretRef: 'servicebus-connection' }
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          ]
        }
      ]
      // Pinned at exactly one replica: the MAUI client's SignalR connections live in-memory on
      // whichever instance accepted them, and there's no backplane (e.g. Azure SignalR Service,
      // Redis) to fan a push out across replicas - see docs/azure-hosting.md.
      scale: { minReplicas: 1, maxReplicas: 1 }
    }
  }
  dependsOn: [ notificationsDb ]
}

resource gatewayApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${namePrefix}-gateway'
  location: location
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: { external: true, targetPort: 8080, transport: 'auto' }
    }
    template: {
      containers: [
        {
          name: 'gateway'
          image: '${containerRegistry}/jobradar-gateway:${imageTag}'
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            // Overrides the docker-compose-local hostnames baked into appsettings.json with
            // this environment's actual internal Container Apps FQDNs.
            { name: 'ReverseProxy__Clusters__users-cluster__Destinations__destination1__Address', value: 'https://${usersServiceApp.name}.internal.${containerAppsEnvironment.properties.defaultDomain}' }
            { name: 'ReverseProxy__Clusters__notifications-cluster__Destinations__destination1__Address', value: 'https://${notificationsServiceApp.name}.internal.${containerAppsEnvironment.properties.defaultDomain}' }
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          ]
        }
      ]
      // The one externally-reachable app - safe to scale to zero the same as any idle HTTP API.
      scale: { minReplicas: 0, maxReplicas: 2 }
    }
  }
  // No explicit dependsOn needed - referencing usersServiceApp.name/notificationsServiceApp.name
  // above already makes Bicep infer the dependency.
}

output gatewayUrl string = 'https://${gatewayApp.properties.configuration.ingress.fqdn}'
output postgresServerFqdn string = postgresServer.properties.fullyQualifiedDomainName
output serviceBusNamespaceName string = serviceBusNamespace.name

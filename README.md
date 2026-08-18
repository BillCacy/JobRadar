# JobRadar

A small event-driven microservices system that watches job sites for postings matching a
user's saved search criteria and pushes a live alert the moment one shows up — built as a
portfolio piece to demonstrate ASP.NET Core Web API, microservices, and real-time (SignalR)
patterns end to end, with a .NET MAUI client for mobile + desktop.

See [`docs/architecture.md`](docs/architecture.md) for the full diagram and the reasoning
behind each design choice, and [`docs/azure-hosting.md`](docs/azure-hosting.md) for deploying
the backend to Azure Container Apps. This file is just setup + how to poke at it.

## What's here

| Service | Responsibility | Talks to the outside world? |
|---|---|---|
| `JobRadar.Gateway` | YARP reverse proxy — the one address the client calls | Yes (port 8080) |
| `JobRadar.Users` | Accounts (email-only, no password) + saved search CRUD | Via gateway only |
| `JobRadar.JobAggregator` | Polls Adzuna / Jooble on a timer, publishes fetched postings | No public API — pure event producer |
| `JobRadar.Matching` | Applies per-user filters, dedupes, decides what's a real match | No public API — pure event consumer/producer |
| `JobRadar.Notifications` | Stores notification history, pushes live alerts over SignalR | Via gateway only |
| `JobRadar.Contracts` | Shared DTOs + integration events referenced by every service | (class library, not a service) |
| `JobRadar.App` | .NET MAUI client — Android / iOS / Mac Catalyst / Windows from one codebase | n/a |

Backend services talk to each other exclusively through RabbitMQ events (via MassTransit) or
by owning their own local copy of the data they need — never by calling each other's HTTP APIs.
See the architecture doc for why.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) + Docker Compose (for the backend)
- [.NET 8 SDK](https://dotnet.microsoft.com/download) if you want to run/debug a backend service
  outside Docker (all five services + `JobRadar.Contracts` target `net8.0`)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) + the `maui` workload
  (`dotnet workload install maui`) for the MAUI client — `net8.0-android`/`-ios`/`-maccatalyst`
  are past their support window and current SDKs refuse to build them (`NETSDK1202`), so
  `JobRadar.App` targets `net10.0-*` instead
- A free [Adzuna](https://developer.adzuna.com/) `app_id` / `app_key` — required
- A free [Jooble](https://jooble.org/api/about) API key — optional, second connector

## Quick start (backend)

```bash
cp .env.example .env
# edit .env: paste in ADZUNA_APP_ID and ADZUNA_APP_KEY at minimum

docker compose up --build
```

That builds and starts all five services plus Postgres and RabbitMQ. First build takes a few
minutes (restoring NuGet packages per service); subsequent starts are fast. When it settles:

- Gateway: `http://localhost:8080`
- RabbitMQ management UI: `http://localhost:15672` (user/pass from `.env`, default `jobradar` / `jobradar`)
- Each service also exposes `/health` if you want to check it came up (through the gateway
  those are `/health` on the gateway itself, `docker compose ps` is the easiest overall check)

### Try it without the client

```bash
# 1. Register (really just "give me a UserId for this email")
curl -s -X POST http://localhost:8080/api/users \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","displayName":"You"}'
# → { "id": "…", "email": "you@example.com", "displayName": "You" }
# copy the id for the next steps

# 2. Save a search (replace USER_ID)
curl -s -X POST http://localhost:8080/api/criteria \
  -H "Content-Type: application/json" -H "X-User-Id: USER_ID" \
  -d '{"keywords":"senior .net developer","location":"remote","remoteOnly":true}'

# 3. Wait for a poll cycle (Aggregator:PollIntervalMinutes in .env, default 10 minutes — turn
#    it down to 1 in .env for testing) then check for matches:
curl -s http://localhost:8080/api/notifications -H "X-User-Id: USER_ID"
```

To watch it work in real time instead of polling the notifications endpoint, run the MAUI
client (below) and leave the Job Feed tab open — matches appear the instant Notifications
pushes them over SignalR.

## Running the MAUI client

The client's application code (ViewModels, Services, Views, csproj) lives in
`src/Client/JobRadar.App`, but the platform-specific boilerplate (`Platforms/*`, app icons) is
left for `dotnet new maui` to generate rather than hand-copied — see
[`src/Client/JobRadar.App/SETUP.md`](src/Client/JobRadar.App/SETUP.md) for the exact steps.
Short version: scaffold with the CLI template in that folder, drop these files on top, `dotnet
add package` the two extra NuGet dependencies, then run against whichever platform you're
targeting. Make sure the backend (`docker compose up`) is already running first.

**Note on where things were built:** the backend was written and sanity-checked (project
structure, JSON/XML well-formedness, brace balance) in an environment without the .NET SDK or
Docker daemon available, so none of it has actually been compiled or run yet. Treat this as a
solid, carefully-considered starting point rather than a guarantee it builds clean on the first
`docker compose up` — read through the code as you bring it up, and expect the normal amount of
first-run friction (a missing package version, a typo) you'd get scaffolding any new solution.

## Opening the whole thing in an IDE

```bash
./scripts/create-solution.sh
```

Generates `JobRadar.slnx` with every project added, for Visual Studio / Rider / VS Code — just
open the file and the IDE picks it up.

### Building and running from the CLI instead

Once `JobRadar.slnx` exists, the `dotnet` CLI works against it directly (requires the .NET 8
SDK):

```bash
# Restore + build every project in the solution
dotnet build JobRadar.slnx

# Run all tests in the solution (if/when test projects are added)
dotnet test JobRadar.slnx
```

`dotnet build`/`dotnet test` build the whole solution at once, but each service is still its own
runnable app — `dotnet run` needs a single project, not the solution, so run services
individually when you want one up without Docker:

```bash
dotnet run --project src/Services/JobRadar.Users/JobRadar.Users.csproj
dotnet run --project src/Gateway/JobRadar.Gateway/JobRadar.Gateway.csproj
# etc. — swap in whichever service's .csproj you want
```

Note these run with whatever's in each service's `appsettings.json`/environment, not the
`.env`-driven config `docker compose up` provides — you'll need Postgres and RabbitMQ reachable
(e.g. `docker compose up postgres rabbitmq`) and matching connection strings if you run services
this way instead of through Compose.

## Adding a third job connector

1. Add a class in `src/Services/JobRadar.JobAggregator/Connectors/` implementing
   `IJobConnector` (`Name`, `IsConfigured`, `SearchAsync`) — see `AdzunaConnector.cs` for the
   shape.
2. Register it in `JobRadar.JobAggregator/Program.cs` next to the existing two
   (`AddHttpClient<...>()` + `AddScoped<IJobConnector, ...>()`).
3. Add whatever config it needs (API key, etc.) to `appsettings.json` and `docker-compose.yml`'s
   environment block for `jobaggregator-service`.

Nothing in Matching, Notifications, the Gateway, or the client needs to change — they only ever
see the normalized `JobPosting` shape from `JobRadar.Contracts`.

## Repo layout

```
JobRadar/
├── docker-compose.yml
├── .env.example
├── deploy/postgres-init/        # creates one DB per service on first run
├── deploy/azure/                # Bicep + deploy script for Azure Container Apps
├── docs/architecture.md
├── docs/azure-hosting.md
├── scripts/create-solution.sh
└── src/
    ├── Shared/JobRadar.Contracts/     # DTOs + integration events
    ├── Gateway/JobRadar.Gateway/      # YARP
    ├── Services/
    │   ├── JobRadar.Users/
    │   ├── JobRadar.JobAggregator/
    │   ├── JobRadar.Matching/
    │   └── JobRadar.Notifications/
    └── Client/JobRadar.App/           # .NET MAUI
```

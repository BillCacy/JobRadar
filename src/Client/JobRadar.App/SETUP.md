# Setting up the MAUI client locally

This folder is the *application layer* of the client (ViewModels, Services, Views, the
.csproj) — deliberately **not** the platform boilerplate (`Platforms/Android`, `Platforms/iOS`,
`Platforms/MacCatalyst`, `Platforms/Windows`, `Resources/AppIcon`, etc.). That boilerplate is
large, mostly mechanical, version-pinned to whatever MAUI workload you have installed, and
`dotnet` (or Visual Studio) generates it correctly for you — hand-copying it tends to go stale
and cause confusing build errors, so it's better to let the tooling own it. Also worth knowing
up front: none of this MAUI code has been build-verified in the environment that generated it
(no .NET SDK / MAUI workload was available there) — treat it as a solid starting scaffold to
compile and shake out on your own machine, not a guarantee it builds byte-for-byte as written.

1. Install the workload (one-time): `dotnet workload install maui`
2. Scaffold a fresh MAUI project in this exact spot so the platform folders and resources get
   generated for you:
   ```
   cd src/Client
   dotnet new maui -n JobRadar.App --force
   ```
   `--force` lets it write into the already-existing `JobRadar.App` folder without complaining
   about the files already here (SETUP.md, this README).
3. The template will generate its own `MauiProgram.cs`, `App.xaml(.cs)`, `AppShell.xaml(.cs)`,
   and a default `MainPage.xaml`. Delete its `MainPage.xaml(.cs)` and overwrite `MauiProgram.cs`,
   `App.xaml(.cs)`, `AppShell.xaml(.cs)` with the versions in this folder — they wire up DI,
   navigation, and the gateway/SignalR services described in the main README.
4. Add the NuGet packages this project needs on top of the template defaults:
   ```
   dotnet add package Microsoft.AspNetCore.SignalR.Client
   dotnet add package CommunityToolkit.Mvvm
   ```
5. Add a project reference to the shared contracts so the client uses the exact same
   `SearchCriteria` / `JobTypeFilter` types as the backend:
   ```
   dotnet add reference ../../Shared/JobRadar.Contracts/JobRadar.Contracts.csproj
   ```
6. Run the backend first (`docker compose up --build` from the repo root), then run the app
   (`dotnet build -t:Run -f net10.0-windows10.0.19041.0` or F5 from Visual Studio / Rider
   targeting Windows, Mac Catalyst, or an Android emulator). Note the `TargetFrameworks` here
   are `net10.0-*`, not `net8.0-*` — the mobile `net8.0` targets are past their support window
   and current SDKs refuse to build them (`NETSDK1202`), so make sure `dotnet new maui` scaffolds
   with `-f net10.0` (the CLI defaults to whatever's newest, which should already match).
7. **Android emulator only**: `GatewayConfig.cs` already points at `10.0.2.2:8080` instead of
   `localhost` for Android, since the emulator can't see the host machine as "localhost" — see
   the comment in that file. iOS simulator, Mac Catalyst, and Windows all reach `localhost`
   directly. A physical device needs your machine's real LAN IP instead of either.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/JobRadar.Contracts/JobRadar.Contracts.csproj Shared/JobRadar.Contracts/
COPY src/Services/JobRadar.JobAggregator/JobRadar.JobAggregator.csproj Services/JobRadar.JobAggregator/
RUN dotnet restore Services/JobRadar.JobAggregator/JobRadar.JobAggregator.csproj

COPY src/Shared/JobRadar.Contracts/ Shared/JobRadar.Contracts/
COPY src/Services/JobRadar.JobAggregator/ Services/JobRadar.JobAggregator/
RUN dotnet publish Services/JobRadar.JobAggregator/JobRadar.JobAggregator.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "JobRadar.JobAggregator.dll"]

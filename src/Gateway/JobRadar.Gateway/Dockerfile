FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Gateway/JobRadar.Gateway/JobRadar.Gateway.csproj Gateway/JobRadar.Gateway/
RUN dotnet restore Gateway/JobRadar.Gateway/JobRadar.Gateway.csproj

COPY src/Gateway/JobRadar.Gateway/ Gateway/JobRadar.Gateway/
RUN dotnet publish Gateway/JobRadar.Gateway/JobRadar.Gateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "JobRadar.Gateway.dll"]

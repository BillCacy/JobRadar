FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/JobRadar.Contracts/JobRadar.Contracts.csproj Shared/JobRadar.Contracts/
COPY src/Services/JobRadar.Matching/JobRadar.Matching.csproj Services/JobRadar.Matching/
RUN dotnet restore Services/JobRadar.Matching/JobRadar.Matching.csproj

COPY src/Shared/JobRadar.Contracts/ Shared/JobRadar.Contracts/
COPY src/Services/JobRadar.Matching/ Services/JobRadar.Matching/
RUN dotnet publish Services/JobRadar.Matching/JobRadar.Matching.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "JobRadar.Matching.dll"]

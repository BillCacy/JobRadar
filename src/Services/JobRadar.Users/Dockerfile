FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Shared/JobRadar.Contracts/JobRadar.Contracts.csproj Shared/JobRadar.Contracts/
COPY src/Services/JobRadar.Users/JobRadar.Users.csproj Services/JobRadar.Users/
RUN dotnet restore Services/JobRadar.Users/JobRadar.Users.csproj

COPY src/Shared/JobRadar.Contracts/ Shared/JobRadar.Contracts/
COPY src/Services/JobRadar.Users/ Services/JobRadar.Users/
RUN dotnet publish Services/JobRadar.Users/JobRadar.Users.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "JobRadar.Users.dll"]

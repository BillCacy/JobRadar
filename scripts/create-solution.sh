#!/usr/bin/env bash
# One-time convenience: generates JobRadar.sln and adds every project to it. Not required for
# `docker compose up` (each Dockerfile builds its own .csproj directly) - this is only for
# opening the whole thing in Visual Studio / Rider / VS Code as a single solution.
set -e
cd "$(dirname "$0")/.."

dotnet new sln -n JobRadar --force
find src -name "*.csproj" -print0 | xargs -0 -n1 dotnet sln JobRadar.slnx add

echo "Done. Open JobRadar.slnx in your IDE of choice."

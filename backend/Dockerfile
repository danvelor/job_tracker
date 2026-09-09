# syntax=docker/dockerfile:1

# Restore before the rest of the source, so editing a file does not re-download
# every package. Project files are what invalidate a restore; code is not.
#
# It copies src/ wholesale rather than each .csproj separately. The
# finer-grained version caches better and costs one COPY line per project —
# eleven of them today, each going stale the moment a project moves. The trade
# is deliberate: a rebuild after a source edit re-restores, and that is a few
# seconds against a line nobody remembers to update.
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props JobTracker.sln ./
COPY src/ ./src/

RUN dotnet restore src/Api/JobTracker.Api/JobTracker.Api.csproj

RUN dotnet publish src/Api/JobTracker.Api/JobTracker.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Not root. This container never writes to its own filesystem, so it should not
# be able to. The base image already ships a non-root `app` user (uid 1654) —
# creating another one fails, which is how this line was found.
USER app

COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "JobTracker.Api.dll"]

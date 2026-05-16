# syntax=docker/dockerfile:1.7
ARG DOTNET_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Copy solution + projects first for layer caching.
COPY ProjectJA.sln global.json ./
COPY src/ src/
COPY tools/ tools/
COPY tests/ tests/

RUN dotnet restore ProjectJA.sln

# Host (the web app).
RUN dotnet publish src/ProjectJA.Host/ProjectJA.Host.csproj \
    -c Release -o /out/host --no-restore /p:UseAppHost=false

# Migrator (per-tenant migration runner; invoked from the deploy script).
RUN dotnet publish tools/Migrator/ProjectJA.Tools.Migrator.csproj \
    -c Release -o /out/migrator --no-restore /p:UseAppHost=false

# Provisioning CLI is small enough to ship in the same image for ad-hoc admin use.
RUN dotnet publish tools/Provisioning/ProjectJA.Tools.Provisioning.csproj \
    -c Release -o /out/provisioning --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /out/host         ./host/
COPY --from=build /out/migrator     ./migrator/
COPY --from=build /out/provisioning ./provisioning/

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Default: run the web app. Override at `docker run`/`docker compose run` time to
# invoke the migrator: `dotnet migrator/ProjectJA.Tools.Migrator.dll`
ENTRYPOINT ["dotnet", "host/ProjectJA.Host.dll"]

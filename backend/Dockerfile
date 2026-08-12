# Prism API — multi-stage build.
#
# Build context is ./backend (see docker-compose.yml), so paths below are
# relative to the backend directory, not the repo root.
#
# The SDK 9.0 image builds the net9.0 target directly. The sandbox-only
# targeting-pack pin (Directory.Build.targets) is inert unless
# PRISM_TARGETING_PACK_VERSION is set, which it is not here — a normal
# internet-backed restore resolves the versions the SDK asks for.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore against just the project graph first, so a source-only change does
# not re-download every package. The solution references only projects under
# src/, so copying those csproj files is enough to restore.
COPY Prism.sln Directory.Build.props Directory.Build.targets ./
COPY src/Prism.Api/Prism.Api.csproj src/Prism.Api/
COPY src/Prism.Common/Prism.Common.csproj src/Prism.Common/
COPY src/Prism.Features/Prism.Features.csproj src/Prism.Features/
COPY src/Prism.Tests/Prism.Tests.csproj src/Prism.Tests/
RUN dotnet restore src/Prism.Api/Prism.Api.csproj

# Now the sources, and publish the API.
COPY src/ src/
RUN dotnet publish src/Prism.Api/Prism.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Bind to all interfaces inside the container; the host port mapping is in
# docker-compose.yml. Migrations, seeding and Swagger are gated on Development,
# and dev.sh runs this for local development, so it defaults to Development —
# a production deployment overrides ASPNETCORE_ENVIRONMENT.
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Development
EXPOSE 5000

# No in-container HEALTHCHECK: the aspnet runtime image ships without curl or
# wget, so a shell-based probe would report the API permanently unhealthy. dev.sh
# waits on GET /health from the host, which is the readiness gate that matters.

ENTRYPOINT ["dotnet", "Prism.Api.dll"]

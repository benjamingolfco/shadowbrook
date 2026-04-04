# Dockerfile for Shadowbrook API
# API-only — frontend deploys separately via Azure Static Web Apps

# Dev stage: hot-reload with dotnet watch + vsdbg for debugging
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /app
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY src/backend/ ./
RUN dotnet restore Shadowbrook.Api/Shadowbrook.Api.csproj
WORKDIR /app/Shadowbrook.Api
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "watch", "run", "--non-interactive", "--no-launch-profile"]

# Stage 1: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy and restore
COPY src/backend/ ./
RUN dotnet restore Shadowbrook.Api/Shadowbrook.Api.csproj

# Pre-generate Wolverine handler code (Static mode). No DB required —
# transports and persistence are disabled during codegen.
RUN dotnet run --project Shadowbrook.Api/Shadowbrook.Api.csproj --no-launch-profile -- codegen write

# Build and publish API (framework-dependent deployment)
RUN dotnet publish Shadowbrook.Api/Shadowbrook.Api.csproj \
    -c Release \
    -o /app/publish \
    --self-contained false

# Stage 2: Final runtime image
# noble-chiseled-extra: Ubuntu minimal image with ICU globalization libraries.
# SQL Server's EF Core provider requires ICU — plain noble-chiseled crashes with
# "Globalization Invariant Mode is not supported" on connection open.
# No shell, no package manager — runs as non-root (UID 1654) by default.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app

# Copy published API
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

# Start the application
ENTRYPOINT ["dotnet", "Shadowbrook.Api.dll"]

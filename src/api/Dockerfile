# Dockerfile for Shadowbrook API
# API-only — frontend deploys separately via Azure Static Web Apps

# Dev stage: hot-reload with dotnet watch + vsdbg for debugging
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /app
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY src/api/Shadowbrook.Api.csproj ./
RUN dotnet restore
COPY src/api/ ./
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

# Copy project files
COPY src/api/Shadowbrook.Api.csproj ./
RUN dotnet restore Shadowbrook.Api.csproj

# Copy source code
COPY src/api/ ./

# Build and publish API (framework-dependent deployment)
RUN dotnet publish Shadowbrook.Api.csproj \
    -c Release \
    -o /app/publish \
    --self-contained false

# Stage 2: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Copy published API
COPY --from=build /app/publish .

# Create non-root user
RUN useradd -r -s /bin/false appuser && \
    chown -R appuser:appuser /app

USER appuser

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Start the application
ENTRYPOINT ["dotnet", "Shadowbrook.Api.dll"]

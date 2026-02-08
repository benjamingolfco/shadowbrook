# Multi-stage Dockerfile for Shadowbrook dev environment
# Builds React frontend and .NET API in a single container

# Stage 1: Build React frontend
FROM node:22-alpine AS frontend-build
WORKDIR /app/web

# Install pnpm
RUN corepack enable && corepack prepare pnpm@latest --activate

# Copy package files
COPY src/web/package.json src/web/pnpm-lock.yaml ./

# Install dependencies
RUN pnpm install --frozen-lockfile

# Copy frontend source
COPY src/web/ ./

# Build frontend
RUN pnpm build

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
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
    --self-contained false \
    --no-restore

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Install curl for health checks
RUN apk add --no-cache curl

# Copy published API
COPY --from=api-build /app/publish .

# Copy frontend build to wwwroot
COPY --from=frontend-build /app/web/dist ./wwwroot

# Create non-root user
RUN addgroup -g 1000 appuser && \
    adduser -u 1000 -G appuser -s /bin/sh -D appuser && \
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

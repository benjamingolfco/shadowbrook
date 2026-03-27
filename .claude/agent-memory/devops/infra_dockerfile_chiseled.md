---
name: Dockerfile runtime image — noble-chiseled
description: Runtime stage uses aspnet:10.0-noble-chiseled; no curl, no shell, no useradd needed
type: project
---

The production runtime stage in `src/backend/Shadowbrook.Api/Dockerfile` was switched from `mcr.microsoft.com/dotnet/aspnet:10.0` (Debian, ~215MB) to `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` (~100MB) on 2026-03-26.

**Implications for future Dockerfile edits:**
- No shell (`/bin/sh`, `/bin/bash`) available in runtime stage — do not add RUN commands after FROM
- No curl — do not add curl-based HEALTHCHECK instructions; health probes are defined in the Bicep container-app module
- No APT — do not add `apt-get` calls; if a native dependency is needed, reconsider the base image
- Runs as non-root UID 1654 by default — no need for `useradd`/`USER` directives
- The dev stage still uses `mcr.microsoft.com/dotnet/sdk:10.0` (Debian) and installs curl for vsdbg — this is intentional and correct

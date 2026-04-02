---
name: how-tos:start-local-dev
description: Use when you need to start the local development environment from scratch
---

# Start Local Dev Environment

## Prerequisites
- Docker running (for SQL Server container)
- .NET 10 SDK installed
- Node.js + pnpm installed

## Steps
1. Start the DB container: `docker compose up db -d` (from repo root)
2. Start the API natively with dev auth:
   ```bash
   cd /path/to/shadowbrook
   Auth__UseDevAuth=true dotnet run --project src/backend/Shadowbrook.Api -- --urls "http://localhost:5221"
   ```
3. Start the frontend: `pnpm --dir src/web dev`
4. Verify API: `curl http://localhost:5221/health` should return `Healthy`
5. Verify frontend: Open `http://localhost:3000`

## Seeding Dev Users
After first startup on a fresh DB, seed at minimum an admin user directly via SQL:
```sql
USE Shadowbrook;
INSERT INTO dbo.AppUsers (Id, IdentityId, Email, FirstName, LastName, Role, OrganizationId, IsActive, CreatedAt, UpdatedAt)
VALUES (NEWID(), 'dev-admin-oid', 'dev-admin@shadowbrook.golf', 'Dev', 'Admin', 'Admin', NULL, 1, GETUTCDATE(), GETUTCDATE());
```

The frontend `.env.development.local` must contain:
```
VITE_USE_DEV_AUTH=true
VITE_DEV_IDENTITY_ID=dev-admin-oid
```

## Notes
- `make dev` uses Docker for both API and DB — but the Docker API container uses an old image and does NOT have `Auth__UseDevAuth=true` by default
- Running the API natively (`dotnet run`) with the dev auth env var is the recommended approach for local QA
- If migrations fail with "DROP COLUMN [DisplayName] failed": the LocalQaFixes migration is a no-op for fresh DBs — drop and recreate the database, then restart


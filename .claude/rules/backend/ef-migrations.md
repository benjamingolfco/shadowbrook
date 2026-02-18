---
paths:
  - "src/api/Migrations/**"
  - "src/api/Data/ApplicationDbContext.cs"
---

# EF Core Migrations

## Development Workflow

All environments (dev, staging, production) use SQL Server and `Database.Migrate()` to apply migrations at startup. Local dev requires `docker compose up db -d` (or `make db`) for the SQL Server container. Tests use SQLite in-memory with `EnsureCreated()` for speed.

### Adding a Migration

```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations add <Name> --project src/api
```

### Naming Conventions

Use PascalCase descriptive names that read like commit messages:

| Change | Name |
|--------|------|
| New entity | `AddWaitlistEntry` |
| New column | `AddPhoneToGolferProfile` |
| Index change | `AddIndexOnBookingDate` |
| Relationship | `AddCourseToTenantRelationship` |
| Data migration | `BackfillDefaultTeeTimeIntervals` |

Avoid generic names like `Update1`, `Changes`, or `FixStuff`.

### After Adding a Migration

1. **Review the generated code** — check `Up()` and `Down()` for correctness
2. **Build**: `dotnet build shadowbrook.slnx`
3. **Run tests**: `dotnet test` (tests use `EnsureCreated`, so they validate the model independent of migrations)
4. **Restart the API** to verify the migration applies cleanly (drop the local DB first if testing from scratch)

### Checking for Pending Changes

```bash
dotnet ef migrations has-pending-model-changes --project src/api
```

## Squashing Migrations

While pre-production (no deployed database with real data), squash freely to keep the migration list clean:

1. Delete the `src/api/Migrations/` folder
2. Drop the local Shadowbrook database in SQL Server
3. Run `dotnet ef migrations add InitialCreate --project src/api`
4. Verify: `dotnet build && dotnet test`

**Once a production database exists with real data, treat migrations as immutable history.** To squash after that point, follow the [official reset procedure](https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing#resetting-all-migrations) which involves manipulating `__EFMigrationsHistory`.

## Production Deployment (Future)

When deploying to Azure, switch from runtime `Migrate()` to **migration bundles**:

```bash
dotnet ef migrations bundle --self-contained -r linux-x64 --project src/api
./efbundle --connection "your-connection-string"
```

Bundles are self-contained executables that apply pending migrations — no SDK needed on the server. Generate them in CI/CD and run as a deployment step before starting the app.

## Common Pitfalls

- **Never write migration files by hand** — always use `dotnet ef migrations add`. The generated Designer.cs and ModelSnapshot.cs contain serialized model state that EF uses to diff for the next migration. Hand-editing these files will corrupt the migration chain.
- **Never call `EnsureCreated()` before `Migrate()`** — `EnsureCreated` bypasses the migration history table and `Migrate` will fail
- **Never delete migrations that have been applied to a shared database** — downstream migrations depend on them
- **Always check the `Down()` method** — EF generates it automatically but it may not be correct for data-preserving rollbacks
- **Migrations are generated against SQL Server** — the dev connection string in `appsettings.Development.json` must point to a running SQL Server instance. Tests use SQLite in-memory but don't use migrations.

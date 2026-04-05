# Teeforce Rename Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **HARD RULE: No building or testing.** Do NOT run `dotnet build`, `dotnet test`, `pnpm build`, `pnpm test`, `make dev`, or any compilation/test commands. Execute the rename steps as written and move on. Build and test verification happens once at the end as a dedicated gap-finding phase.

**Goal:** Rename the entire project from Shadowbrook to Teeforce — code, config, infra, docs — in a single PR.

**Architecture:** Big bang rename in one branch. All C# namespaces, project files, directories, Docker, infrastructure, workflows, and documentation updated together. Azure resource deployment and GitHub repo rename happen post-merge during a maintenance window.

**Tech Stack:** .NET 10, React/TypeScript, Bicep, GitHub Actions, Docker

**Spec:** `docs/superpowers/specs/2026-04-05-teeforce-rename-design.md`

---

### Task 1: Rename Solution File and Project Directories

**Files:**
- Rename: `shadowbrook.slnx` -> `teeforce.slnx`
- Rename dirs: `src/backend/Shadowbrook.Api/` -> `src/backend/Teeforce.Api/`
- Rename dirs: `src/backend/Shadowbrook.Domain/` -> `src/backend/Teeforce.Domain/`
- Rename dirs: `tests/Shadowbrook.Domain.Tests/` -> `tests/Teeforce.Domain.Tests/`
- Rename dirs: `tests/Shadowbrook.Api.Tests/` -> `tests/Teeforce.Api.Tests/`
- Rename dirs: `tests/Shadowbrook.Api.IntegrationTests/` -> `tests/Teeforce.Api.IntegrationTests/`

- [ ] **Step 1: Rename solution file**

```bash
git mv shadowbrook.slnx teeforce.slnx
```

- [ ] **Step 2: Update solution file contents**

Replace all `Shadowbrook` with `Teeforce` inside `teeforce.slnx`. The file contains 5 project references like:
```
src/backend/Shadowbrook.Api/Shadowbrook.Api.csproj
```
Each becomes:
```
src/backend/Teeforce.Api/Teeforce.Api.csproj
```

- [ ] **Step 3: Rename project directories**

```bash
git mv src/backend/Shadowbrook.Api src/backend/Teeforce.Api
git mv src/backend/Shadowbrook.Domain src/backend/Teeforce.Domain
git mv tests/Shadowbrook.Domain.Tests tests/Teeforce.Domain.Tests
git mv tests/Shadowbrook.Api.Tests tests/Teeforce.Api.Tests
git mv tests/Shadowbrook.Api.IntegrationTests tests/Teeforce.Api.IntegrationTests
```

- [ ] **Step 4: Rename .csproj files**

```bash
git mv src/backend/Teeforce.Api/Shadowbrook.Api.csproj src/backend/Teeforce.Api/Teeforce.Api.csproj
git mv src/backend/Teeforce.Domain/Shadowbrook.Domain.csproj src/backend/Teeforce.Domain/Teeforce.Domain.csproj
git mv tests/Teeforce.Domain.Tests/Shadowbrook.Domain.Tests.csproj tests/Teeforce.Domain.Tests/Teeforce.Domain.Tests.csproj
git mv tests/Teeforce.Api.Tests/Shadowbrook.Api.Tests.csproj tests/Teeforce.Api.Tests/Teeforce.Api.Tests.csproj
git mv tests/Teeforce.Api.IntegrationTests/Shadowbrook.Api.IntegrationTests.csproj tests/Teeforce.Api.IntegrationTests/Teeforce.Api.IntegrationTests.csproj
```

- [ ] **Step 5: Update ProjectReference and InternalsVisibleTo in .csproj files**

In `src/backend/Teeforce.Api/Teeforce.Api.csproj`:
- Change `<InternalsVisibleTo Include="Shadowbrook.Api.Tests" />` to `<InternalsVisibleTo Include="Teeforce.Api.Tests" />`
- Change `<ProjectReference Include="..\Shadowbrook.Domain\Shadowbrook.Domain.csproj" />` to `<ProjectReference Include="..\Teeforce.Domain\Teeforce.Domain.csproj" />`

In `tests/Teeforce.Domain.Tests/Teeforce.Domain.Tests.csproj`:
- Change `Include="..\..\src\backend\Shadowbrook.Domain\Shadowbrook.Domain.csproj"` to `Include="..\..\src\backend\Teeforce.Domain\Teeforce.Domain.csproj"`

In `tests/Teeforce.Api.IntegrationTests/Teeforce.Api.IntegrationTests.csproj`:
- Change `Include="..\..\src\backend\Shadowbrook.Api\Shadowbrook.Api.csproj"` to `Include="..\..\src\backend\Teeforce.Api\Teeforce.Api.csproj"`

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: rename solution, projects, and directories from Shadowbrook to Teeforce"
```

---

### Task 2: Rename All C# Namespaces and Using Statements

**Files:**
- Modify: All `.cs` files under `src/backend/` and `tests/` (~189 files)

- [ ] **Step 1: Global find/replace in all C# files**

Replace all occurrences of `Shadowbrook` with `Teeforce` in every `.cs` file under `src/backend/` and `tests/`. This covers:
- `namespace Shadowbrook.` -> `namespace Teeforce.`
- `using Shadowbrook.` -> `using Teeforce.`
- `Shadowbrook.Api` -> `Teeforce.Api` (in string literals, assembly references)
- `Shadowbrook.Domain` -> `Teeforce.Domain`

Use a bulk find/replace across all `.cs` files:
```bash
find src/backend tests -name "*.cs" -exec sed -i 's/Shadowbrook/Teeforce/g' {} +
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "chore: rename all C# namespaces from Shadowbrook to Teeforce"
```

---

### Task 3: Update Configuration Files

**Files:**
- Modify: `src/backend/Teeforce.Api/appsettings.Development.json`
- Modify: `docker-compose.yml`

- [ ] **Step 1: Update appsettings.Development.json**

In `src/backend/Teeforce.Api/appsettings.Development.json`:
- Change `Database=Shadowbrook` to `Database=Teeforce` in the connection string (line 13)
- Change `admin-test@shadowbrook.com` to `admin-test@benjamingolfco.onmicrosoft.com` in SeedAdminEmails (line 23)

- [ ] **Step 2: Update docker-compose.yml**

In `docker-compose.yml`:
- Change `container_name: shadowbrook-api` to `container_name: teeforce-api` (line 19)
- Change `dockerfile: src/backend/Shadowbrook.Api/Dockerfile` to `dockerfile: src/backend/Teeforce.Api/Dockerfile` (line 22)
- Change `Database=Shadowbrook` to `Database=Teeforce` in ConnectionStrings__DefaultConnection (line 28)
- Update volume paths: replace all `Shadowbrook.Api` with `Teeforce.Api` and `Shadowbrook.Domain` with `Teeforce.Domain` (lines 33-36)

- [ ] **Step 3: Check appsettings.json (non-Development)**

In `src/backend/Teeforce.Api/appsettings.json`, verify no `Shadowbrook` references remain. The main appsettings.json has `SeedAdminEmails` with `aarongbenjamin@gmail.com` (no change needed) but check for any other occurrences.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: update config files and docker-compose for Teeforce rename"
```

---

### Task 4: Update Seed Data and Test Email Addresses

**Files:**
- Modify: `src/backend/Teeforce.Api/Infrastructure/Data/E2ESeedData.cs`
- Modify: `src/web/e2e/fixtures/test-data.ts`
- Modify: `src/web/e2e/fixtures/auth.ts`
- Modify: `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs`

- [ ] **Step 1: Update E2ESeedData.cs**

In `src/backend/Teeforce.Api/Infrastructure/Data/E2ESeedData.cs`:
- Change `"e2e@shadowbrook.golf"` to `"e2e@benjamingolfco.onmicrosoft.com"` (line 27)
- Change `"e2e-operator@shadowbrook.golf"` to `"e2e-operator@benjamingolfco.onmicrosoft.com"` (lines 88, 93)

- [ ] **Step 2: Update e2e test fixtures**

In `src/web/e2e/fixtures/test-data.ts`:
- Change `'e2e-operator@shadowbrook.golf'` to `'e2e-operator@benjamingolfco.onmicrosoft.com'` (line 20)

In `src/web/e2e/fixtures/auth.ts`:
- Change `e2e-operator@shadowbrook.golf` to `e2e-operator@benjamingolfco.onmicrosoft.com` (lines 15, 112)

- [ ] **Step 3: Update domain test email addresses**

In `tests/Teeforce.Domain.Tests/AppUserAggregate/AppUserTests.cs`:
- Change `"admin@shadowbrook.com"` to `"admin@benjamingolfco.onmicrosoft.com"` (lines 21, 25, 57)

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: update seed data and test emails to @benjamingolfco.onmicrosoft.com"
```

---

### Task 5: Update Makefile

**Files:**
- Modify: `Makefile`

- [ ] **Step 1: Update all project path references**

In `Makefile`:
- Change `dotnet run --project src/backend/Shadowbrook.Api` to `dotnet run --project src/backend/Teeforce.Api` (line 16)
- Change `dotnet build shadowbrook.slnx` to `dotnet build teeforce.slnx` (line 22)
- Change `dotnet test tests/Shadowbrook.Domain.Tests` to `dotnet test tests/Teeforce.Domain.Tests` (line 26)
- Change `dotnet test tests/Shadowbrook.Api.Tests` to `dotnet test tests/Teeforce.Api.Tests` (line 27)
- Change `dotnet clean shadowbrook.slnx` to `dotnet clean teeforce.slnx` (line 36)

Replace all remaining `Shadowbrook` occurrences with `Teeforce` and `shadowbrook` with `teeforce`.

- [ ] **Step 2: Commit**

```bash
git add Makefile
git commit -m "chore: update Makefile paths for Teeforce rename"
```

---

### Task 6: Update GitHub Actions Workflows

**Files:**
- Modify: `.github/workflows/deploy-api.yml`
- Modify: `.github/workflows/deploy-web.yml`
- Modify: `.github/workflows/deploy-infra.yml`
- Modify: `.github/workflows/acr-cleanup.yml`
- Modify: `.github/workflows/swa-cleanup.yml`
- Modify: `.github/workflows/pr.yml`
- Modify: `.github/workflows/e2e.yml`
- Modify: `.github/workflows/claude-planning.yml`
- Modify: `.github/workflows/claude-implementation.yml`

- [ ] **Step 1: Update deploy-api.yml**

Replace all occurrences:
- `shadowbrook-${{ inputs.environment }}-rg` -> `teeforce-${{ inputs.environment }}-rg` (line 34)
- `shadowbrookacr` -> `teeforceacr` (line 35)
- `shadowbrook` (IMAGE_NAME) -> `teeforce` (line 36)
- `shadowbrook-app-${{ inputs.environment }}` -> `teeforce-app-${{ inputs.environment }}` (line 37)
- `src/backend/Shadowbrook.Api/Dockerfile` -> `src/backend/Teeforce.Api/Dockerfile` (line 54)

- [ ] **Step 2: Update deploy-web.yml**

Replace:
- `shadowbrook-app-${{ inputs.environment }}` -> `teeforce-app-${{ inputs.environment }}` (line 51)
- `shadowbrook-${{ inputs.environment }}-rg` -> `teeforce-${{ inputs.environment }}-rg` (line 52)

- [ ] **Step 3: Update deploy-infra.yml**

Replace all `shadowbrook` with `teeforce`:
- Comments referencing `shadowbrook-shared-rg` (lines 4-5)
- `"shadowbrook-shared"` -> `"teeforce-shared"` (line 53)
- `"shadowbrook-app-${{ inputs.environment }}"` -> `"teeforce-app-${{ inputs.environment }}"` (line 61)
- `"shadowbrook-${{ inputs.environment }}-rg"` -> `"teeforce-${{ inputs.environment }}-rg"` (line 62)
- `"shadowbrook-${{ inputs.environment }}"` -> `"teeforce-${{ inputs.environment }}"` (line 72)

- [ ] **Step 4: Update acr-cleanup.yml and swa-cleanup.yml**

In `acr-cleanup.yml`:
- `shadowbrookacr` -> `teeforceacr` (line 30)
- `shadowbrook` -> `teeforce` (IMAGE_NAME, line 31)

In `swa-cleanup.yml`:
- `shadowbrook-web-test` -> `teeforce-web-test` (line 30)
- `shadowbrook-test-rg` -> `teeforce-test-rg` (line 31)

- [ ] **Step 5: Update pr.yml**

Replace all occurrences:
- `shadowbrook.slnx` -> `teeforce.slnx` (lines 72, 75, 146, 174, 177, 180)
- `src/backend/Shadowbrook.Api` -> `src/backend/Teeforce.Api` (lines 83, 89, 96, 114, 126)

- [ ] **Step 6: Update e2e.yml**

Replace:
- `shadowbrook-app-test.happypond-1a892999.eastus2.azurecontainerapps.io` -> leave as a placeholder comment `# URL will change after new container app deployment` and set to empty or a variable. The actual new URL won't be known until after Azure redeployment.

Note: For now, comment out or set to a placeholder. The URL is updated post-deployment in the maintenance window.

- [ ] **Step 7: Update claude-planning.yml and claude-implementation.yml**

In `claude-planning.yml`:
- `name: Shadowbrook Planning` -> `name: Teeforce Planning` (line 1)
- `Planning Manager for the Shadowbrook agent pipeline` -> `Planning Manager for the Teeforce agent pipeline` (line 50)

In `claude-implementation.yml`:
- `name: Shadowbrook Implementation` -> `name: Teeforce Implementation` (line 1)
- `benjamingolfco/shadowbrook` -> `benjamingolfco/teeforce` (lines 99, 110, 226)
- `Sprint Manager for the Shadowbrook agent pipeline` -> `Sprint Manager for the Teeforce agent pipeline` (line 163)

- [ ] **Step 8: Commit**

```bash
git add .github/
git commit -m "chore: update GitHub Actions workflows for Teeforce rename"
```

---

### Task 7: Update Bicep Infrastructure Templates

**Files:**
- Modify: `infra/bicep/main.bicep`
- Modify: `infra/bicep/shared.bicep`
- Modify: `infra/bicep/modules/container-app.bicep`
- Modify: `infra/bicep/modules/container-app-env.bicep`
- Modify: `infra/bicep/modules/database.bicep`
- Modify: `infra/bicep/modules/registry.bicep`
- Modify: `infra/bicep/modules/managed-identity.bicep`
- Modify: `infra/bicep/modules/log-analytics.bicep`
- Modify: `infra/bicep/modules/app-insights.bicep`
- Modify: `infra/bicep/modules/static-web-app.bicep`

- [ ] **Step 1: Update main.bicep**

Replace all occurrences of `shadowbrook` with `teeforce`:
- Comment line 1: `// Teeforce - Environment Infrastructure Orchestration`
- Comment line 5: `teeforce-shared-rg`
- `'shadowbrook-shared-rg'` -> `'teeforce-shared-rg'` (line 31)
- `'shadowbrookacr'` -> `'teeforceacr'` (line 34)
- `'shadowbrook-${environment}-rg'` -> `'teeforce-${environment}-rg'` (line 37)
- `'id-shadowbrook-${environment}'` -> `'id-teeforce-${environment}'` (line 69)

- [ ] **Step 2: Update shared.bicep**

- Comment line 1: `// Teeforce - Shared Infrastructure`
- `'shadowbrook-shared-rg'` -> `'teeforce-shared-rg'` (line 13)

- [ ] **Step 3: Update all Bicep modules**

In each module file, replace `shadowbrook` with `teeforce`:

`modules/container-app.bicep`:
- `'shadowbrook-app-${environment}'` -> `'teeforce-app-${environment}'` (line 42)
- `'shadowbrook-api'` -> `'teeforce-api'` (line 84)
- `'/shadowbrook:${imageTag}'` -> `'/teeforce:${imageTag}'` (line 85)

`modules/container-app-env.bicep`:
- `'shadowbrook-env-${environment}'` -> `'teeforce-env-${environment}'` (line 17)

`modules/database.bicep`:
- `'shadowbrook-sql-${environment}'` -> `'teeforce-sql-${environment}'` (line 18)
- `'shadowbrook-db-${environment}'` -> `'teeforce-db-${environment}'` (line 19)

`modules/registry.bicep`:
- `'shadowbrookacr'` -> `'teeforceacr'` (line 8)
- `'shadowbrook'` -> `'teeforce'` in workload tag (line 21)

`modules/managed-identity.bicep`:
- `'id-shadowbrook-${environment}'` -> `'id-teeforce-${environment}'` (line 11)
- `'shadowbrook'` -> `'teeforce'` in workload tag (line 18)

`modules/log-analytics.bicep`:
- `'shadowbrook-logs-${environment}'` -> `'teeforce-logs-${environment}'` (line 13)

`modules/app-insights.bicep`:
- `'shadowbrook-insights-${environment}'` -> `'teeforce-insights-${environment}'` (line 13)

`modules/static-web-app.bicep`:
- `'shadowbrook-web-${environment}'` -> `'teeforce-web-${environment}'` (line 11)

- [ ] **Step 4: Commit**

```bash
git add infra/bicep/
git commit -m "chore: update Bicep templates for Teeforce rename"
```

---

### Task 8: Update Infrastructure Scripts

**Files:**
- Modify: `infra/scripts/deploy.sh`
- Modify: `infra/scripts/setup-github-oidc.sh`
- Modify: `infra/scripts/grant-graph-permissions.sh`
- Modify: `infra/scripts/teardown.sh`
- Modify: `infra/scripts/reset-db.sh`

- [ ] **Step 1: Update deploy.sh**

Replace all `shadowbrook` with `teeforce`:
- Comment (line 4): `# Teeforce - Deploy Bicep infrastructure`
- `ACR_NAME="shadowbrookacr"` -> `ACR_NAME="teeforceacr"` (line 33)
- `"shadowbrook-shared"` -> `"teeforce-shared"` (line 41)
- `shadowbrook` repository references -> `teeforce` (lines 48, 52, 54)
- `"shadowbrook-app-${ENVIRONMENT}"` -> `"teeforce-app-${ENVIRONMENT}"` (line 59)
- `"shadowbrook-${ENVIRONMENT}-rg"` -> `"teeforce-${ENVIRONMENT}-rg"` (line 60)
- `"shadowbrook-${ENVIRONMENT}"` -> `"teeforce-${ENVIRONMENT}"` (line 71)

- [ ] **Step 2: Update setup-github-oidc.sh**

Replace all `shadowbrook` with `teeforce`:
- Comment (line 4): `# Teeforce - Set up GitHub Actions OIDC authentication`
- `REPO="benjamingolfco/shadowbrook"` -> `REPO="benjamingolfco/teeforce"` (line 26)
- `APP_NAME="shadowbrook-github-actions"` -> `APP_NAME="teeforce-github-actions"` (line 27)
- All resource group and ACR references (lines 133-135, 162-164)

- [ ] **Step 3: Update grant-graph-permissions.sh**

- Comment (line 4): `# Teeforce - Grant Microsoft Graph API permissions to managed identity`
- `IDENTITY_NAME="id-shadowbrook-${ENVIRONMENT}"` -> `IDENTITY_NAME="id-teeforce-${ENVIRONMENT}"` (line 25)
- `RESOURCE_GROUP="shadowbrook-${ENVIRONMENT}-rg"` -> `RESOURCE_GROUP="teeforce-${ENVIRONMENT}-rg"` (line 26)

- [ ] **Step 4: Update teardown.sh**

- Comment (line 4): `# Teardown Teeforce environment`
- `shadowbrook-test-rg` -> `teeforce-test-rg` (line 11)
- `shadowbrook-shared-rg` -> `teeforce-shared-rg` (line 12)

- [ ] **Step 5: Update reset-db.sh**

- Comment (line 4): `# Reset Teeforce database`
- `"shadowbrook-sql-${ENVIRONMENT}"` -> `"teeforce-sql-${ENVIRONMENT}"` (line 11)
- `"shadowbrook-db-${ENVIRONMENT}"` -> `"teeforce-db-${ENVIRONMENT}"` (line 12)
- `"shadowbrook-${ENVIRONMENT}-rg"` -> `"teeforce-${ENVIRONMENT}-rg"` (line 13)
- `shadowbrook-app-${ENVIRONMENT}` -> `teeforce-app-${ENVIRONMENT}` (line 47)

- [ ] **Step 6: Commit**

```bash
git add infra/scripts/
git commit -m "chore: update infrastructure scripts for Teeforce rename"
```

---

### Task 9: Update Documentation

**Files:**
- Modify: `README.md`
- Modify: `.claude/CLAUDE.md`
- Modify: `infra/README.md`
- Modify: `.claude/rules/backend/backend-conventions.md`
- Modify: `.claude/rules/backend/ef-migrations.md`
- Modify: `.claude/rules/backend/integration-test-conventions.md`
- Modify: `.claude/skills/how-tos/start-local-dev.md`
- Modify: `docs/superpowers/plans/2026-04-01-user-invite-flow.md`

- [ ] **Step 1: Update README.md**

- `# Shadowbrook` -> `# Teeforce` (line 1)
- Badge URLs: `benjamingolfco/shadowbrook` -> `benjamingolfco/teeforce` (line 3)
- `shadowbrook-app-test.happypond-...` -> add comment that URL will change post-deployment (line 21)
- `shadowbrook.slnx` -> `teeforce.slnx` (line 54)

- [ ] **Step 2: Update .claude/CLAUDE.md**

Replace all occurrences of `Shadowbrook` with `Teeforce` and `shadowbrook` with `teeforce`:
- Title: `# Teeforce — Tee Time Booking Platform` (line 1)
- All project structure paths (lines 46-54)
- Build commands: `teeforce.slnx` (lines 63-64)
- Migration commands: `src/backend/Teeforce.Api` (line 71)

- [ ] **Step 3: Update infra/README.md**

Replace all `shadowbrook` with `teeforce` and `Shadowbrook` with `Teeforce` throughout the file (~30+ occurrences). This includes:
- Title (line 1)
- All resource group names, ACR names, resource naming examples
- All CLI command examples
- File path references to `src/backend/Shadowbrook.Api/` -> `src/backend/Teeforce.Api/`
- The test URL will need updating post-deployment

- [ ] **Step 4: Update .claude/ rules files**

In `.claude/rules/backend/backend-conventions.md`:
- Replace all `Shadowbrook` with `Teeforce` and `shadowbrook` with `teeforce` (project paths, solution file, migration commands)

In `.claude/rules/backend/ef-migrations.md`:
- `src/backend/Shadowbrook.Api/Migrations/**` -> `src/backend/Teeforce.Api/Migrations/**` (line 3)
- `src/backend/Shadowbrook.Api/Data/ApplicationDbContext.cs` -> `src/backend/Teeforce.Api/Data/ApplicationDbContext.cs` (line 4)
- `--project src/backend/Shadowbrook.Api` -> `--project src/backend/Teeforce.Api` (line 17)

In `.claude/rules/backend/integration-test-conventions.md`:
- `tests/Shadowbrook.Api.IntegrationTests/**/*.cs` -> `tests/Teeforce.Api.IntegrationTests/**/*.cs` (line 3)
- `tests/Shadowbrook.Api.IntegrationTests/` -> `tests/Teeforce.Api.IntegrationTests/` (line 10)
- `"Shadowbrook.Api.IntegrationTests.StepOrderer"` -> `"Teeforce.Api.IntegrationTests.StepOrderer"` (line 44)
- `"Shadowbrook.Api.IntegrationTests"` -> `"Teeforce.Api.IntegrationTests"` (line 45)

- [ ] **Step 5: Update skills and plan docs**

In `.claude/skills/how-tos/start-local-dev.md`:
- `'dev-admin@shadowbrook.golf'` -> `'dev-admin@benjamingolfco.onmicrosoft.com'` (line 29)

In `docs/superpowers/plans/2026-04-01-user-invite-flow.md`:
- `"admin@shadowbrook.com"` -> `"admin@benjamingolfco.onmicrosoft.com"` (lines 116, 120)

- [ ] **Step 6: Scan for any remaining Shadowbrook references in docs and .claude/**

```bash
grep -ri "shadowbrook" .claude/ docs/ README.md --include="*.md" -l
```

Fix any remaining occurrences found.

- [ ] **Step 7: Commit**

```bash
git add README.md .claude/ infra/README.md docs/
git commit -m "chore: update all documentation for Teeforce rename"
```

---

### Task 10: Final Sweep — Catch Remaining References

**Files:**
- Any files missed by previous tasks

- [ ] **Step 1: Search entire repo for remaining Shadowbrook references**

```bash
grep -ri "shadowbrook" --include="*.cs" --include="*.json" --include="*.yml" --include="*.yaml" --include="*.bicep" --include="*.sh" --include="*.md" --include="*.ts" --include="*.tsx" --include="*.csproj" --include="*.slnx" -l .
```

- [ ] **Step 2: Fix any remaining occurrences**

For each file found, replace `Shadowbrook` with `Teeforce` and `shadowbrook` with `teeforce` (except in the spec/plan docs themselves which reference the rename, and git history).

- [ ] **Step 3: Search for @shadowbrook email domain**

```bash
grep -ri "@shadowbrook" --include="*.cs" --include="*.json" --include="*.ts" --include="*.md" -l .
```

Replace any remaining `@shadowbrook.com` or `@shadowbrook.golf` with `@benjamingolfco.onmicrosoft.com`.

- [ ] **Step 4: Commit if any changes found**

```bash
git add -A
git commit -m "chore: fix remaining Shadowbrook references missed in earlier tasks"
```

---

### Task 11: Build and Test Verification (Gap-Finding Phase)

> **This is the ONLY task where building and testing is allowed.**

- [ ] **Step 1: Build the .NET solution**

```bash
dotnet build teeforce.slnx
```

Fix any compilation errors. Common issues:
- Missed namespace renames
- Stale `using` statements
- Assembly references that weren't updated

- [ ] **Step 2: Run dotnet format**

```bash
dotnet format teeforce.slnx
```

- [ ] **Step 3: Run unit tests**

```bash
dotnet test teeforce.slnx --filter "Category!=Integration"
```

Fix any test failures.

- [ ] **Step 4: Run frontend lint**

```bash
pnpm --dir src/web lint
```

- [ ] **Step 5: Run frontend tests**

```bash
pnpm --dir src/web test
```

- [ ] **Step 6: Start the app locally**

```bash
make dev
```

Verify the app starts and the API responds on `:5221` and web on `:3000`.

- [ ] **Step 7: Commit any fixes**

```bash
git add -A
git commit -m "fix: resolve build/test issues from Teeforce rename"
```

---

### Task 12: Post-Merge Maintenance Window (Manual Steps)

> **These steps happen AFTER the PR is merged to main.** They are manual operations, not automated by agents.

- [ ] **Step 1: Rename GitHub repo**

GitHub Settings -> General -> Repository name -> Change `shadowbrook` to `teeforce`.

- [ ] **Step 2: Clone fresh into new directory**

```bash
git clone https://github.com/benjamingolfco/teeforce.git ~/dev/orgs/benjamingolfco/teeforce
```

- [ ] **Step 3: Copy gitignored files from old clone**

```bash
cp ~/dev/orgs/benjamingolfco/shadowbrook/.claude/settings.local.json ~/dev/orgs/benjamingolfco/teeforce/.claude/settings.local.json
cp -r ~/dev/orgs/benjamingolfco/shadowbrook/.claude/commands ~/dev/orgs/benjamingolfco/teeforce/.claude/commands
cp ~/dev/orgs/benjamingolfco/shadowbrook/.mcp.json ~/dev/orgs/benjamingolfco/teeforce/.mcp.json
cp -r ~/dev/orgs/benjamingolfco/shadowbrook/.local ~/dev/orgs/benjamingolfco/teeforce/.local
cp ~/dev/orgs/benjamingolfco/shadowbrook/src/web/.env.development.local ~/dev/orgs/benjamingolfco/teeforce/src/web/.env.development.local
cp -r ~/dev/orgs/benjamingolfco/shadowbrook/.vscode ~/dev/orgs/benjamingolfco/teeforce/.vscode
cp -r ~/dev/orgs/benjamingolfco/shadowbrook/.idea ~/dev/orgs/benjamingolfco/teeforce/.idea
```

- [ ] **Step 4: Copy Claude Code memory files**

```bash
cp -r ~/.claude/projects/-home-aaron-dev-orgs-benjamingolfco-shadowbrook ~/.claude/projects/-home-aaron-dev-orgs-benjamingolfco-teeforce
```

Update any paths inside the memory files that reference `shadowbrook`.

- [ ] **Step 5: Create new ACR and push image**

```bash
az acr create --name teeforceacr --resource-group teeforce-shared-rg --sku Basic
az acr import --name teeforceacr --source shadowbrookacr.azurecr.io/shadowbrook:latest --image teeforce:latest
```

- [ ] **Step 6: Deploy full Bicep stack with new resource names**

```bash
cd ~/dev/orgs/benjamingolfco/teeforce
./infra/scripts/deploy.sh test
```

This creates all new resources (resource groups, SQL server with fresh DB, container app, etc.). EF Core migrations run on first startup.

- [ ] **Step 7: Verify the app is healthy**

Check the new container app URL (output from deployment) responds with a healthy status.

- [ ] **Step 8: Update E2E test URL**

Once the new container app URL is known, update:
- `.github/workflows/e2e.yml` — `E2E_API_URL`
- `.claude/` sandbox config if it references the old URL
- `README.md` — test environment URL

Commit and push these changes.

- [ ] **Step 9: Re-run setup-github-oidc.sh**

```bash
./infra/scripts/setup-github-oidc.sh
```

This updates the app registration and RBAC assignments to point to the new resource names.

- [ ] **Step 10: Delete old Azure resources**

```bash
az group delete --name shadowbrook-test-rg --yes
az group delete --name shadowbrook-shared-rg --yes
```

Verify the old resources are gone and the new ones are healthy.

# Teeforce Rename Design Spec

## Overview

Rename the entire project from "Teeforce" to "Teeforce" — code, infrastructure, GitHub repo, and Azure resources — in a single coordinated cutover with a maintenance window.

## Naming Convention

- **PascalCase / display contexts:** `Teeforce` (namespaces, project names, titles, display text)
- **Lowercase contexts:** `teeforce` (URLs, Azure resource names, repo slug, Docker)

## Approach: Big Bang Rename

One branch, one PR, one maintenance window. No transitional state.

## Scope

### 1. Code Rename (~350 files)

**Directory renames:**
- `src/backend/Teeforce.Domain/` -> `src/backend/Teeforce.Domain/`
- `src/backend/Teeforce.Api/` -> `src/backend/Teeforce.Api/`
- `tests/Teeforce.Domain.Tests/` -> `tests/Teeforce.Domain.Tests/`
- `tests/Teeforce.Api.Tests/` -> `tests/Teeforce.Api.Tests/`
- `tests/Teeforce.Api.IntegrationTests/` -> `tests/Teeforce.Api.IntegrationTests/`

**Solution file:**
- `teeforce.slnx` -> `teeforce.slnx`
- Update all project references inside

**Project files:**
- Rename 5 `.csproj` files (`Teeforce.*.csproj` -> `Teeforce.*.csproj`)
- Update all `<ProjectReference>` cross-references

**C# namespaces:**
- Global find/replace `Teeforce` -> `Teeforce` across all `.cs` files (~189 files)

**Configuration:**
- `appsettings*.json` — `Database=Teeforce` -> `Database=Teeforce`
- Seed data emails: `admin-test@benjamingolfco.onmicrosoft.com` -> `admin-test@benjamingolfco.onmicrosoft.com`

**Docker:**
- `docker-compose.yml` — container name, DB name, volume paths

**Makefile:**
- All project path references updated

**Documentation:**
- `CLAUDE.md` — all Teeforce references, project paths, build commands
- `README.md` — title, URLs, project structure paths, badge URLs
- `.claude/` files — agent definitions, skills, rules referencing Teeforce paths

**Excluded from rename:** Git history (commit messages mentioning Teeforce are historical record).

### 2. GitHub Rename

- Rename repo `benjamingolfco/teeforce` -> `benjamingolfco/teeforce` via GitHub Settings
- GitHub auto-redirects all old URLs (clones, issues, PRs)
- Update local remotes: `git remote set-url origin https://github.com/benjamingolfco/teeforce.git`
- Update hardcoded repo references in GitHub Actions workflows and README badges
- Update `.claude/` sandbox/settings referencing the repo path

### 3. Azure Infrastructure

**Resources recreated via Bicep redeploy (destroy + recreate):**
- Resource groups: `teeforce-{env}-rg` -> `teeforce-{env}-rg`
- Shared resource group: `teeforce-shared-rg` -> `teeforce-shared-rg`
- Container Apps: `teeforce-app-{env}` -> `teeforce-app-{env}`
- Container App Environment
- Log Analytics workspace
- App Insights instance
- Static Web App
- Managed Identity: `id-teeforce-{env}` -> `id-teeforce-{env}`

**Resources that cannot be renamed in-place:**
- **SQL Server:** Create `teeforce-sql-{env}` fresh. No data migration — EF Core migrations run on first startup with a fresh database.
- **ACR:** Create `teeforceacr`, use `az acr import` to copy images from old registry, update all workflow references, delete old registry.

**Bicep templates:** All 10 files updated with new naming patterns.

**Deploy scripts:** `deploy.sh`, `grant-graph-permissions.sh` updated.

**GitHub Actions workflows:** Environment variables for resource names, container app URLs, ACR login server, E2E test URLs.

**Note:** The E2E test URL (`teeforce-app-test.happypond-...`) will change when the container app is recreated — the new URL won't be known until after deployment. Update in workflows, `.claude/` sandbox config, and any hardcoded references after the new container app is provisioned.

## Agent Execution Rules

**HARD RULE: No building or testing during implementation.** Agents performing rename tasks must NOT run `dotnet build`, `dotnet test`, `pnpm build`, `pnpm test`, `make dev`, or any compilation/test commands. Execute the rename steps as written and move on. Build and test verification happens once at the end as a dedicated gap-finding phase.

### 4. Execution Order (Maintenance Window)

1. Merge the code rename PR (all code, config, docs, Makefile, Docker, CLAUDE.md)
2. Rename GitHub repo (teeforce -> teeforce)
3. Clone fresh into `~/dev/orgs/benjamingolfco/teeforce/` (keep old `teeforce/` directory as reference)
4. Copy gitignored files from old clone to new:
   - `.claude/settings.local.json` — local Claude Code settings
   - `.claude/commands/` — custom slash commands
   - `.mcp.json` — MCP server config
   - `.local/` — local data
   - `src/web/.env.development.local` — local frontend env vars
   - `.vscode/` — workspace settings
   - `.idea/` — JetBrains settings
5. Copy `~/.claude/projects/` memory files to new project path
5. Create new ACR, push image from CI or manually
6. Deploy Bicep stack with new resource names (creates fresh SQL, container app, everything)
7. EF Core migrations run on first startup, fresh DB
8. Verify app is healthy
9. Delete old Azure resources

## Domains

- `teeforce.golf` — owned, available for production use
- `benjamingolfco.com` — owned, used for org-level references

## Decisions Made

- **Casing:** `Teeforce` (PascalCase) / `teeforce` (lowercase) — not `TeeForce`
- **Repo strategy:** Rename existing repo (preserves issues, PRs, history, secrets)
- **Azure resources:** Full rename, accept maintenance window
- **Database:** Fresh DB, no migration needed
- **Seed emails:** `@benjamingolfco.onmicrosoft.com` (matches existing test accounts; switch to real domain later)
- **Timeline:** One shot, accept downtime

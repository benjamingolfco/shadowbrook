# Teeforce — Tee Time Booking Platform

## Project Principles

### 1. Zero Training Required
Both golfers and course operators should be productive immediately — no onboarding sessions, no manuals. Progressive disclosure, familiar patterns, error prevention over error messages. Operator tools mirror how they already think (tee sheet = visual grid, not a form).

### 2. Event-Driven Backend
The backend communicates through domain events, not direct service coupling. Key actions publish events; downstream concerns (SMS, waitlist processing, analytics) subscribe. If a downstream system is slow or down, the core flow still completes.

### 3. SMS is the Communication Channel
Web for actions (browse, book, manage profile), SMS for system-to-golfer communication (confirmations, waitlist updates, cancellation notices). Over time, SMS expands from one-way notifications to two-way conversational booking.

### 4. Multi-Tenant from Day One
Every course shares infrastructure but gets its own isolated world. Every API endpoint, query, and data access path is scoped to a course. Per-course configuration for intervals, pricing, policies, and rules. No data leakage between tenants.

### 5. Configuration Without Opinions
Course operators know their course best. Ship with sensible defaults, but every operational parameter is configurable. No hard-coded business rules. As usage data accumulates, we may introduce gentle suggestions — but never force them.

## Build & Run
- Both: `make dev` (API on :5221, Web on :3000)
- API only: `make api`
- Web only: `make web`
- Build all: `make build`
- Run tests: `make test`
- Lint frontend: `make lint`
- Start DB only: `make db`
- Stop containers: `make down`
- Follow API logs: `make logs`
- Clean build artifacts: `make clean`
- Deep clean (fixes Docker/sandbox permission issues): `make clean-hard`
- All commands: `make help`
- Docker: Always use `docker compose` to run the app in Docker — never hand-craft `docker run` commands. The compose file has all environment variables, volume mounts, and service dependencies configured correctly.

## Tech Stack
- Backend: .NET 10, EF Core 10, OpenAPI, minimal APIs (not controllers)
- Database: SQL Server (local via `docker compose up db -d`, EF Core migrations)
- Frontend: React 19, TypeScript 5.9, Vite 7, React Router, TanStack Query, React Hook Form + Zod, Tailwind CSS, shadcn/ui
- Package manager: pnpm (never npm or yarn)
- Messaging: WolverineFx (SQL Server transport, migrating to Azure Service Bus)
- SMS: ITextMessageService abstraction (Twilio planned)
- Observability: Serilog + Application Insights (Serilog sink, OrganizationId enrichment)
- Infra: Azure (Container Apps, Static Web Apps, SQL Database, Log Analytics, App Insights)

## Project Structure
- src/backend/Teeforce.Domain/ — Domain model (aggregates, entities, events, repository interfaces, domain services — zero dependencies)
- src/backend/Teeforce.Api/ — .NET Web API (minimal API endpoints, Infrastructure/ for EF, repositories, event dispatch, services)
- tests/Teeforce.Domain.Tests/ — Pure domain unit tests (no DB, no HTTP)
- tests/Teeforce.Api.Tests/ — xUnit tests: unit tests (validators, handlers, utilities) + integration tests (TestWebApplicationFactory with SQL Server via Testcontainers)
- src/web/ — React SPA
- docs/ — Documentation
- docs/plans/ — Design docs and implementation plans
- infra/ — Azure deployment config (Bicep modules, deploy scripts, parameter files)
- src/backend/Teeforce.Api/Infrastructure/Observability/ — Serilog enrichers (OrganizationIdEnricher)

## Code Conventions
- Backend follows DDD patterns (aggregates, value objects, domain events, capability tokens for cross-aggregate invariants) — see `.claude/rules/backend/domain-conventions.md` (path-scoped to `Teeforce.Domain`)
- C#: `.editorconfig` at repo root defines style rules; see `.claude/rules/backend/backend-conventions.md` for full conventions
- TypeScript: strict mode, ES modules, no CommonJS, path aliases (`@/*`)
- Frontend: feature-based folders, TanStack Query for data fetching, RHF + Zod for forms (see `.claude/rules/frontend/react-conventions.md`)
- Prefer existing patterns in the codebase over introducing new ones

## Workflow
- Run `dotnet build teeforce.slnx` after C# changes to verify compilation
- Run `dotnet format teeforce.slnx` after C# changes to fix IDE style warnings (braces, `this.` qualification, naming, etc.)
- Run `pnpm --dir src/web lint` after TypeScript changes
- Run `pnpm --dir src/web test` after frontend component changes
- **Testing pyramid: unit tests first, integration tests second.** Test behavior at the cheapest layer possible — validators, handlers, domain logic should all be unit tested without spinning up a DB or HTTP server. Integration tests are for DB-dependent behavior, middleware, and E2E flows only. See `.claude/rules/backend/backend-conventions.md` for backend-specific patterns; the same principle applies to frontend (component tests before browser tests).
- **Test integrity: assertions are specifications.** Test assertions define expected system behavior and are protected. When a test fails after a code change, assume the code is wrong first — fix the implementation, not the test. Only modify existing assertions or remove tests when the intended behavior has changed per the acceptance criteria. Adding new assertions or new tests is always encouraged. Test scaffolding (setup, arrangement, dependency wiring, property renames) may be updated freely to match code changes. When modifying assertions or removing tests, post a justification on the PR explaining what behavior changed and why.
- Prefer running single tests over full suites for speed
- `dotnet-ef` tool path: `export PATH="$PATH:/home/aaron/.dotnet/tools"`
- Add migration: `dotnet ef migrations add <Name> --project src/backend/Teeforce.Api`
- Check pending: `dotnet ef migrations has-pending-model-changes --project src/backend/Teeforce.Api`
- See `.claude/rules/backend/ef-migrations.md` for full migration conventions

## Branching

Work on branches, push, and create PRs. Never commit directly to main.

| Prefix | When | Example |
|--------|------|---------|
| `issue/<number>-description` | Working on a GitHub issue | `issue/5-tee-time-settings` |
| `bug/description` | Fixing a bug | `bug/course-name-validation` |
| `chore/description` | Random tasks, cleanup, config | `chore/update-dependencies` |
| `testing/description` | Trying something out | `testing/react-router` |

## Agent Pipeline

This project uses an automated multi-agent pipeline via GitHub Actions, split into two workflows — **Planning** (new issues → Ready) and **Implementation** (in-sprint execution).

See `.claude/skills/agent-pipeline/SKILL.md` for the full protocol.

- **Workflow files:**
  - `.github/workflows/claude-planning.yml` — planning pipeline + cron (backlog processing, story refinement, feasibility check, sprint planning)
  - `.github/workflows/claude-implementation.yml` — sprint execution (architecture detail, implementation, PR lifecycle, merge cascade)
  - `.github/workflows/claude-code-review.yml` — standalone code review on all PRs
- **Agent manager instructions:** `.claude/agents/planning-manager.md`, `.claude/agents/sprint-manager.md`
- **Agent definitions:** `.claude/agents/*.md`
- **Pipeline statuses:** (no status) → Needs Story → **Ready** → Implementing → QA → Done
- **Awaiting Owner** — used when BA has open questions or after repeated CI failures (not a pipeline phase — a hold state)
- **Sprint gate:** Ready — issues wait here until assigned to an iteration for sprint execution

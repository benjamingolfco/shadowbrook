# Shadowbrook — Tee Time Booking Platform

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
- All commands: `make help`

## Tech Stack
- Backend: .NET 10, EF Core 10, OpenAPI, minimal APIs (not controllers)
- Database: SQLite for dev (auto-created via EnsureCreated), SQL Server for production
- Frontend: React 19, TypeScript 5.9, Vite 7, React Router, TanStack Query, React Hook Form + Zod, Tailwind CSS, shadcn/ui
- Package manager: pnpm (never npm or yarn)
- SMS: ITextMessageService abstraction (Twilio planned)
- Infra: Azure (planned)

## Project Structure
- src/api/ — .NET Web API (minimal API endpoints, services, EF data context)
- src/web/ — React SPA
- tests/api/ — xUnit integration tests (TestWebApplicationFactory with SQLite in-memory)
- docs/ — Documentation
- docs/plans/ — Design docs and implementation plans
- infra/ — Azure deployment config (planned)

## Code Conventions
- C#: file-scoped namespaces, nullable reference types, implicit usings
- C#: minimal API endpoints in src/api/Endpoints/, extension method pattern (MapXxxEndpoints)
- TypeScript: strict mode, ES modules, no CommonJS, path aliases (`@/*`)
- Frontend: feature-based folders, TanStack Query for data fetching, RHF + Zod for forms (see `.claude/rules/frontend/react-conventions.md`)
- Prefer existing patterns in the codebase over introducing new ones

## Workflow
- Run `dotnet build shadowbrook.slnx` after C# changes to verify compilation
- Run `pnpm --dir src/web lint` after TypeScript changes
- Run `pnpm --dir src/web test` after frontend component changes
- Prefer unit tests before integration tests
- Prefer running single tests over full suites for speed
- `dotnet-ef` tool path: `export PATH="$PATH:/home/aaron/.dotnet/tools"`

## Branching

Work on branches, push, and create PRs. Never commit directly to main.

| Prefix | When | Example |
|--------|------|---------|
| `issue/<number>-description` | Working on a GitHub issue | `issue/5-tee-time-settings` |
| `bug/description` | Fixing a bug | `bug/course-name-validation` |
| `chore/description` | Random tasks, cleanup, config | `chore/update-dependencies` |
| `testing/description` | Trying something out | `testing/react-router` |

## GitHub Project Management

Repo: `benjamingolfco/shadowbrook` | Project: #1 under `benjamingolfco` org

| Action | Command |
|--------|---------|
| Create issue | `gh api repos/benjamingolfco/shadowbrook/issues -X POST -f title="..." -f body="..." -f type="Feature"` |
| List issue types | `gh api orgs/benjamingolfco/issue-types` |
| Add labels | `gh issue edit {number} --add-label "label1,label2"` |
| Add to project | `gh project item-add 1 --owner benjamingolfco --url {issue_url}` |
| Set project field | `gh project item-edit --project-id {id} --id {item_id} --field-id {field_id} --single-select-option-id {option_id}` |
| Link sub-issue | `gh api repos/benjamingolfco/shadowbrook/issues/{parent}/sub_issues -X POST -F sub_issue_id={child_id}` |
| View issue | `gh issue view {number}` |
| List issues | `gh issue list --state open` |
| List project items | `gh project item-list 1 --owner benjamingolfco` |
| List project fields | `gh project field-list 1 --owner benjamingolfco` |

Notes:
- Use `-F` (not `-f`) for integer fields (e.g., `sub_issue_id`)
- Issue types: Task, Bug, Feature, User Story
- Project fields: Status (Triage/Needs Story/Story Review/Needs Architecture/Architecture Review/Ready/Implementing/CI Pending/In Review/Changes Requested/Ready to Merge/Awaiting Owner/Done), Priority (P0/P1/P2), Size (XS-XL)

### Issue Labels

| Label | When to Apply |
|-------|--------------|
| `golfers love` | Feature/story where the golfer directly experiences or benefits from the functionality |
| `course operators love` | Feature/story where the course operator directly experiences or benefits from the functionality |
| `v1` | Core MVP — must ship for launch |
| `v2` | Enhanced — post-MVP improvements |
| `v3` | Future — long-term roadmap items |
| `agentic` | Issue is managed by the automated agent pipeline (required for agents to process it) |
| `agent/business-analyst` | Assign issue to Business Analyst agent |
| `agent/architect` | Assign issue to Architect agent |
| `agent/ux-designer` | Assign issue to UX Designer agent |
| `agent/backend-developer` | Assign issue to Backend Developer agent |
| `agent/frontend-developer` | Assign issue to Frontend Developer agent |
| `agent/reviewer` | Assign issue to Code Reviewer agent |
| `agent/devops` | Assign issue to DevOps Engineer agent |

Apply audience labels based on who benefits — many features get **both** `golfers love` and `course operators love` (see "Features Both Golfers AND Courses Will Love" in the roadmap). Always apply exactly one version label (`v1`, `v2`, or `v3`) based on the roadmap tier. The `agentic` label opts an issue into the automated pipeline — without it, agents ignore the issue. Agent `agent/*` labels are managed by the pipeline — see below.

## Agent Pipeline

This project uses an automated multi-agent pipeline via GitHub Actions. See `.claude/skills/agent-pipeline/SKILL.md` for the full protocol.

- **Workflow files:** `.github/workflows/claude-pm.yml` (orchestrator), `.github/workflows/claude-agents.yml` (dispatch)
- **Agent definitions:** `.claude/agents/*.md`
- **Pipeline statuses:** Triage → Needs Story → **Story Review** (owner gate) → Needs Architecture → **Architecture Review** (owner gate) → Ready → Implementing → CI Pending → In Review → Changes Requested → **Ready to Merge** (owner gate) → Done

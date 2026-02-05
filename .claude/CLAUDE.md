# Shadowbrook — Tee Time Booking Platform

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
- Frontend: React 19, TypeScript 5.9, Vite 7
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
- TypeScript: strict mode, ES modules, no CommonJS
- Prefer existing patterns in the codebase over introducing new ones

## Workflow
- Run `dotnet build shadowbrook.slnx` after C# changes to verify compilation
- Run `pnpm --dir src/web lint` after TypeScript changes
- Prefer running single tests over full suites for speed
- Worktrees go in `.worktrees/` (gitignored)
- `dotnet-ef` tool path: `export PATH="$PATH:/home/aaron/.dotnet/tools"`

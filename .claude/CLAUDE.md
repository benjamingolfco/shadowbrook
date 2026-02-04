# Shadowbrook — Tee Time Booking Platform

## Build & Run
- API: `dotnet run --project src/api`
- API build: `dotnet build shadowbrook.slnx`
- Web install: `pnpm install --dir src/web`
- Web dev: `pnpm --dir src/web dev`
- Web build: `pnpm --dir src/web build`
- Web lint: `pnpm --dir src/web lint`

## Tech Stack
- Backend: .NET 10, EF Core 10, Azure SQL, OpenAPI
- Frontend: React 19, TypeScript 5.9, Vite 7
- Package manager: pnpm (never npm or yarn)
- SMS: ITextMessageService abstraction (Twilio planned)
- Infra: Azure (planned)

## Project Structure
- src/api/ — .NET Web API (controllers, services, EF data context)
- src/web/ — React SPA
- docs/ — Documentation
- infra/ — Azure deployment config (planned)

## Code Conventions
- C#: file-scoped namespaces, nullable reference types, implicit usings
- TypeScript: strict mode, ES modules, no CommonJS
- Prefer existing patterns in the codebase over introducing new ones

## Workflow
- Run `dotnet build shadowbrook.slnx` after C# changes to verify compilation
- Run `pnpm --dir src/web lint` after TypeScript changes
- Prefer running single tests over full suites for speed

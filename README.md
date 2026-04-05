# Teeforce

[![Deploy Test](https://github.com/benjamingolfco/teeforce/actions/workflows/deploy-test.yml/badge.svg)](https://github.com/benjamingolfco/teeforce/actions/workflows/deploy-test.yml)
[![E2E Tests](https://github.com/benjamingolfco/teeforce/actions/workflows/e2e.yml/badge.svg)](https://github.com/benjamingolfco/teeforce/actions/workflows/e2e.yml)

Tee time booking platform for golf courses.

> Private repository — [BenjaminGolfCo](https://github.com/benjamingolfco)

## Principles

1. **Zero Training Required** — both golfers and operators are productive immediately
2. **Event-Driven Backend** — domain events, not service coupling, for resiliency and scalability
3. **SMS is the Communication Channel** — web for actions, SMS for golfer communication
4. **Multi-Tenant from Day One** — shared infrastructure, isolated course data and configuration
5. **Configuration Without Opinions** — sensible defaults, full operator control

## Test Environment

- **Frontend:** https://purple-field-0a3932a0f.4.azurestaticapps.net
- **API:** https://teeforce-app-test.wittywave-545ed3d5.eastus2.azurecontainerapps.io

## Tech Stack

- **Backend:** .NET 10 Web API with EF Core 10 (Azure SQL)
- **Frontend:** React 19 + TypeScript + Vite 7
- **SMS:** Abstracted text message service (Twilio planned)
- **Infra:** Azure (planned)

## Getting Started

### API (`src/api`)

```bash
dotnet run --project src/api
```

Runs on `https://localhost:5001` by default. Health check at `GET /health`.

### Web (`src/web`)

```bash
cd src/web
pnpm install
pnpm dev
```

Runs on `http://localhost:5173` by default.

### Build

```bash
# API
dotnet build teeforce.slnx

# Web
cd src/web && pnpm build
```

## Project Structure

```
src/
  api/          .NET 10 Web API
  web/          React + Vite + TypeScript
docs/           Project documentation
infra/          Azure deployment config (planned)
```

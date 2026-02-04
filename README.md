# Shadowbrook

Tee time booking platform for golf courses.

> Private repository — [BenjaminGolfCo](https://github.com/benjamingolfco)

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
dotnet build shadowbrook.slnx

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

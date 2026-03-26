.PHONY: help dev db api web build test lint e2e clean down logs

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-15s\033[0m %s\n", $$1, $$2}'

dev: ## Run API (Docker) + frontend dev server
	@echo "Starting API in Docker (http://localhost:5221) and Web (http://localhost:3000)"
	docker compose up -d --build
	@echo "API logs: make logs"
	pnpm --dir src/web dev

db: ## Start SQL Server container
	docker compose up db -d

api: ## Run the .NET API natively (requires SQL Server: make db)
	dotnet run --project src/backend/Shadowbrook.Api

web: ## Run the Vite dev server
	pnpm --dir src/web dev

build: ## Build everything
	dotnet build shadowbrook.slnx
	pnpm --dir src/web build

test: ## Run all backend tests
	dotnet test tests/Shadowbrook.Domain.Tests
	dotnet test tests/Shadowbrook.Api.Tests

lint: ## Lint the frontend
	pnpm --dir src/web lint

e2e: ## Run Playwright e2e tests
	pnpm --dir src/web e2e

clean: ## Clean build artifacts
	dotnet clean shadowbrook.slnx
	rm -rf src/web/dist

down: ## Stop Docker containers
	docker compose down

logs: ## Follow API container logs
	docker compose logs -f api

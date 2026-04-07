.PHONY: help dev db api web build test lint e2e clean down kill-web logs

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
	dotnet run --project src/backend/Teeforce.Api

web: ## Run the Vite dev server
	pnpm --dir src/web dev

build: ## Build everything
	dotnet build teeforce.slnx
	pnpm --dir src/web build

test: ## Run all backend tests
	dotnet test tests/Teeforce.Domain.Tests
	dotnet test tests/Teeforce.Api.Tests

lint: ## Lint the frontend
	pnpm --dir src/web lint

e2e: ## Run Playwright e2e tests
	pnpm --dir src/web e2e

clean: ## Clean build artifacts
	dotnet clean teeforce.slnx
	rm -rf src/web/dist

clean-hard: ## Nuke all bin/obj dirs (fixes permission issues from Docker/sandbox)
	rm -rf src/backend/*/obj/ src/backend/*/bin/
	rm -rf tests/*/obj/ tests/*/bin/
	rm -rf src/web/dist

down: kill-web ## Stop Docker containers and the Vite dev server
	docker compose down

kill-web: ## Kill the Vite dev server on :3000
	@pid=$$(lsof -ti:3000 2>/dev/null); \
	if [ -n "$$pid" ]; then \
		echo "Killing Vite on :3000 (pid $$pid)"; \
		kill $$pid 2>/dev/null || true; \
	else \
		echo ":3000 free"; \
	fi

logs: ## Follow API container logs
	docker compose logs -f api

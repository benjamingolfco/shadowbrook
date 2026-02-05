.PHONY: help dev api web build test lint clean

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-15s\033[0m %s\n", $$1, $$2}'

dev: ## Run backend and frontend together
	@echo "Starting API on http://localhost:5221 and Web on http://localhost:3000"
	@$(MAKE) -j2 api web

api: ## Run the .NET API
	dotnet run --project src/api

web: ## Run the Vite dev server
	pnpm --dir src/web dev

build: ## Build everything
	dotnet build shadowbrook.slnx
	pnpm --dir src/web build

test: ## Run API tests
	dotnet test tests/api

lint: ## Lint the frontend
	pnpm --dir src/web lint

clean: ## Clean build artifacts
	dotnet clean shadowbrook.slnx
	rm -rf src/web/dist

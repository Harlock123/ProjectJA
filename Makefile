.PHONY: restore build run \
        db db-stop db-shell db-clean \
        up down \
        test test-unit test-arch test-integration \
        migrate provision \
        env

# ── local dev ──────────────────────────────────────────────────────────────────

restore:
	dotnet restore

build: restore
	dotnet build

run:
	dotnet run --project src/ProjectJA.Host

# ── postgres container ─────────────────────────────────────────────────────────

db:
	docker run --rm -d --name projectja-pg \
	  -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=projectja \
	  -p 5432:5432 -v projectja-pgdata:/var/lib/postgresql \
	  postgres:18-alpine

db-stop:
	docker stop projectja-pg

db-shell:
	docker exec -it projectja-pg psql -U postgres -d projectja

db-clean:
	docker rm -f projectja-pg
	docker volume rm projectja-pgdata

# ── docker compose (on-prem stack) ─────────────────────────────────────────────

env:
	cp .env.example .env

up:
	docker compose -f deploy/docker-compose.onprem.yml up --build

down:
	docker compose -f deploy/docker-compose.onprem.yml down

# ── tests ──────────────────────────────────────────────────────────────────────

test: test-unit test-arch

test-unit:
	dotnet test tests/ProjectJA.UnitTests

test-arch:
	dotnet test tests/ProjectJA.ArchitectureTests

test-integration:
	dotnet test tests/ProjectJA.IntegrationTests

# ── tools ──────────────────────────────────────────────────────────────────────

# Apply pending migrations to every tenant DB.
# Set ConnectionStrings__TenantDirectory before calling.
migrate:
	dotnet run --project tools/Migrator

# Provision a new tenant (SaaS mode).
# Usage: make provision SLUG=acme NAME="ACME Corp" EMAIL=admin@acme.example.com
SLUG  ?= acme
NAME  ?= My Org
EMAIL ?= admin@example.com

provision:
	dotnet run --project tools/Provisioning -- create \
	  --slug $(SLUG) \
	  --name "$(NAME)" \
	  --admin-email $(EMAIL)

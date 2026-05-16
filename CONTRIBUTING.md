# Contributing to ProjectJA

Thanks for your interest. A few ground rules.

## Licensing of contributions

ProjectJA is distributed under the Business Source License 1.1 (see `LICENSE`). To accept outside contributions while preserving the ability to relicense future releases under Apache 2.0, all contributors must sign the Contributor License Agreement in `CLA.md`. A CLA bot will request your signature on your first pull request.

## Development setup

Prerequisites:
- .NET 9 SDK (pinned in `global.json`)
- Docker + docker-compose (for the integration tests and local stack)
- A POSIX shell (for the scripts in `tools/` and `deploy/`)

Common commands:
```
dotnet restore
dotnet build
dotnet test
docker compose -f deploy/docker-compose.onprem.yml up --build
```

## Code style

- Nullable reference types are enabled solution-wide.
- Prefer file-scoped namespaces and minimal `using` blocks.
- Modules talk to each other only through their `Contracts/` folder — never reach into another module's `Domain/`, `Application/`, or `Persistence/`. Architecture tests enforce this.
- Every aggregate root carries a `TenantId`. The `AppDbContext` applies a tenant filter; do not bypass it.

## Reporting issues

Use GitHub issues with reproduction steps and the relevant log excerpt. Security-sensitive reports should go to the address in `SECURITY.md` (TBD).

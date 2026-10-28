# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers the features, observability, OpenAPI,
deployment and tenant tools in detail, and `STARTUP.md` walks through a first local run. `ProjectJAbeginning.md`
holds the original design decisions. This file covers how the code fits together and the rules that keep it
working.

## What this is

A Jira-style issue tracker that runs as multi-tenant SaaS or single-tenant on-prem. It is a modular monolith on
.NET 10: Blazor Interactive Server with MudBlazor for the UI, ASP.NET Core Minimal APIs, SignalR (optionally over
Redis) for live boards, and EF Core 10 on PostgreSQL with one database per tenant. Background jobs use Coravel;
attachments go to S3-compatible storage. Licensed BSL 1.1 (see `LICENSE`, `CLA.md` and `CONTRIBUTING.md`).

## Commands

```bash
docker compose -f deploy/docker-compose.dev.yml up -d   # Postgres on 5432, MinIO on 9000/9001, the attachments bucket
dotnet build ProjectJA.sln                              # SDK 10.0.300 or later (global.json)
dotnet run --project src/ProjectJA.Host                 # migrates, seeds the default tenant, org and admin, then serves
dotnet test tests/ProjectJA.ArchitectureTests           # module boundaries
dotnet test tests/ProjectJA.IntegrationTests            # needs Docker running (Testcontainers starts postgres:16-alpine)
dotnet tool restore                                     # dotnet-ef 10.0.8, pinned in .config/dotnet-tools.json
dotnet ef migrations add <Name> --project src/ProjectJA.Infrastructure --startup-project src/ProjectJA.Host \
  --context AppDbContext --output-dir Persistence/Migrations
dotnet run --project tools/Provisioning -- create --slug acme --name "ACME Corp" --admin-email admin@acme.example.com
dotnet run --project tools/Migrator                     # apply migrations to every tenant in the directory
```

- `tests/ProjectJA.UnitTests` has a project file but no tests yet.
- The seeded login is `admin@projectja.local` / `ChangeMe123!` unless `Bootstrap:AdminEmail` and
  `Bootstrap:AdminPassword` are set.
- Attachments need the `Storage` settings in `STARTUP.md`; without them uploads fail (`NullObjectStore`).

## Layout

- `src/ProjectJA.Host/`: the composition root. `Program.cs` wires services, auth, Serilog, OpenTelemetry, health
  checks, OpenAPI/Scalar and every module.
  - `Components/` holds the Blazor pages, dialogs and layout; `Hubs/` the SignalR board hub and Redis bridge.
  - `Authentication/` covers per-tenant OIDC, `Endpoints/` login and the startup bootstrap, and `Theming/` themes,
    avatars and tooltips. `wwwroot/js/` has the drag-and-drop and upload scripts.
- `src/ProjectJA.SharedKernel/`: cross-cutting primitives with no module dependencies. Entity and aggregate bases,
  `Result`, tenancy (`ITenantContext`, `TenantId`, `UseTenant`), `IClock`, and the email, storage, audit,
  notification and realtime interfaces.
- `src/ProjectJA.Infrastructure/`: implementations. `Persistence/AppDbContext` (one context for every module's
  tables) and its migrations, `Tenancy/` (the tenant directory, its own context and migrations, resolution
  middleware and circuit handler), `Email/` (outbox plus Postmark, SMTP or logging dispatchers), `Storage/` (S3 or
  null) and `DependencyInjection.cs`.
- `src/Modules/ProjectJA.Modules.*`: Identity, Projects, Issues, Workflows, Boards, Search, Notifications and Audit.
  - Each module has `Module.cs` (an `Add<Name>Module` and a `Map<Name>Endpoints` extension) plus `Contracts/`
    (public interfaces and DTOs), `Domain/`, `Application/`, `Persistence/` (EF configurations) and `Endpoints/`.
  - Boards and Search are empty shells so far: search lives in Issues (`IssueSearch`) and the board in the Host's
    pages and hub.
- `tests/`: `ArchitectureTests` (NetArchTest), `IntegrationTests` (xUnit, `WebApplicationFactory<Program>` and
  Testcontainers; `Fixtures/AppFactory.cs`), and the empty `UnitTests`.
- `tools/Provisioning` creates a tenant database and admin; `tools/Migrator` migrates every tenant.
- `deploy/`: the multi-stage `Dockerfile` (host, migrator and provisioning in one image), compose files for SaaS,
  on-prem and dev, the Caddy config and `scripts/deploy.sh` for server roll-outs.
- `SAMPLEINPUTFILES/`: a sample WBS spreadsheet for trying the Excel issue import. `scripts/start-pg.sh` revives a
  standalone dev Postgres container.

## Rules that keep it working

- **Modules talk through `Contracts/` only.** A module may use another module's `Contracts` namespace, never its
  `Domain`, `Application`, `Persistence` or `Endpoints`. `ModuleBoundaryTests` fails the build otherwise. A new
  module must be added to that test's list, registered in `Program.cs` (`Add...Module`, `Map...Endpoints`) and
  referenced from Host and Infrastructure.
- **Tenancy is one database per tenant**, not a row filter. `AppDbContext` is created per scope with the
  connection string of the current `ITenantContext`. The tenant is set by `TenantResolutionMiddleware` (subdomain
  when `Tenancy:Mode` is `SaaS`, the seeded default on-prem) and by `TenantCircuitHandler` for Blazor circuits.
  Code outside a request (jobs, tools, bootstrap) must set it with `IServiceProvider.UseTenant(...)`.
  `CONTRIBUTING.md` still mentions a `TenantId` filter on `AppDbContext`; there is none.
- **Schema changes go through migrations.** Add the entity's `DbSet` and `ApplyConfiguration` call in
  `AppDbContext`, then add a migration with the command above. The design-time factories exist so `dotnet ef` works
  without a resolved tenant. The app migrates its own database at start-up; `tools/Migrator` does the rest.
- **Prefer `IClock`** (registered as `SystemClock`) over `DateTime.UtcNow` for the current time.
- **Licence header.** Every source file starts with `// SPDX-License-Identifier: BUSL-1.1`; keep it on new files
  (generated migrations excepted).
- **Style.** `.editorconfig` sets LF line endings, UTF-8, four-space C# and file-scoped namespaces (a warning).
  Nullable reference types are on in every project.
- **Secrets stay out of the repo.** Connection strings, storage keys, Postmark and OIDC settings come from
  configuration or `Section__Key` environment variables; the values in the docs are local-dev defaults.

## Making a change

- Run the architecture tests on every change. Run the integration tests (Docker needed) when you touch persistence,
  tenancy, auth or endpoints, and add one for a new flow.
- Endpoints: follow the Projects and Issues modules (`WithName`, `WithSummary`, `WithTags`, `Produces<T>`) so they
  show properly in `/scalar/v1`.
- Keep `README.md` in step with features, settings and operational changes.

## CI and releases

- `.github/workflows/ci.yml` runs on pull requests to `main` and pushes to `main`: tool restore, a Release build,
  then the unit and architecture tests. The integration tests run only on pushes to `main`. Test results are kept
  as a `.trx` artifact for 14 days.
- `.github/workflows/deploy.yml` runs on a `v*.*.*` tag or by hand. It builds `deploy/Dockerfile` for `linux/amd64`
  and `linux/arm64` and pushes it to `ghcr.io/<owner>/projectja`, tagged with the version, `latest` (on tags) and
  the commit SHA.

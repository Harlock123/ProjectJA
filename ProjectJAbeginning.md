# ProjectJA — Architectural Beginning

A lightweight, dual-deployable (SaaS + on-prem) Jira-style issue tracker built on .NET 9. Designed explicitly for small/mid organizations that want capability without the plugin-ecosystem instability of large Jira installs.

---

## Part 1 — Options Considered

### Frontend — the "not React" question

**Primary candidate: Blazor (Interactive Auto render mode) in .NET 9.**
- One language (C#) for full stack. No npm tree, no webpack/vite gymnastics, no React 19 churn.
- "Auto" mode in .NET 8+ does SSR for first paint, then upgrades to WASM for interactivity — feels fast, low bandwidth on cold start, no SignalR dependency for the long term.
- MudBlazor or Radzen give a Jira-like component set (tables, drag-drop boards, dialogs) without building from scratch.

**Leaner alternative: HTMX + Razor Pages.** Server-rendered HTML, sprinkles of interactivity via `hx-*` attributes. Zero JS framework. Drag-drop kanban needs a small JS helper (SortableJS, ~12KB). Genuinely minimal — most interactions are "submit a form, swap a fragment."

Skip React/Angular/Vue. If a JS framework is non-negotiable, Svelte is the least bloated.

### Backend / API

**Primary candidate: ASP.NET Core with FastEndpoints (or Minimal APIs) in a modular monolith.**
- **Modular monolith, not microservices.** Microservices kill the on-prem story — small orgs can't run Kubernetes. One deployable, internally organized as modules.
- **FastEndpoints** over MediatR — MediatR is now commercial. FastEndpoints gives REPR-pattern endpoints that are easy to test and discover. Minimal APIs are also fine with zero dependencies.
- **REST over GraphQL.** GraphQL is overkill and complicates on-prem ops.
- **SignalR** for live bits (board updates, comments, notifications). Built-in, works on-prem trivially.

### Database

**Primary candidate: PostgreSQL + EF Core 9.**
- Free, no licensing landmines for on-prem (vs. SQL Server licensing).
- JSONB columns are ideal for Jira-style custom fields without schema explosion.
- Full-text search built in — no Elasticsearch needed for a long time.
- EF Core 9 is solid; drop to Dapper for the 2–3 hot read paths if needed.

Migrations run on app startup so on-prem installs are zero-touch.

### Multi-tenancy

| Strategy | Isolation | Ops cost | On-prem fit |
|---|---|---|---|
| Shared schema + `TenantId` column | Weak | Lowest | Trivial (1 tenant row) |
| Schema-per-tenant | Medium | Medium | Easy |
| **Database-per-tenant** | **Strong** | **Higher** | **Natural — on-prem is just one DB** |

### Auth

- **ASP.NET Core Identity** for local accounts (works on-prem out of the box).
- **OpenIddict** for OIDC so SaaS customers can SSO with Google/Microsoft/Okta. Same code path both modes.
- Duende IdentityServer is more polished but paid above a revenue threshold — bad for on-prem story.

### Packaging / deployment

- Same Docker image + docker-compose.yml for both modes.
- SaaS: deploy on Fly.io, Hetzner, AWS ECS, or a single fat VM with Caddy. Skip K8s until forced.
- On-prem: `docker compose up`. App + Postgres + reverse proxy. One command.
- Self-contained .NET publish so on-prem doesn't need a runtime installed.
- Background jobs: Hangfire, Quartz.NET, or Coravel (license tradeoffs differ — see below).

### Anti-bloat principles

- One repo, one deployable, one database engine.
- No message broker until there's a real reason.
- No microservices.
- No plugin system in v1. Jira's instability at big orgs *is* the plugin system. Use clean module boundaries inside the monolith and offer webhooks for outside integrations.
- Feature flags via a simple table, not LaunchDarkly.

---

## Part 2 — Final Picks (minimizing interdependencies, licensing risk, weight)

Applying the filter — **fewest dependencies, permissive licenses only, smallest runtime footprint** — here is the locked-in stack:

| Layer | Pick | License | Why this over alternatives |
|---|---|---|---|
| UI rendering | **Blazor Interactive Server** (.NET 9) | MIT | No WASM payload, no npm, no double-runtime. Auto mode is an easy upgrade later. Server mode is the lightest and the simplest mental model. |
| UI components | **MudBlazor** | MIT | Single dependency, broad component coverage (tables, dialogs, drag-drop helpers). Radzen's studio is commercial — avoid the upsell pressure. |
| Real-time | **SignalR** (built-in) | MIT | Already shipped with ASP.NET Core. No new dep. |
| HTTP API | **ASP.NET Core Minimal APIs** | MIT | Zero extra dependency. FastEndpoints is great but adds a library; Minimal APIs in .NET 9 are now expressive enough on their own. |
| In-process messaging | **Hand-rolled `IDomainEventDispatcher`** over `IServiceProvider` | n/a | MediatR went commercial. Wolverine is heavy. A 40-line dispatcher is enough. |
| Validation | **FluentValidation** | Apache 2.0 | Mature, MIT-compatible, no commercial drift risk. |
| ORM | **EF Core 9** | MIT | Bundled with .NET. Migrations, LINQ, JSON column support. Dapper only if a hot path demands it. |
| Database | **PostgreSQL 16+** | PostgreSQL License (permissive) | Free for SaaS and on-prem, no per-core fees, JSONB, FTS. |
| AuthN/AuthZ | **ASP.NET Core Identity + OpenIddict** | MIT / Apache 2.0 | Both permissive. Local accounts on-prem; OIDC for SaaS SSO. |
| Multi-tenancy | **Database-per-tenant** | n/a | On-prem deployment becomes "the one tenant." Strong isolation. `pg_dump` per-tenant export. |
| Background jobs | **Coravel** | MIT | Hangfire core is LGPL — distributing a closed-source on-prem build is awkward. Quartz.NET (Apache 2.0) is fine but heavier. Coravel is MIT, lightweight, in-process, no extra storage needed for simple scheduling. |
| Logging | **Serilog** + Console/File sinks | Apache 2.0 | Standard, permissive, no SaaS lock-in. |
| Reverse proxy (on-prem) | **Caddy** | Apache 2.0 | Automatic HTTPS via Let's Encrypt, single binary, one-file config. |
| Containerization | **Docker + docker-compose** | Apache 2.0 / permissive | Universal, no K8s requirement on-prem. |
| Object storage | **Cloudflare R2 (SaaS) / MinIO (on-prem)** | Cloudflare standard / MIT | One S3-compatible API in code. R2 has zero egress fees — critical on an attachment-heavy product. MinIO is a single MIT binary that drops into docker-compose. |
| SaaS hosting | **Hetzner** (dedicated server or paired VPS) | n/a | Best $/perf for a database-per-tenant model on a single Postgres cluster. Same Docker config you ship on-prem. Move Postgres to its own box when shared compute is outgrown. |
| Source license | **BSL 1.1 → Apache 2.0 (3-year change date)** | BUSL-1.1 | Source-available so on-prem customers can audit/modify; blocks competing hosted services during the window; auto-converts to Apache 2.0 after 3 years. Additional Use Grant carves out legitimate on-prem use by end users. |
| DB topology | **One Postgres cluster, one DB per tenant, PgBouncer (transaction pooling)** | PostgreSQL / MIT | PgBouncer multiplexes thousands of client connections onto a small backend pool — the only way "one cluster, many DBs" scales past a few hundred tenants. On-prem skips PgBouncer entirely. |
| SMTP client (on-prem) | **MailKit** | MIT | Canonical .NET SMTP/IMAP client. Customers point it at their own relay. |
| Transactional email (SaaS) | **Postmark** | commercial SaaS (no source-license impact) | Best transactional deliverability, opinionated about transactional-only, simple API. SES requires reputation management; SendGrid has slipped. Single provider — no multi-driver tax. |
| Audit log | **In-house `Modules.Audit` + `IAuditLog` contract** | n/a | Domain-significant events captured explicitly at the Application layer. Avoids EF-change-tracker noise. `pgaudit` is available as an opt-in supplement for forensic depth. |

**What was rejected and why:**
- *MediatR* — recently moved to a commercial license. Replaced with a hand-rolled dispatcher.
- *Hangfire (core)* — LGPL 3.0. Distribution obligations are messy for a sold/on-prem product. Replaced with Coravel.
- *Duende IdentityServer* — commercial above $1M revenue. Replaced with OpenIddict.
- *Radzen Studio* — commercial scaffolding tool. MudBlazor stays purely OSS.
- *FastEndpoints* — fine library, but Minimal APIs cover the need with one fewer dependency.
- *SQL Server* — licensing pressure on on-prem customers.
- *React / Angular / Vue* — bundler + node toolchain + framework churn. Out of scope for a .NET-first lean build.
- *Elasticsearch* — Postgres FTS is enough until it isn't.
- *Kubernetes* — operational overhead that no small org can absorb.

---

## Part 3 — Solution Structure & Module Boundaries

### Top-level layout

```
ProjectJA/
├── src/
│   ├── ProjectJA.Host/                       # Composition root: Blazor + API + DI wiring
│   │   ├── Program.cs
│   │   ├── Components/                       # Blazor pages, layouts, shared UI
│   │   │   ├── App.razor
│   │   │   ├── Routes.razor
│   │   │   ├── Layout/
│   │   │   └── Pages/
│   │   ├── Endpoints/                        # Minimal API endpoint registrations per module
│   │   ├── Hubs/                             # SignalR hubs (BoardHub, NotificationHub)
│   │   ├── Middleware/                       # TenantResolutionMiddleware, etc.
│   │   ├── wwwroot/
│   │   └── appsettings.json
│   │
│   ├── ProjectJA.SharedKernel/               # Cross-cutting primitives (zero deps on modules)
│   │   ├── Domain/                           # Entity, ValueObject, AggregateRoot, IDomainEvent
│   │   ├── Results/                          # Result<T>, Error types
│   │   ├── Time/                             # IClock abstraction
│   │   ├── Tenancy/                          # ITenantContext, TenantId
│   │   ├── Messaging/                        # IDomainEventDispatcher (hand-rolled)
│   │   ├── Email/                            # IEmailSender, EmailMessage record
│   │   └── Audit/                            # IAuditLog contract
│   │
│   ├── ProjectJA.Infrastructure/             # Persistence, identity, integrations
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs               # Single DbContext; modules contribute configs
│   │   │   ├── Migrations/
│   │   │   └── TenantConnectionResolver.cs   # Picks per-tenant connection string (PgBouncer host in SaaS)
│   │   ├── Identity/                         # ASP.NET Identity + OpenIddict wiring
│   │   ├── BackgroundJobs/                   # Coravel scheduler registration
│   │   ├── Storage/                          # IObjectStore + S3ObjectStore (R2 / MinIO / S3)
│   │   └── Email/
│   │       ├── SmtpEmailSender.cs            # MailKit — on-prem default
│   │       ├── PostmarkEmailSender.cs        # SaaS default
│   │       ├── EmailQueueJob.cs              # Coravel job: retry w/ backoff, dead-letter
│   │       └── Templates/                    # Razor email templates
│   │
│   └── Modules/
│       ├── ProjectJA.Modules.Identity/       # Orgs, users, roles, invitations
│       ├── ProjectJA.Modules.Projects/       # Projects, components, versions
│       ├── ProjectJA.Modules.Issues/         # Issues, comments, attachments, links
│       ├── ProjectJA.Modules.Workflows/      # Statuses, transitions, rules
│       ├── ProjectJA.Modules.Boards/         # Kanban/scrum boards, sprints
│       ├── ProjectJA.Modules.Search/         # Postgres FTS abstraction
│       ├── ProjectJA.Modules.Notifications/  # In-app + email notifications
│       └── ProjectJA.Modules.Audit/          # AuditEvent entity, monthly partitions, retention job
│
├── tests/
│   ├── ProjectJA.UnitTests/
│   ├── ProjectJA.IntegrationTests/           # Testcontainers + real Postgres
│   └── ProjectJA.ArchitectureTests/          # Enforce module boundaries (NetArchTest)
│
├── tools/
│   ├── Provisioning/                         # CLI: create tenant DB, seed admin, register in tenant directory
│   │   └── Program.cs                        # `dotnet run --project tools/Provisioning ...`
│   └── Migrator/                             # CLI: iterate tenant registry and apply pending migrations
│       └── Program.cs                        # Run on deploy
│
├── deploy/
│   ├── Dockerfile
│   ├── docker-compose.yml                    # SaaS: app + postgres + pgbouncer + caddy (R2 for storage)
│   ├── docker-compose.onprem.yml             # On-prem: app + postgres + caddy + minio (no pgbouncer)
│   └── caddy/Caddyfile
│
├── docs/
│   ├── ProjectJAbeginning.md                 # this document
│   ├── architecture/
│   └── runbooks/
│
├── LICENSE                                   # BSL 1.1 with Change Date + Apache 2.0 Change License
├── CONTRIBUTING.md                           # Contribution guide + CLA pointer
├── CLA.md                                    # Contributor License Agreement (signed via bot)
├── ProjectJA.sln
└── README.md
```

### Module internal layout (every `ProjectJA.Modules.*` project)

```
ProjectJA.Modules.Issues/
├── Module.cs                                 # public static AddIssuesModule(IServiceCollection)
├── Contracts/                                # PUBLIC: what other modules may consume
│   ├── IIssueQueries.cs                      # cross-module read interface (no entities exposed)
│   ├── Events/                               # PUBLIC integration events (IssueCreated, etc.)
│   └── Dtos/
├── Domain/                                   # INTERNAL: aggregates, entities, value objects
│   ├── Issue.cs
│   ├── Comment.cs
│   └── Events/                               # INTERNAL domain events
├── Application/                              # INTERNAL: use cases / handlers
│   ├── Commands/
│   ├── Queries/
│   └── Validators/                           # FluentValidation
├── Persistence/                              # INTERNAL: EF configurations, repository impls
│   ├── IssueConfiguration.cs
│   └── IssueRepository.cs
└── Endpoints/                                # INTERNAL: Minimal API endpoint group
    └── IssuesEndpoints.cs                    # MapIssuesEndpoints(IEndpointRouteBuilder)
```

### Module boundary rules (enforced by architecture tests)

1. **Modules may reference another module's project, but only to access its `Contracts/` namespace.** Reaching into another module's `Domain/`, `Application/`, `Persistence/`, or `Endpoints/` namespaces is forbidden and enforced by `tests/ProjectJA.ArchitectureTests/ModuleBoundaryTests.cs`.
2. **No cross-module DB joins.** Each module owns its tables. If module B needs data from module A, it goes through `A.Contracts.IXxxQueries` or subscribes to A's integration events.
3. **Domain events are in-process only and stay inside a module.** Anything another module needs becomes an *integration event* defined in `Contracts/Events/` and published through `IDomainEventDispatcher`.
4. **The Host project is the only place that knows every module exists.** It calls each module's `AddXxxModule(...)` and `MapXxxEndpoints(...)`.
5. **Tenancy is non-negotiable.** Every aggregate root carries `TenantId`. The `AppDbContext` applies a global query filter keyed off `ITenantContext`. On-prem just resolves to the single seeded tenant.
6. **Modules consume the base `DbContext` type, not `AppDbContext` directly.** `AppDbContext` lives in `Infrastructure` and references every module to know entity types — so modules cannot reference it back (project-reference cycle). Infrastructure registers a scoped `DbContext` resolver that returns the same `AppDbContext` instance, and modules inject `DbContext` and use `db.Set<T>()`. This keeps endpoints living in `Module/Endpoints/` while preserving a single migration history.

### Request lifecycle (one paragraph)

A browser hits the Blazor Server circuit or a Minimal API endpoint. `TenantResolutionMiddleware` reads the subdomain (SaaS) or returns the default tenant (on-prem) and populates `ITenantContext`. `TenantConnectionResolver` selects the per-tenant Postgres connection string. The endpoint or Blazor component dispatches a command into the relevant module's `Application/` layer, which loads aggregates via EF Core, mutates them, raises domain events, and persists. The dispatcher fans events out to in-process handlers (including notification publishing). SignalR hubs broadcast board/issue updates to connected clients in the same tenant scope.

### First vertical slice to validate the stack

Build end-to-end before any horizontal breadth:

1. Create an organization (tenant) + first admin user.
2. Create a project with three statuses (Todo / Doing / Done).
3. Create, edit, comment on, and delete issues.
4. Drag-drop kanban board with live updates via SignalR.
5. Postgres FTS search across issue titles + descriptions.
6. SSO-ready login (local account + one OIDC provider stub).
7. `docker compose up` on a fresh machine and have it work.

If that slice is pleasant to build on this stack, the rest of a Jira-equivalent is more of the same.

---

## Part 4 — Decisions Locked In (2026-05-15)

**Endpoint location → endpoints live in `Module/Endpoints/`. Modules inject the base `DbContext` type; Infrastructure registers a scoped resolver that returns the `AppDbContext` instance.**
This was forced by a project-reference cycle: `AppDbContext` lives in Infrastructure and references every module to know entity types, so modules cannot reference Infrastructure back. Three options were weighed — endpoints in Host, per-module DbContexts, or base-`DbContext` injection — and base-`DbContext` injection won on simplicity (single migration history) and adherence to the doc's intent (modules self-contained). Per-module DbContexts would be the right answer for a plugin-extensible platform, which ProjectJA explicitly is not.
- *Codebase impact:* `Infrastructure/DependencyInjection.cs` registers `services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());`. Module endpoints use `[FromServices] DbContext db` and `db.Set<TEntity>()`. Cross-module reads go through `Contracts/IXxxQueries` interfaces — e.g. Issues uses `Modules.Projects.Contracts.IProjectQueries` to look up project metadata and allocate issue numbers, never `db.Set<Project>()` directly. A NetArchTest in `tests/ProjectJA.ArchitectureTests/ModuleBoundaryTests.cs` enforces that no module reaches into another module's `Domain`/`Application`/`Persistence`/`Endpoints` namespaces.

**SaaS hosting target → Hetzner (dedicated server or paired VPS), Docker + Caddy.**
A single Hetzner box can run one Postgres cluster hosting hundreds of tenant databases plus the app and Caddy. When Postgres needs its own host, move it without changing the Docker compose shape. Same configuration ships on-prem, so operating ProjectJA SaaS *is* operating what you sell.
- *Codebase impact:* a `tools/Provisioning/` CLI runs `CREATE DATABASE`, seeds the admin row, and registers the tenant in the directory. Hosting choice otherwise leaves application code untouched.

**File attachments → S3-compatible API everywhere. Cloudflare R2 in SaaS, MinIO on-prem.**
One code path. R2's zero-egress pricing protects margin on attachment-heavy workloads; MinIO (MIT) drops into docker-compose for on-prem.
- *Schema:* metadata in Postgres (`id`, `tenant_id`, `issue_id`, `filename`, `content_type`, `size_bytes`, `sha256`, `storage_key`, `uploaded_by`, `uploaded_at`). Bytes in object storage keyed `tenants/{tenantId}/attachments/{attachmentId}`. Pre-signed URLs for upload/download — the app never proxies bytes.
- *Codebase impact:* `ProjectJA.Infrastructure/Storage/` holds `IObjectStore` and a single `S3ObjectStore` implementation that uses the AWS SDK against a configurable endpoint URL (S3, R2, B2, MinIO all work unchanged). `appsettings.json` carries `Storage:Endpoint`, `Storage:Bucket`, `Storage:AccessKey`, `Storage:SecretKey`. Defer virus scanning and image thumbnails to v2.

**Source license → Business Source License 1.1 with a 3-year change date to Apache 2.0, plus an Additional Use Grant for on-prem end users.**
On-prem customers get full, auditable source. Competing hosted services are blocked during the BSL window; each release auto-converts to Apache 2.0 after 3 years. Avoids AGPL's chilling effect on enterprise buyers and SSPL's reputational baggage.
- *Additional Use Grant (drafted):* "You may make production use of the Licensed Work, provided that your use does not include offering the Licensed Work to third parties on a hosted or embedded basis."
- *Codebase impact:* `LICENSE` at repo root with the BSL 1.1 template (Change Date filled in per release, Change License = Apache 2.0, Use Grant inserted). Per-file SPDX header `// SPDX-License-Identifier: BUSL-1.1`. `CONTRIBUTING.md` and a CLA bot (CLA Assistant or EasyCLA) gate outside PRs so relicensing remains possible. On-prem shipments include source — there is no protection benefit to shipping only a binary under BSL.

**Per-tenant DB topology → one Postgres cluster, one DB per tenant, PgBouncer in transaction-pooling mode in front (SaaS only).**
Lowest ops cost at small/mid scale. PgBouncer multiplexes thousands of client connections onto a small backend pool — the only way "one cluster, many DBs" scales past a few hundred tenants. A dedicated-cluster premium tier can be added later for tenants who pay for isolation.
- *Codebase impact:* `TenantConnectionResolver` produces connection strings pointing at PgBouncer in SaaS (`Host=pgbouncer;Database=tenant_{slug};...`) and at Postgres directly on-prem. Per-tenant .NET pools are kept small (≤10) because PgBouncer is doing the real pooling. A `tools/Migrator` CLI iterates the tenant registry and applies pending migrations on deploy.

**Email delivery → one `IEmailSender` interface, two implementations. MailKit/SMTP on-prem, Postmark API in SaaS.**
Postmark is the transactional-only provider with the cleanest deliverability story; MailKit (MIT) is the canonical .NET SMTP client for on-prem customers pointing at their own relay. No multi-driver tax.
- *Codebase impact:* `IEmailSender` and `EmailMessage` live in `SharedKernel/Email/` so modules don't reference Infrastructure. `SmtpEmailSender` and `PostmarkEmailSender` both live in `Infrastructure/Email/`. Config switch: `Email:Driver = "smtp" | "postmark"`. Outbound mail is queued through a Coravel background job with exponential backoff and a dead-letter table. Templates are Razor, rendered in-process — no heavy templating dependency.

**Audit log scope → domain-significant events captured explicitly at the Application layer via `IAuditLog`. Not every database write.**
Aligned with the business question ("who did what to what"), not EF change-tracker noise. `pgaudit` remains available as an opt-in supplement when a customer needs DB-level forensics.
- *In scope by default:* resource create/update/delete on core entities, permission and role changes, authentication events (login, logout, failure, SSO link, password change), admin actions (settings, workflows, exports), and security-relevant denials. *Out of scope by default:* page views, `last_seen_at` ticks, search queries.
- *Retention:* 90 days default, configurable per tenant. Table partitioned by month for cheap drop-old-partition pruning.
- *Codebase impact:* new `ProjectJA.Modules.Audit/` module with `AuditEvent` entity (`id`, `tenant_id`, `actor_id`, `occurred_at`, `action`, `resource_type`, `resource_id`, `summary`, `detail_jsonb`, `ip`, `user_agent`). `IAuditLog` contract in `SharedKernel/Audit/`. Command handlers call `_audit.RecordAsync(...)` explicitly where audit-worthy. A daily Coravel job purges expired partitions. Admin search UI ships in v1.1 unless v1 has room.

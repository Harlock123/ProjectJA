# ProjectJA

A lightweight, dual-deployable (SaaS + on-prem) Jira-style issue tracker built on .NET 10 (LTS). Designed for small/mid organizations that want capability without the plugin-ecosystem instability of large Jira installs.

Architectural decisions and rationale live in [`ProjectJAbeginning.md`](ProjectJAbeginning.md).

## Stack at a glance

| Layer | Pick |
|---|---|
| UI | Blazor Interactive Server (.NET 10) + MudBlazor 9.4 |
| API | ASP.NET Core Minimal APIs |
| Real-time | SignalR |
| DB | PostgreSQL 16+ with EF Core 10 (database-per-tenant, PgBouncer in SaaS) |
| Auth | ASP.NET Core Identity + OpenIddict |
| Background jobs | Coravel |
| Object storage | Cloudflare R2 (SaaS) / MinIO (on-prem) — S3-compatible |
| Email | Postmark (SaaS) / MailKit SMTP (on-prem) |
| Reverse proxy | Caddy |
| Deploy | Docker + docker-compose |
| License | BSL 1.1 → Apache 2.0 (3-year change date) |

## Solution layout

```
src/
  ProjectJA.Host/             Composition root: Blazor + Minimal APIs + DI
  ProjectJA.SharedKernel/     Cross-cutting primitives (zero module deps)
  ProjectJA.Infrastructure/   Persistence, identity, storage, email
  Modules/
    Identity, Projects, Issues, Workflows, Boards, Search, Notifications, Audit
tests/
  UnitTests, IntegrationTests, ArchitectureTests
tools/
  Provisioning/   Tenant provisioning CLI
  Migrator/       Per-tenant migration runner
deploy/
  Dockerfile, docker-compose.yml (SaaS), docker-compose.onprem.yml, caddy/
```

## Getting started

```sh
dotnet restore
dotnet build
dotnet run --project src/ProjectJA.Host
```

For an on-prem-style local stack:
```sh
cp .env.example .env   # fill in secrets
docker compose -f deploy/docker-compose.onprem.yml up --build
```

## Status

First vertical slice in progress. The stack is proven end-to-end for the on-prem single-tenant path: a fresh `dotnet run --project src/ProjectJA.Host` against a running Postgres will create the schema, seed a default organization + admin user, and serve `/projects` and `/projects/{id}` pages backed by EF Core.

### Working
- EF Core 10 + Npgsql wired through `AppDbContext` (inherits `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`).
- ASP.NET Core Identity with local accounts; cookie auth via `IdentityConstants.ApplicationScheme`.
- Login + logout pages (Blazor static SSR forms calling `SignInManager`). `[Authorize]` on `/projects` and `/projects/{id}` with redirect to `/login`.
- Auth state shown in nav (current user + sign-out link, or sign-in link).
- Domain entities: `Organization`, `Project`, `Issue` (with owned `Comment` collection), `IssueStatus` enum (Todo/Doing/Done), `ApplicationUser`.
- Modules own their endpoints (`Module/Endpoints/`) and consume the base `DbContext` type. Cross-module reads via `Contracts/` interfaces (`IProjectQueries`, `IOrganizationQueries`).
- Architecture test (`NetArchTest`) enforces module boundaries on every build.
- **Multi-tenant connection switching is live.** `Tenant` entity + `TenantDirectoryDbContext` hold the meta-directory. `AppDbContext` is registered as a scoped factory that pulls the connection string from `ITenantContext` at construction time, so each request reaches a different tenant DB.
- **Tenant resolution paths:**
  - HTTP requests: `TenantResolutionMiddleware` reads subdomain in SaaS mode (`Tenancy:Mode=SaaS`) or returns the seeded default in on-prem mode; populates the scoped `TenantContext`.
  - Sign-in: `TenantStampedClaimsFactory` adds a `tenant_id` claim to the auth cookie at `SignInManager.PasswordSignInAsync` time.
  - Blazor circuits: `TenantCircuitHandler.OnCircuitOpenedAsync` reads `tenant_id` from the auth state and populates the circuit-scoped `TenantContext` — so SignalR interactions after the initial HTTP request can still reach the right tenant DB.
  - Out-of-request scopes (bootstrap, future CLIs/jobs): `IServiceProvider.UseTenant(info)` extension forces the scoped tenant explicitly.
- **Subdomain/claim mismatch defense (SaaS):** if an authenticated user hits a subdomain whose tenant ID doesn't match their cookie's `tenant_id` claim, `TenantResolutionMiddleware` signs them out (`SignOutAsync(IdentityConstants.ApplicationScheme)`) and 302s them to `/login`. Also catches legacy cookies without a `tenant_id` claim. On-prem is exempt — there's only one tenant.
- **Real EF Core migrations.** Initial migrations are checked in for both `AppDbContext` (`src/ProjectJA.Infrastructure/Persistence/Migrations/`) and `TenantDirectoryDbContext` (`src/ProjectJA.Infrastructure/Tenancy/Migrations/`). `IDesignTimeDbContextFactory` implementations on both let `dotnet ef migrations add` work despite the scoped-tenant DI registration. `StartupBootstrap` now calls `.Database.MigrateAsync()` instead of `EnsureCreatedAsync()`.
- **Tenant provisioning CLI** (`tools/Provisioning`): `dotnet run --project tools/Provisioning -- create --slug acme --name "ACME Corp" --admin-email admin@acme.example.com [--admin-password "Hunter2!"]`. Runs `CREATE DATABASE tenant_{slug}` on a maintenance connection, applies migrations to the new DB, registers the tenant in the directory, and seeds organization + admin user. Generates a strong password if `--admin-password` is omitted and prints it once.
- **Per-tenant migrator** (`tools/Migrator`): `dotnet run --project tools/Migrator`. Applies pending migrations to the tenant directory, then iterates every tenant in the directory and applies pending migrations to that tenant's `AppDbContext`. Exit code is non-zero if any tenant fails. Run on every deploy before swapping traffic.
- **Drag-drop kanban board with live updates** at `/projects/{id}/board`. Three columns (Todo/Doing/Done) using HTML5 drag-drop wired through `wwwroot/js/board-dnd.js` — a tiny JS module is required because Safari/Firefox refuse to start a drag unless `dataTransfer.setData()` is called synchronously in `dragstart`, which Blazor can't do from C#. The module attaches DOM listeners idempotently (guarded by a `data-dnd-bound` attribute) and invokes `[JSInvokable] OnCardDropped(issueId, statusInt)` on the Board component. On drop, the issue's status transitions, persists, and broadcasts via `IRealtimeNotifier`. Other clients viewing the same project see the move within milliseconds.
- **Realtime architecture:** `IRealtimeNotifier` is the publishing abstraction in `SharedKernel/Realtime/`. `SignalRRealtimeNotifier` (in Host) implements it by publishing to **three** sinks: the `BoardHub` (SignalR, for future mobile/API clients), an in-process `BoardEventStream` (for Blazor Server components running in the same process), and **optionally** a Redis pub/sub channel for cross-instance fan-out. Groups are scoped `tenant:{tid:N}:project:{pid:N}` so cross-tenant broadcasts are impossible. The hub itself is `[Authorize]` and reads the `tenant_id` claim to validate group membership.
- **SaaS scale-out via Redis backplane.** Set `ConnectionStrings:Redis` (or `Redis:ConnectionString`) and the app wires:
  - **SignalR**: `.AddStackExchangeRedis(...)` with channel prefix `projectja:signalr` so hub broadcasts on one instance reach SignalR clients connected to another.
  - **BoardEventStream**: `RedisBoardEventBridge` (hosted service) subscribes to `projectja:board`. `SignalRRealtimeNotifier` publishes a JSON envelope `{ OriginInstanceId, GroupName, EventName, Payload }` to that channel; the bridge on every instance receives it, **filters out messages originating from this instance** (via the singleton `InstanceIdentity` Guid), and fans the rest into the local `BoardEventStream`. Self-echo loops avoided, no double-fires on the origin.
  - When `Redis:ConnectionString` is absent, the app runs single-instance with in-process state only — same code path, no Redis dependency. On-prem unaffected.
- **Tenant directory caching:** `CachedTenantDirectory` decorates the raw `TenantDirectory` with `IMemoryCache`. Lookups by slug, id, and "default" each hit the cache on subsequent reads; positive results only (so a newly provisioned tenant is visible on the next request without negative-cache stalling). TTL configurable via `Tenancy:DirectoryCacheTtlMinutes` (default 5). On the hot path — `TenantResolutionMiddleware` on every request — this drops directory-DB load to roughly `requests / (TTL × tenants)`.
- **Postgres full-text search** over issue titles + descriptions. A `tsvector` generated-stored column (`title` weighted A, `description` weighted B) plus a GIN index, mapped via EF shadow property `SearchVector` on `Issue` with `HasComputedColumnSql`. `Modules.Issues.Contracts.IIssueSearch` exposes `SearchAsync(query, limit)`; `Modules.Issues.Application.IssueSearch` implements it using `EF.Functions.PlainToTsQuery` + `NpgsqlTsVector.Rank`. The same service backs both `GET /api/issues/search?q=...&limit=20` and the `/search` Blazor page. A search input is wired into the top nav for quick access.
- **Invite-only registration.** `Invite` aggregate in `Modules.Identity.Domain` with EF migration. `Modules.Identity.Contracts.IInviteService` exposes `CreateAsync`, `ListPendingAsync`, `RevokeAsync`, `AcceptAsync`. Tokens are 32-byte cryptorandom values; only `SHA-256(token)` is persisted, the raw token only lives in the invite URL. `/invites` admin page (authenticated) sends invites and lists pending ones; the page shows the invite link inline so the admin can copy/paste it. `/accept-invite?token=...` is a static SSR Blazor page that lets the invitee set name + password, creates the `ApplicationUser` in the correct tenant DB (resolved by subdomain in SaaS), and signs them in.
- **OIDC sign-in.** External identity providers (Google, Microsoft, Okta, Auth0, generic OIDC) supported via `Microsoft.AspNetCore.Authentication.OpenIdConnect`. Configured via `Authentication:Oidc:*` (Enabled, DisplayName, Authority, ClientId, ClientSecret, optional extra Scopes). When enabled and configured, the Login page shows a "Sign in with [DisplayName]" button; `POST /signin-external` issues the challenge, the IdP redirects back to `/signin-oidc`, then `GET /signin-external/callback` resolves the user by email via `UserManager.FindByEmailAsync` and signs them into the Identity application scheme — the existing `TenantStampedClaimsFactory` stamps the `tenant_id` claim. Unknown emails redirect to `/access-denied?reason=unknown_user` unless **auto-provisioning** is enabled: when `Authentication:Oidc:AutoProvision=true` and the email's domain matches an entry in `Authentication:Oidc:AutoProvisionDomains` (e.g. `["acme.com"]`), a passwordless `ApplicationUser` is created in the current tenant's organization (FirstName/LastName pulled from `given_name`/`family_name` claims if present) and the user is signed in. Recorded as a `user.auto-provisioned` audit event with provider + domain in `Detail`. The default is **off** — without a domain allow-list, any random Google account could sign into any tenant subdomain. `oidc.signin` audit events recorded with provider name in `Detail`. The global `Authentication:Oidc:*` config remains as a fallback when no tenant-specific config is set; the doc-locked-in OpenIddict-as-server is reserved for a future "ProjectJA-as-IdP" scenario.
- **Per-tenant OIDC config.** Each tenant DB carries a singleton `tenant_oidc_config` row (Enabled, DisplayName, Authority, ClientId, ClientSecret, AutoProvision, AutoProvisionDomains). Managed via `/admin/oidc-settings` — `MudPaper` form with Save/Clear actions. The service exposes `OidcConfigView` to UI (with `HasClientSecret` bool instead of the secret) and an internal `OidcConfigSecret` used only by the Host's startup scheme registrar. Audit: `oidc-config.saved` / `oidc-config.cleared`.
- **Per-tenant scheme registration at startup.** After `EnsureSeededAsync`, `TenantOidcSchemeRegistrar.RegisterAllAsync` iterates the tenant directory, opens a scoped DI scope per tenant via `UseTenant`, pulls each tenant's `OidcConfigSecret`, and (when enabled + Authority + ClientId are all present) registers a tenant-specific scheme `oidc:{slug}` with callback path `/signin-oidc/{slug}` via `IAuthenticationSchemeProvider.AddScheme` + `IOptionsMonitorCache<OpenIdConnectOptions>.TryAdd`. `/signin-external` resolves the current subdomain's tenant, prefers `oidc:{slug}` if registered, and falls back to the global `oidc` scheme. The Login page button text comes from whichever wins (tenant config → global → none → no button).
  - **Restart required:** changing or adding a tenant's OIDC config at runtime requires an app restart to register the new scheme. ASP.NET Core's authentication pipeline isn't designed for dynamic per-request scheme reconfiguration; restart-on-change is the honest pragmatic choice. The admin UI surfaces this in an info banner.
- **Password reset.** `/forgot-password` (static SSR) takes an email and calls `UserManager.GeneratePasswordResetTokenAsync`; the token is Base64URL-encoded and embedded in a link delivered via the queued `IEmailSender` (so retry + dead-letter apply to reset emails too). The page always shows the same generic "if that email is registered, we sent a reset link" alert — anti-enumeration. `/reset-password?token=...&email=...` (static SSR) decodes the token, calls `UserManager.ResetPasswordAsync`, and on success links the user back to `/login`. Tokens are single-use and expire in one hour (ASP.NET Identity default). Audit events: `password.reset.requested` (only when the email matches a real user; suppressed otherwise to avoid existence leak via audit) and `password.reset.completed`. "Forgot your password?" link added to the Login page.
- **Audit log persistence + retention.** `Modules.Audit.Domain.AuditEvent` table in every tenant DB with the doc-spec fields (id, tenant_id, actor_id, occurred_at, action, resource_type, resource_id, summary, jsonb detail, ip, user_agent). `Modules.Audit.Application.AuditLog` implements the `IAuditLog` contract from SharedKernel — pulls actor from `ClaimTypes.NameIdentifier`, tenant from `ITenantContext`, IP + user-agent from `IHttpContextAccessor`. `IAuditQueries` exposes recent-events reads. The invite, project, and issue flows call `IAuditLog.RecordAsync` at every audit-worthy point: `invite.created/accepted/revoked`, `project.created`, `issue.created/transitioned/deleted`, `attachment.created/deleted`. `/audit` admin page lists recent events with actor names resolved via the new `IUserQueries` contract on `Modules.Identity`. `AuditRetentionJob` (Coravel `IInvocable`, daily at 03:00 UTC with `PreventOverlapping`) iterates every tenant and bulk-deletes events older than `Audit:RetentionDays` (default 90) via EF Core `ExecuteDeleteAsync`.
- **Object storage + issue attachments.** `Infrastructure/Storage/S3ObjectStore` implements `SharedKernel.Storage.IObjectStore` using `AWSSDK.S3` against any S3-compatible endpoint (R2 in SaaS, MinIO on-prem, S3 itself). Configured via `Storage:Endpoint` / `Storage:Bucket` / `Storage:AccessKey` / `Storage:SecretKey` (+ optional `Region`, `ForcePathStyle`). When unconfigured, `NullObjectStore` is registered and surfaces "Storage not configured" errors loudly at the first attempted operation. Per-tenant key prefix `tenants/{tenantId:N}/attachments/{attachmentId:N}/{filename}` guarantees cross-tenant isolation in a shared bucket. `Modules.Issues.Domain.Attachment` + EF migration owns the metadata table. `Modules.Issues.Contracts.IAttachmentService` exposes `ListAsync` / `UploadAsync` / `GetDownloadUrlAsync` / `DeleteAsync`. API endpoints: `GET /api/issues/{id}/attachments`, `GET /api/attachments/{id}/download-url` (15-min pre-signed URL), `DELETE /api/attachments/{id}`. `/issues/{id}` Blazor page lets users upload (25 MB cap) and download attachments; download opens the pre-signed URL in a new tab via `IJSRuntime.InvokeVoidAsync("open", ...)`.
  - **Direct-from-browser uploads.** `POST /api/issues/{id}/attachments/begin` returns `{ attachmentId, uploadUrl, expiresAt, storageKey, fileName, contentType, sizeBytes }` — a 15-minute pre-signed PUT URL. The browser PUTs the file straight to S3/R2/MinIO (no server bytes proxy), then `POST /api/issues/{id}/attachments/{attachmentId}/complete` persists the metadata. `CompleteUploadAsync` validates that the storage key matches the expected `tenants/{currentTenantId}/attachments/{attachmentId}/{safeFileName}` shape so a malicious client can't redirect a row at someone else's bytes. `wwwroot/js/attachment-upload.js` runs the three-step dance from a plain `<input type="file">` and notifies the Blazor page via `JSInvokable` callbacks. `IAttachmentService.UploadAsync` (server-side streaming) stays for API clients that don't run JS.
  - **Bucket CORS required:** R2/MinIO/S3 must allow `PUT` from the app's origin. AWS S3 example policy: `AllowedMethods: [PUT]`, `AllowedOrigins: [https://app.example.com]`, `AllowedHeaders: [*]`.
- **MudBlazor 9.4 styling.** All pages restyled with MudBlazor components. Providers (`MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`) live in `MainLayout.razor`; `App.razor` carries `<Routes @rendermode="InteractiveServer" />` so the entire routed tree (layout + page) is interactive together — MudBlazor's popover-driven components (`MudSelect`, dialogs, snackbars) need a single render-mode subtree to find their providers. App shell uses `MudAppBar` with auth-aware nav (Projects / Invites / Audit), top-bar search, and a user menu. Pages use `MudPaper` / `MudGrid` / `MudTable` / `MudTextField` / `MudButton`. The Board page uses plain `<div>` columns + cards (with inline styles) so the JS drag-drop module can hold stable DOM hooks — `data-status` on columns and `data-issue-id` on cards. Issue status renders as a colored `MudChip` (Doing = warning, Done = success). MudBlazor's CSS/JS is self-hosted from `_content/MudBlazor/`; Roboto loads from Google Fonts.
- **Queued email + dispatcher split.** Two-layer abstraction: `IEmailSender` (callers, e.g. `InviteService`) and `IEmailDispatcher` (the last-mile transmitter).
  - `IEmailSender` is always `QueuedEmailSender` — writes the message to a per-tenant `email_outbox` table (Postgres `jsonb` payload) and returns. The user-facing path stays fast and resilient to provider hiccups.
  - `IEmailDispatcher` resolves at startup based on `Email:Driver`:
    - `smtp` → `SmtpEmailDispatcher` (MailKit; `Email:Smtp:*`).
    - `postmark` → `PostmarkEmailDispatcher` (typed `HttpClient`; `Email:Postmark:ServerToken`).
    - anything else → `LoggingEmailDispatcher` writes the message to logs.
  - `EmailDispatchJob` (Coravel `IInvocable`) runs every 30 seconds with `PreventOverlapping`. Iterates every tenant via `ITenantDirectory.ListAllAsync`, opens a tenant-scoped DI scope, pulls up to 25 pending entries (`SentAt == null && DeadLetterAt == null && ScheduledFor <= now`), and dispatches each. Failed entries get exponential backoff (30s → 1m → 2m → 4m → 8m → 16m, capped at 30 minutes) and dead-letter after 6 attempts (`DeadLetterAt` set, row kept for forensics).
  - From-address falls back to `Email:From` + `Email:FromName` when the caller leaves `EmailMessage.From` blank.
  - **`/admin/email-outbox` admin page** with filter tabs (Dead-lettered / Pending / Sent / All), backed by `IEmailOutboxAdmin`. **Retry** resets `DeadLetterAt` + `Attempts`, schedules immediately so the dispatcher picks it up on the next tick; **Discard** hard-deletes the row. Both actions emit `email.retry` / `email.discard` audit events. Subject + recipient list are parsed from the JSON payload at view time — no denormalized columns.
- **.NET 10 (LTS) upgrade.** Solution-wide bump from .NET 9 → .NET 10: TFMs to `net10.0`, Microsoft.* + Microsoft.Extensions.* to 10.0.x, Npgsql.EntityFrameworkCore.PostgreSQL to 10.0.1, Serilog.AspNetCore to 10.0.0 (forces transitive sink bumps), `dotnet-ef` tool to 10.0.8, Dockerfile + CI to `10.0`. MudBlazor stayed on 9.4 (net9 lib runs fine on net10). One genuine net10 regression caught by the integration suite: `IssueSearch` had been pre-computing `EF.Functions.PlainToTsQuery` into a local; EF Core 10 / Npgsql 10 client-evaluates that and throws — fixed by inlining the call into the Where/OrderBy/Select expression tree (the doc-spec way). One framework gotcha: `Microsoft.OpenApi` v2 flattened `Microsoft.OpenApi.Models` into `Microsoft.OpenApi`.
- **Migration squash.** As of 2026-05-18 each EF context carries a single `InitialCreate` migration (`AppDbContextModelSnapshot.cs` regenerated; ~4,600 lines of generated scaffolding dropped, ~32% repo shrink). 100% model-driven schema means the squash lost nothing; fresh-DB replay produces an identical model. Existing dev DBs had their `__EFMigrationsHistory` hand-reconciled. Future migrations branch from this single baseline.
- **Issue expansion.** `Issue` carries `IssueType` (Story/Task/Spike/Enhancement/Defect/Chore/Epic, default Task), `Points` (int?, 0–100), `AcceptanceCriteria` (string?), an immutable `CreatedById` (set at create-time), and an editable `ReporterId` (defaults to creator). Domain methods: `Reclassify`, `SetReporter`. Wired through the create form, the project's issues list, the issue dialog, and the REST API DTOs.
- **Priority + labels.** `IssuePriority` enum (None/Low/Medium/High/Critical, default Medium) with `Issue.SetPriority`. Labels are **free-text multi-tag stored as Postgres `text[]`** via `b.PrimitiveCollection(x => x.Labels)` (Npgsql native — no Label aggregate / join / colors by deliberate anti-bloat decision). `Issue.SetLabels` normalises (trim → drop blanks → cap 40 chars per tag → case-insensitive de-dupe → cap 20). Surfaced in API DTOs, the create form, the issue dialog Details tab, and chips on board cards + project list rows. Migration `AddIssuePriorityAndLabels`.
- **IssueEditDialog (`Components/Dialogs/IssueEditDialog.razor`).** Modal with three tabs — Details, Attachments, Comments. Opens from a board card's `@ondblclick` *and* the ProjectDetail issues-list edit pencil / double-click. Save-details persists field changes; attachments + comments persist immediately. Uses `IDbContextFactory<AppDbContext>` for a fresh short-lived context per write, NOT the circuit-scoped shared context — that's what avoids EF Core's "owned child with a domain-assigned Guid key is misclassified Modified → UPDATE → 0 rows" trap (see [`CLAUDE.md` style notes if present, or the `project_projectja_ef_owned_keys.md` memory]). Board republishes `IssueMoved` realtime after a dialog Ok so other clients converge.
- **Comments (add / edit / delete).** First comments surface in the app — `Issue` owns a `Comment` aggregate (`AuthorId`, `Body`, `CreatedAt`) and the domain enforces **author-only edits + deletes** (`EditComment` / `RemoveComment` throw when `actingUserId != AuthorId`). The dialog's Comments tab shows comments newest-first with a 30vh scroll cap; the author sees double-click-to-edit. **Realtime `CommentsChanged`** events broadcast over `IRealtimeNotifier` (in-proc + SignalR + optional Redis) so other open dialogs on the same issue reload immediately; the publishing dialog suppresses its own echo via an `_ownPublishInFlight` latch.
- **Project members & permissions (RBAC).** `Project` owns a `ProjectMember` collection (composite key `(ProjectId, UserId)`, `Role ∈ {Viewer, Member, Admin}`, `AddedAt`) backed by table `project_members`; the project creator is auto-seeded as the first Admin and the domain refuses to strand a project with zero Admins. **Clean 3-tier capability matrix**: Viewer = read-only · Member = full issue write (create/edit/move/assign/comment/attachments) · Admin = + manage members / settings / sprints. **Single policy chokepoint** `ProjectAccess.RequireAsync(http, projects, projectId, minimum, ct)` in `Modules.Projects.Contracts`: reads `NameIdentifier` claim → `IProjectQueries.GetRoleAsync` → returns short-circuit 401 (no/!parseable claim) or **403 JSON** (`role is null || role < minimum`, so "not a member" and "below minimum" share one path). Used by every REST write endpoint (`min=Member`) AND every REST read endpoint (`min=Viewer`); UI pages additionally resolve role in `OnInitializedAsync`, redirect non-members to `/projects?denied=1`, hide write controls for Viewers, and guard every write method server-side as defense-in-depth (the Blazor circuit can be tampered). `IProjectQueries.ListMemberProjectIdsAsync` scopes the `GET /api/projects` list and `GET /api/issues/search` post-filter to the caller's memberships. `ProjectMembersDialog` (from a "Membership" button next to Board on ProjectDetail) — Admins add/remove/change-role with last-Admin guards; non-admins see a read-only list. **Cookie auth quirk worth knowing:** with `IdentityConstants.ApplicationScheme` + `LoginPath=/login` and no `OnRedirectToLogin` override, anonymous GETs return **302 → /login** (browser UX), anonymous mutating verbs return **401**; authenticated-but-not-a-member always gets a clean 403 JSON on both reads and writes.
- **Audit log extension + interactive-circuit actor.** Every membership change (`project.member.added` / `.removed` / `.role_changed`) and every sprint event (`sprint.created` / `.started` / `.completed` / `.edited` / `.deleted`, `issue.sprint_changed`) writes an `audit_events` row. **Non-obvious fix:** `AuditLog.RecordAsync` reads the actor from `IHttpContextAccessor.HttpContext`, but a Blazor *interactive* circuit has no `HttpContext` (same root cause as the auth-cookie-must-be-an-HTTP-endpoint rule), so audit raised from a dialog would lose the actor. `AuditEntry` now carries an optional `Guid? ActorId`; `AuditLog` does `actorId = entry.ActorId ?? <claim-derived>`. HTTP endpoints unchanged (auto-capture from the cookie); interactive callers (`ProjectMembersDialog`, `Sprints` page, `SprintPlan` page) pass the acting user explicitly via a best-effort `try/catch`-wrapped helper.
- **Sprints.** Time-boxed iterations per project. `Sprint` aggregate (`Name`, optional `Goal`, optional planned `Start`/`End`, `StartedAt`/`CompletedAt`, `Status` ∈ {Planned=0, Active=1, Completed=2}) in `Modules.Projects.Domain`; `Issue.SprintId` (nullable, null = backlog) + `Issue.AssignToSprint`. Lifecycle is strictly Planned→Active→Completed (one-way); at most **one Active sprint per project** enforced at the application layer (`ISprintQueries.HasActiveSprintAsync`); on Complete, any issue still `Status != Done` has its `SprintId` cleared (back to backlog) via the cross-module `ISprintIssueOps` bridge in `Modules.Issues.Contracts` (keeps the Issues→Projects one-way module dependency; sprint endpoints live in the Issues module for the same reason). REST: `GET /api/projects/{pid}/sprints` (Viewer), `POST` (Admin), `PUT /api/sprints/{id}` (Admin; rejects on Completed), `PATCH /api/sprints/{id}/start` (Admin; 409 if another Active), `PATCH /api/sprints/{id}/complete` (Admin; logs `issuesReturnedToBacklog` count in the audit Detail), `DELETE` (Admin; only when Planned + empty), plus `PATCH /api/issues/{id}/sprint` (Admin; validates target sprint is in the same project + not Completed). UI: dedicated `/projects/{id}/sprints` page (list + per-sprint Start/Complete/Delete + "Plan a sprint" form, all Admin-gated), sprint dropdown on the issue create form + IssueEditDialog Details tab (Admin only), sprint chip on issue rows + board cards, "Sprints" button in the project header. Migration `AddSprintsAndIssueSprint`.
- **Sprint follow-ons.** (1) **Board sprint filter** — single `/board` page, `MudSelect` "Show:" at the top (`"all" | "backlog" | "<sprint-guid>"`), defaults to the project's Active sprint when one exists. (2) **Drag-and-drop planning page** at `/projects/{id}/sprints/plan` (Admin only; non-admin → `/sprints`) — two columns (Backlog | Selected Sprint), picker selects the target sprint, drag a card across to (re)assign. Cards render with **the same theme-aware markup, chips, and double-click→IssueEditDialog as the main board**. Tiny `wwwroot/js/sprint-plan-dnd.js` mirrors `board-dnd.js` (same Safari/Firefox dataTransfer dance). (3) **Inline-expandable completion report on `/sprints`** — replaced the MudTable with `MudExpansionPanels`; the expanded body shows 4 stat cards (Todo / Doing / Done / Total — issue count + summed Points) and the issue list. For Completed sprints the body only shows what's currently in the sprint (the Done remnants); the carry-over count lives in the `sprint.completed` audit row's `Detail.issuesReturnedToBacklog`.
- **System Versioning page** at `/system/versioning` (any authenticated user; "System" button with Info icon in the app bar). Dynamic, no caching — Runtime/host (`.NET 10.0.x`, `RuntimeInformation` framework + OS + arch, `Process.GetCurrentProcess()` uptime, GC + working-set), Application (entry assembly version/file/informational + product, `IWebHostEnvironment.EnvironmentName`, content/web roots), Database (`NpgsqlConnectionStringBuilder`-parsed host/port/db, `NpgsqlConnection.PostgreSqlVersion` for the real server version like `16.14`, applied migrations list from `Db.Database.GetAppliedMigrationsAsync()`), and Loaded assemblies (`AppDomain.CurrentDomain.GetAssemblies()` filtered to non-dynamic, sortable + filterable MudTable, ~210 rows). Refresh button re-gathers. Failures (e.g. DB unreachable) surface as a MudAlert rather than 500'ing.
- `StartupBootstrap.EnsureSeededAsync` provisions both DB schemas, seeds a default tenant in the directory pointing at the app's connection string, sets the bootstrap scope's tenant via `UseTenant`, then seeds the default organization + admin user inside that tenant. Idempotent backfill steps also run here: legacy projects with zero members get the bootstrap admin as Admin + every other user as Member (the [[ProjectMember]] same EF-Added-state insert trap as Comments).

### Deferred to follow-up slices
All commitments from `ProjectJAbeginning.md` are landed; integration tests, OpenAPI/Scalar, observability, and CI/CD have their own dedicated sections farther down. Actual outstanding items:
- **Custom workflows.** Per-project configurable status pipelines instead of the fixed Todo/Doing/Done. The scaffold module `Modules.Workflows` already exists in the solution layout — building it out is its own slice (domain `Workflow` aggregate + transition rules, migration, REST surface, board column rendering from the project's workflow, audit).
- **Sprint reports — small polish.** Surface `sprint.completed` audit `Detail.issuesReturnedToBacklog` on the Completed-sprint inline report so the carry-over count is visible without going to the audit log.
- **Org-level admin role.** No concept of "tenant admin" yet — pages like `/admin/email-outbox`, `/admin/oidc-settings`, `/audit`, `/system/versioning` are gated only by `[Authorize]`. A future slice can introduce a tenant-admin role + policy and tighten those routes.
- **API read-gating asymmetry.** Anonymous GETs return 302→/login (cookie-auth default), anonymous mutating verbs return 401 — same app-wide. Authenticated-non-member is always a clean 403 JSON. Acceptable but worth knowing; a future `OnRedirectToLogin` override could uniformly return 401 for `/api/*`.

### CI / CD

- **`.github/workflows/ci.yml`** — on every PR and push to `main`: restore, build (Release), run unit + architecture tests. Integration tests run only on pushes to `main` (they need Docker, which slows the loop on PRs).
- **`.github/workflows/deploy.yml`** — on tag `v*.*.*` (or manual `workflow_dispatch`): build a multi-arch image (`linux/amd64` + `linux/arm64`) using `deploy/Dockerfile`, push to `ghcr.io/{owner}/projectja:{tag}` + `:latest` + `:{short-sha}`. GHCR auth via the workflow's `GITHUB_TOKEN`.
- The image bundles three publish outputs side-by-side: `host/` (the web app, default entrypoint), `migrator/` (per-tenant migrator), and `provisioning/` (tenant provisioning CLI). Override the command at `docker run` / `docker compose run` time to invoke a different one.

### Server-side rollout

`deploy/scripts/deploy.sh` runs on the Hetzner box (or any Linux host with Docker). Usage: `./deploy.sh [tag]` (defaults to `:latest`). Steps:
1. `docker compose pull app` — pulls the new image from GHCR.
2. `docker compose run --rm app dotnet migrator/ProjectJA.Tools.Migrator.dll` — runs migrations against every tenant DB before swapping traffic.
3. `docker compose up -d app` — rolls the app container with the new image.
4. Polls `http://localhost:8080/healthz/ready` for up to `HEALTH_TIMEOUT_SECS` (default 90) — bails and dumps recent logs if the new container doesn't go ready.
5. Prunes dangling images on success.

Environment knobs: `PROJECT_ROOT`, `COMPOSE_FILE`, `HEALTH_URL`, `HEALTH_TIMEOUT_SECS`. The compose file resolves `${GITHUB_OWNER}` and `${PROJECTJA_TAG}` from the shell env.

### Observability

- **Structured logging via Serilog.** Console (compact format) + daily-rolling file sink (`logs/projectja-{date}.log`, 14-file retention). Enrichers: environment name, machine name, `Application=ProjectJA`. Per-request `UseSerilogRequestLogging` emits one line per HTTP request with method, path, status, elapsed-ms, and `TenantSlug` from `HttpContext.Items`. Log file path configurable via `Serilog:FilePath`; per-category minimum levels via `Serilog:MinimumLevel:Override:*` in appsettings. Noisy categories (`Microsoft.AspNetCore.Hosting.Diagnostics`, `Microsoft.EntityFrameworkCore.Database.Command`) pre-tuned to Warning.
- **OpenTelemetry traces + metrics.** Built on `OpenTelemetry.Extensions.Hosting`. Auto-instruments ASP.NET Core, HttpClient, runtime + process metrics. Picks up Npgsql's own `ActivitySource`. App-defined sources matched via wildcard `ProjectJA.*`. **OTLP exporter** is conditional: set `OpenTelemetry:Otlp:Endpoint` (e.g. `http://otel-collector:4317`) to ship spans + metrics to Tempo, Jaeger, Honeycomb, Datadog, or any OTLP-compatible collector. With no endpoint configured, traces and metrics still collect in-process but aren't exported — useful for dev.
- **Health checks.**
  - `GET /healthz` — liveness. Returns 200 if the process is up; no dependencies checked. Safe for k8s liveness probes that should kill the pod if the process is wedged but pass through transient DB blips.
  - `GET /healthz/ready` — readiness. Runs the Postgres connectivity check (tagged `ready`). Returns 503 when the DB is unreachable. Safe for k8s readiness probes and load balancers (pull the instance out of rotation during DB outages).

### OpenAPI / Scalar docs

`Microsoft.AspNetCore.OpenApi` generates the spec; `Scalar.AspNetCore` provides the UI.
- `GET /openapi/v1.json` — the OpenAPI document.
- `GET /scalar/v1` — interactive Scalar reference UI.

Projects + Issues + Search + Attachments endpoints are annotated with `WithName`, `WithSummary`, `WithTags`, and `Produces<T>` for response shapes. Other modules can adopt the same pattern when their API surface stabilizes.

**Production access:** both routes are open by default. For SaaS, restrict the routes behind your reverse proxy (Caddy basic auth, IP allow-list, or wire `RequireAuthorization()` into both `MapOpenApi` and `MapScalarApiReference` calls). On-prem deployments behind a corporate network typically leave them open for in-house API consumers.

### Integration tests

Integration tests live in `tests/ProjectJA.IntegrationTests/` and exercise the full app stack against a real Postgres via [Testcontainers](https://dotnet.testcontainers.org/). **Docker must be running locally** (or in CI) for these to execute.

```sh
# Make sure Docker is running, then:
dotnet test tests/ProjectJA.IntegrationTests
```

A single `postgres:16-alpine` container is shared across the assembly (xUnit collection fixture) so tests pay the container-start cost once. Each test uses random project keys / unique markers to avoid stepping on its siblings. `WebApplicationFactory<Program>` hosts the app in-process pointed at the test container's connection string via `Bootstrap:*` config overrides; the app's `StartupBootstrap.EnsureSeededAsync` runs as normal, providing a known default tenant + organization + admin user.

Tests cover: anonymous routing (smoke), bootstrap state (default tenant + org + admin user), end-to-end project + issue creation through EF Core, Postgres FTS search ranking, and the audit-log write path.

### Running locally (on-prem mode)

```sh
# Start a Postgres (named volume keeps data across container restarts):
docker run --rm -d --name projectja-pg \
  -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=projectja \
  -p 5432:5432 -v projectja-pgdata:/var/lib/postgresql/data \
  postgres:16-alpine

# Run the app:
dotnet run --project src/ProjectJA.Host
```

Default credentials seeded on first run: `admin@projectja.local` / `ChangeMe123!` (override via the `Bootstrap:*` configuration keys). `--rm` only drops the container — data lives in the `projectja-pgdata` volume and survives restarts; to wipe and re-bootstrap a clean DB: `docker rm -f projectja-pg && docker volume rm projectja-pgdata`.

### Inspecting the database

The Docker container publishes Postgres on `localhost:5432`, so any SQL client connects with no special Docker networking:

| Setting | Value |
|---|---|
| Host | `localhost` |
| Port | `5432` |
| Database | `projectja` (on-prem); `projectja_directory` + per-tenant `tenant_{slug}` in SaaS mode |
| User / Password | `postgres` / `postgres` |

GUI options (SSMS-style):

- **Rider's built-in Database tool** — Rider bundles the DataGrip engine. View → Tool Windows → Database → **+** → Data Source → PostgreSQL. No extra install; this is the path of least resistance if you're already in Rider.
- **[DBeaver](https://dbeaver.io/) Community** (free, cross-platform) — the strongest standalone free option.
- **[pgAdmin 4](https://www.pgadmin.org/)** (free) — the official Postgres tool, closest in scope to SSMS.
- **[TablePlus](https://tableplus.com/)** / **[Postico 2](https://eggerapps.at/postico2/)** (freemium) — polished native macOS clients.

No-GUI quick peek (no install — runs `psql` inside the container):

```sh
docker exec -it projectja-pg psql -U postgres -d projectja
```

In SaaS mode the schema is split across databases: the tenant directory lives in `projectja_directory`, and each provisioned tenant gets its own `tenant_{slug}` database. Connect to the same server and switch databases in the client to inspect each one.

### Provisioning a new tenant (SaaS mode)

```sh
export ConnectionStrings__TenantDirectory="Host=localhost;Database=projectja_directory;Username=postgres;Password=postgres"
export Provisioning__MaintenanceConnection="Host=localhost;Database=postgres;Username=postgres;Password=postgres"
export Provisioning__TenantConnectionTemplate="Host=localhost;Database=tenant_{slug};Username=postgres;Password=postgres"

dotnet run --project tools/Provisioning -- create \
  --slug acme \
  --name "ACME Corp" \
  --admin-email admin@acme.example.com
```

### Running migrations across all tenants

```sh
export ConnectionStrings__TenantDirectory="..."
dotnet run --project tools/Migrator
```

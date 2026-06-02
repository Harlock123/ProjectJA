# Running ProjectJA in Rider

## One-time setup

**1. Start the dev dependencies.** The app needs Postgres at startup (bootstrap creates the schema) and an S3-compatible object store for issue attachments. Both are wired up in `deploy/docker-compose.dev.yml` — Postgres + MinIO + a one-shot bucket-create step:

```sh
docker compose -f deploy/docker-compose.dev.yml up -d
```

That brings up:
- Postgres on `localhost:5432` with user `postgres` / pass `postgres` (matches the default connection string — no config changes needed).
- MinIO on `localhost:9000` (S3 API) + `localhost:9001` (console, login `minioadmin` / `minioadmin`) with a `projectja-attachments` bucket and browser CORS open for dev.

Data lives in named volumes `projectja-pgdata` and `projectja-miniodata`, so it survives `docker compose down`. To wipe and start over: `docker compose -f deploy/docker-compose.dev.yml down -v`.

**Storage config for the app.** Add this to `src/ProjectJA.Host/appsettings.Development.json` (or export the equivalent `Storage__*` env vars before `dotnet run`):

```json
{
  "Storage": {
    "Endpoint": "http://localhost:9000",
    "Bucket": "projectja-attachments",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "ForcePathStyle": true,
    "Region": "us-east-1"
  }
}
```

Without these keys the app falls back to `NullObjectStore` and attachment uploads return a 500 with *"Object storage is not configured"*.

**Upgrading from a prior standalone `docker run -d --name projectja-pg ...` setup:** stop and remove that container first, then the compose Postgres reuses the same `projectja-pgdata` volume so every existing project / issue / user carries over:

```sh
docker stop projectja-pg && docker rm projectja-pg
docker compose -f deploy/docker-compose.dev.yml up -d
```

**2. Open the solution.** `File → Open` → pick `ProjectJA.sln` at the repo root. Rider will detect the 16 projects.

**3. Restore.** Rider auto-restores on open; if not, right-click the solution → `Restore NuGet Packages`.

## Running

In the run-configuration dropdown (top right toolbar), pick **`ProjectJA.Host: https`**, then hit the green run button (or `Ctrl+F5` / `Shift+F10`).

The app starts on `https://localhost:7215`. On first run it'll:

1. Create the schema (via `EnsureSeededAsync` running real EF migrations).
2. Seed the default tenant in the directory.
3. Seed the default organization.
4. Seed the admin user.

You'll see Serilog output in Rider's run window — look for `Now listening on: https://localhost:7215`.

## Logging in

Browser opens automatically. At the sign-in page:

- **Email:** `admin@projectja.local`
- **Password:** `ChangeMe123!`

(These come from `Bootstrap:AdminEmail` / `Bootstrap:AdminPassword`, which fall back to those defaults when unset.)

After login you land on `/projects`. From there:

- Create a project (right-hand form).
- Click into it, create issues.
- Open `/projects/{id}/board` for the drag-drop kanban.
- Try `/search` from the top nav, `/admin/email-outbox`, `/audit`, `/admin/oidc-settings`.

## Useful URLs while running

| Path | What |
|---|---|
| `/projects` | Project list (main UI) |
| `/scalar/v1` | Interactive API docs |
| `/openapi/v1.json` | Raw OpenAPI spec |
| `/healthz` | Liveness probe |
| `/healthz/ready` | Readiness probe (checks DB) |

## Common gotchas

- **HTTPS cert warning** on first launch — Rider prompts to trust the .NET dev cert; accept it. Or use the `http` launch profile instead.
- **Bootstrap fails with connection error** — Postgres container isn't running. Check `docker ps`.
- **Want to start fresh** — stop the app, then `docker rm -f projectja-pg && docker volume rm projectja-pgdata`, then re-run. (Data now persists in the named volume across normal restarts, so wiping it is an explicit step.)
- **Bootstrap email/password customization** — edit `src/ProjectJA.Host/appsettings.Development.json` (or set env vars on the run config): `Bootstrap:AdminEmail`, `Bootstrap:AdminPassword`, `Bootstrap:OrgName`.

That's it — no other ceremony.

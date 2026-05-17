# Running ProjectJA in Rider

## One-time setup

**1. Start Postgres.** The app needs it at startup (bootstrap creates the schema). Easiest way — Docker:

```sh
docker run --rm -d --name projectja-pg \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=projectja \
  -p 5432:5432 \
  -v projectja-pgdata:/var/lib/postgresql/data \
  postgres:16-alpine
```

The default connection string already points at `localhost:5432` with user `postgres` / pass `postgres`, so no config changes needed.

`--rm` removes the *container* on stop, but the named volume `projectja-pgdata` is independent — your data (projects, issues, users) survives a `docker stop` / restart. The first run still bootstraps the schema + seed admin; later runs find the existing data and skip re-seeding.

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

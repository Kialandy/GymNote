# GymNote — Gym Exercise Library

A small full-stack app where each user keeps a private list of suggested gym exercises
(name, sets, reps per set, rest time).

| Path | What |
|---|---|
| `backend/` | ASP.NET Core Web API (.NET 10 LTS), EF Core + Npgsql, JWT auth. Solution: `GymNote.slnx` |
| `backend/src/GymNote.Api` | The API (migrations in `Data/Migrations`) |
| `backend/tests/GymNote.Api.Tests` | xUnit + `WebApplicationFactory` integration tests |
| `frontend/` | React + TypeScript + Vite + Tailwind CSS, React Router, Vitest + Testing Library |
| `gateway/nginx.conf` | Local reverse proxy that mimics the future ALB/Ingress: `/api` → backend, `/` → frontend |
| `docker-compose.yml` | Local stack: postgres + backend + frontend + gateway |

## Run everything locally (Docker)

Prerequisite: Docker (Desktop) with Compose v2.

```bash
cd app
docker compose up --build
```

Open **http://localhost**. With the default compose settings a demo account is seeded:

- email `demo@gymnote.local`, password `DemoPass123!` (demo-only; created only when `SEED_DEMO_DATA=true`)

Stop with `Ctrl+C`; `docker compose down -v` also deletes the database volume. The backend
recreates the schema (and demo data, if enabled) on the next start.

Compose uses local-only fallback credentials. Override them with shell variables or an `app/.env` file
(git-ignored):

| Compose variable | Default | Purpose |
|---|---|---|
| `POSTGRES_PASSWORD` | `gymnote-local-only` | Password of the local Postgres container |
| `JWT_SECRET` | local-only placeholder | Passed to the backend as `Jwt__Secret` |
| `SEED_DEMO_DATA` | `true` | Passed to the backend |

## Backend configuration (environment variables)

All configuration comes from environment variables. No secrets are stored in the repo.

| Variable | Required | Default | Description |
|---|---|---|---|
| `ConnectionStrings__Default` | yes | – | Npgsql connection string, e.g. `Host=db;Port=5432;Database=gymnote;Username=gymnote;Password=...` |
| `Jwt__Secret` | yes | – | HMAC-SHA256 signing key, **at least 32 characters**. The app refuses to start without it. |
| `Jwt__Issuer` | yes | – | Token issuer (`iss`), validated on every request |
| `Jwt__Audience` | yes | – | Token audience (`aud`), validated on every request |
| `ASPNETCORE_URLS` | no | `http://+:8080` (set in the image) | Listen address |
| `RUN_MIGRATIONS` | no | `true` | Apply EF Core migrations on startup |
| `SEED_DEMO_DATA` | no | `false` | Create the demo user and a few exercises if they don't exist yet |

Tokens are valid for 1 hour. Logs go to stdout (single-line console format).

Tip: the `aspnet` image does not include Kerberos libraries, so append `;GSS Encryption Mode=Disable` to
the connection string to keep Npgsql from logging a harmless `libgssapi_krb5` message on startup
(docker-compose does this).

### Startup behaviour

- **Migrations** (`RUN_MIGRATIONS=true`): EF Core takes a lock on the migrations history table, so several
  replicas starting at once is safe. If the database is not reachable yet, startup retries 10 times, 3 seconds apart.
- **Demo seed** (`SEED_DEMO_DATA=true`): skipped when the demo user already exists. A concurrent insert
  from another replica is detected through the unique email index. Safe to run on every start.
- **Recreated databases**: if a token belongs to a user that no longer exists (for example after a DB
  reset), the exercise endpoints return 401 and the frontend sends the user back to login.
- **Stateless**: no sessions and no local files. ASP.NET data-protection keys are kept in memory.

## API

| Method & path | Auth | Result |
|---|---|---|
| `GET /health` | – | `200 {"status":"healthy"}`. Never touches the database (ALB / Kubernetes probes). |
| `POST /api/auth/register` `{email,password}` | – | `201`; `409` if the email exists; `400` ProblemDetails (invalid email, password < 8 chars) |
| `POST /api/auth/login` `{email,password}` | – | `200 {"token": "..."}`; generic `401` on any failure |
| `GET /api/exercises` | Bearer | `200` with the caller's exercises, sorted by name |
| `POST /api/exercises` `{name,suggestedSets,suggestedRepsPerSet,suggestedRestSeconds}` | Bearer | `201`; `400` ProblemDetails. Name required (max 100), sets 1–20, reps 1–100, rest 0–600 s |
| `DELETE /api/exercises/{id}` | Bearer | `204`; `404` if it doesn't exist **or belongs to another user** |

Errors are returned as RFC 9110 ProblemDetails (`application/problem+json`). Validation errors include an
`errors` object keyed by camelCase field name.

## Run without Docker (development)

Requires the .NET 10 SDK, Node.js 22.12+ (24 LTS recommended), and a reachable PostgreSQL.

```bash
# Backend (http://localhost:8080)
cd app/backend
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=gymnote;Username=gymnote;Password=<password>"
export Jwt__Secret="<at least 32 random characters>" Jwt__Issuer=gymnote Jwt__Audience=gymnote
export ASPNETCORE_URLS=http://localhost:8080 SEED_DEMO_DATA=true
dotnet run --project src/GymNote.Api

# Frontend (http://localhost:5173). The Vite dev server proxies /api to the backend.
cd app/frontend
npm install
npm run dev            # set API_PROXY_TARGET to proxy somewhere other than http://localhost:8080
```

The frontend always calls the relative path `/api`, with no absolute URLs baked into the build. The same
bundle works behind the compose gateway, the Vite proxy, or an ALB/Ingress.

Add a migration after changing the model:

```bash
cd app/backend
dotnet tool install --global dotnet-ef --version 10.0.13   # once
dotnet ef migrations add <Name> --project src/GymNote.Api --output-dir Data/Migrations
```

## Tests

```bash
# Backend: xUnit + WebApplicationFactory (register, login, create, list, delete, per-user isolation, validation)
cd app/backend
dotnet test

# Frontend: Vitest + React Testing Library (login/register form, exercise list, create, delete, 401 redirect)
cd app/frontend
npm ci
npm test               # = vitest run: single run, non-zero exit code on failure
```

The backend tests host the real API in memory and replace PostgreSQL with the EF Core in-memory provider,
so CI needs neither a database nor Docker. Both commands exit non-zero on failure.

Other frontend scripts: `npm run typecheck`, `npm run build` (type-check + production build into `dist/`).

## Container images

| Image | Build context | Port | Notes |
|---|---|---|---|
| backend | `app/backend` | 8080 | Multi-stage `sdk:10.0` → `aspnet:10.0`. Runs as the non-root `app` user (UID 1654). |
| frontend | `app/frontend` | 80 | Multi-stage `node:24-alpine` → `nginx:1.29-alpine`. SPA fallback via `try_files`, long cache on `/assets`. |

The images must run on **linux/amd64** (EKS nodes). On an Apple Silicon Mac, build for that platform
explicitly:

```bash
cd app
docker buildx build --platform linux/amd64 -t gymnote-backend:latest  --load ./backend
docker buildx build --platform linux/amd64 -t gymnote-frontend:latest --load ./frontend

# or build and push straight to a registry:
docker buildx build --platform linux/amd64 -t <registry>/gymnote-backend:<tag>  --push ./backend
docker buildx build --platform linux/amd64 -t <registry>/gymnote-frontend:<tag> --push ./frontend
```

Both Dockerfiles run the build stage on the native platform (`--platform=$BUILDPLATFORM`). The output is
portable (.NET IL / static files), so only the slim runtime layer is amd64 and builds stay fast without
emulating the SDK.

Quick check of an amd64 backend image (works without a database because `/health` doesn't use it):

```bash
docker run --rm --platform linux/amd64 -p 8080:8080 \
  -e Jwt__Secret=$(openssl rand -hex 32) -e Jwt__Issuer=gymnote -e Jwt__Audience=gymnote \
  -e ConnectionStrings__Default="Host=unused" -e RUN_MIGRATIONS=false \
  gymnote-backend:latest
curl http://localhost:8080/health
```

### Routing expected from the future ALB / Ingress

Same as `gateway/nginx.conf`: `/api/*` → backend service port 8080, everything else → frontend
service port 80. Point health checks / probes at backend `GET /health` and frontend `GET /`.

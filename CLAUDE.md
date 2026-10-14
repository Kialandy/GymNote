# GymNote

Gym Exercise Library monorepo: everything lives under `app/`. Read `app/README.md` for structure,
env vars, API, and commands.

## Scope rules (from the original brief)
- Keep scope strictly to the spec; do not add extra features.
- Do NOT create Terraform, Jenkins, or Kubernetes files here. EKS/ALB deployment is done separately.
- Must stay container-friendly and cloud-agnostic: config only via env vars, stateless, logs to stdout,
  no AWS SDK, no secrets in the repo, frontend calls relative `/api` only.
- `GET /health` must never touch the database (ALB/K8s probes).
- Startup must be idempotent (DB is destroyed/recreated often): migrations + optional demo seed.
- Images must run on linux/amd64 (dev machine is Apple Silicon); use `docker buildx build --platform linux/amd64`.

## Local toolchain
The Mac has **no .NET SDK or Node installed**; only Docker Desktop. Run builds/tests in containers:

```bash
# Backend tests (from app/backend)
docker run --rm -v "$PWD":/src -v gymnote-nuget:/root/.nuget/packages -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet test

# Frontend typecheck/tests/build (from app/frontend); node_modules stays in a container volume
docker run --rm -v "$PWD":/src -v /src/node_modules -w /src node:24-alpine \
  sh -c 'npm ci && npm run typecheck && npm test && npm run build'

# Full stack -> http://localhost (demo: demo@gymnote.local / DemoPass123!)
cd app && docker compose up --build
```

New EF migration: install `dotnet-ef` 10.0.13 inside the sdk container and run
`dotnet-ef migrations add <Name> --project src/GymNote.Api --output-dir Data/Migrations`.

## Non-obvious decisions
- Backend tests use the EF Core **InMemory** provider (via `GymNoteApiFactory`) so CI needs no DB/Docker;
  Postgres-specific behavior (unique email index, check constraints) is not covered by `dotnet test`.
- Exercise endpoints return 401 if the token's user no longer exists (handles DB recreation); the frontend
  redirects to login on any authenticated 401.
- Data-protection keys are kept in memory (`InMemoryXmlRepository`) so nothing is written to disk.
- `Microsoft.EntityFrameworkCore.Relational` is pinned explicitly to keep EF package versions aligned.
- Connection strings should include `GSS Encryption Mode=Disable` (aspnet image lacks Kerberos libs).
- Solution file is `app/backend/GymNote.slnx` (.NET 10 default format).

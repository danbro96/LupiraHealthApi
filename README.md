# LupiraHealthApi

A personal health-tracking API — sibling service to `LupiraCalApi`, sharing the same identity (Authentik OIDC) and
operational conventions (.NET 10, Marten on Postgres, Minimal APIs, `OpResult<T>`, OpenTelemetry → OpenObserve, Docker +
GitHub Actions → Docker Hub) but owning its **sensitive data in its own database** (`lupira_health`), isolated from the
calendar service.

## Phase 1 scope

- **Identity & ownership** — JIT-provisioned `Principal` (OIDC `sub` is the only cross-service join key). A
  single-owner `HealthRecord` container; every resource hangs off it. (No co-owner sharing yet.)
- **Devices** — register/list/rename/retire; registration mints a one-time per-device **ingest API key**.
- **Location tracking (the priority feature)** — batched NDJSON ingest (idempotent via device `seq`, resumable via a
  cursor, pausable), raw/thinned track + distance/speed stats + bounding-box queries, on-read downsampling, and
  derived **Visits / Trips / DailyLocationSummary** (materialized by a rollup), plus a coarse "where was I at T" place
  label. Raw fixes live in time-partitioned `telemetry` tables (native weekly partitioning, no PostGIS/TimescaleDB).
- **Ring telemetry** — batched ingest of point-samples + device summaries; on-read downsampling.

Deferred to phase 2: clinical records (immunizations, health profile), co-owner sharing, and the LupiraCal synergy
bridge.

## Architecture

- `src/LupiraHealthApi.Core` — domain + application services + DTOs (zero ASP.NET). Marten owns the `health` schema;
  the high-frequency time-series lives in a separate `telemetry` schema written by raw Npgsql (binary-array idempotent
  merge), which Marten's schema-diff never touches.
- `src/LupiraHealthApi` — thin ASP.NET host: Minimal-API endpoint groups → handlers → services. Two auth policies:
  `ApiPolicy` (OIDC JWT for humans) and `IngestPolicy` (per-device API key for the uploader).

## Develop

```bash
dotnet build LupiraHealthApi.slnx -c Release
dotnet test  LupiraHealthApi.slnx -c Release      # Server.Tests use Testcontainers (Docker required)
# Apply schema (health + telemetry) to a local/prod DB:
dotnet run --project src/LupiraHealthApi -- --apply-schema
```

In Development, authenticate REST calls with `X-Dev-User: you@example.com`; ingest calls use
`Authorization: DeviceKey {keyId}.{secret}` (from `POST /api/devices`).

A custom mobile app (built separately) pushes GPS + ring telemetry to the ingest endpoints.

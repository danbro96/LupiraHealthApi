# LupiraHealthApi

A small, self-hostable **personal health-vitals API**. It owns three things:

- **Smart-ring telemetry** — heart rate, HRV, SpO₂, skin temperature, steps, activity, plus device-computed
  summaries (e.g. sleep sessions). Batched, store-and-forward ingest from a companion mobile app.
- **A health-record container** — a single-owner record that every resource hangs off.
- **Devices** — register / list / rename / retire the hardware that feeds the record; each registration mints a
  one-time per-device ingest key.

It is built as a thin ASP.NET host over a transport-neutral core, stores discrete state as
[Marten](https://martendb.io) documents and high-frequency time-series in raw partitioned Postgres tables, and
authenticates humans with OIDC and devices with per-device API keys.

> **Scope.** Phase 1 is single-owner, no sharing. Location/presence tracking is intentionally *not* here — it
> lives in a separate service. Clinical records (immunizations, health profile) and co-owner sharing are
> deferred to phase 2. See [docs/architecture.md](docs/architecture.md) for the full design.

## Surfaces

- **REST** under `/api` — see the [endpoint map](#endpoint-map).
- **OpenAPI** document at `/openapi/v1.json`.
- **Interactive API reference** ([Scalar](https://scalar.com)) at `/scalar/v1`.
- **MCP** at `/mcp` — read-only agent tools (`whoami`, `list_health_records`, `list_devices`, `read_vitals`, `read_summaries`); OIDC-gated, LAN/WireGuard-only. No gRPC or message bus.

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10, ASP.NET Core Minimal APIs |
| Document store | Marten 9.6 on PostgreSQL (schema `health`) |
| Time-series | Raw Npgsql, native range-partitioned tables (schema `telemetry`) |
| Auth | JWT bearer (OIDC) for users · per-device API key for ingest |
| API docs | `Microsoft.AspNetCore.OpenApi` 10.0 + Scalar 2.16 |
| Observability | OpenTelemetry 1.16 (OTLP exporter, env-gated) |
| Tests | xUnit + Testcontainers (ephemeral Postgres) |

## Run locally

**Prerequisites:** the .NET 10 SDK and a PostgreSQL instance. Docker is required for the test suite
(Testcontainers spins up a throwaway Postgres).

```bash
# Build & test
dotnet build LupiraHealthApi.slnx -c Release
dotnet test  LupiraHealthApi.slnx -c Release          # Server.Tests need Docker

# Apply the schema (Marten `health` + raw `telemetry`) to the DB in ConnectionStrings__Postgres
dotnet run --project src/LupiraHealthApi -- --apply-schema

# Run
ConnectionStrings__Postgres="Host=localhost;Database=lupira_health;Username=postgres;Password=postgres" \
ASPNETCORE_ENVIRONMENT=Development \
dotnet run --project src/LupiraHealthApi
```

Then open `http://localhost:8080/scalar/v1`.

**Authenticating in development.** Production uses real OIDC tokens, but you don't need an identity provider to
exercise the API locally. With `ASPNETCORE_ENVIRONMENT=Development`, a dev-only auth handler accepts an
`X-Dev-User` header and provisions a principal from it:

```bash
# Human/API calls — pretend to be this user (Development only; ignored otherwise)
curl http://localhost:8080/me -H "X-Dev-User: you@example.com"

# Register a device — the response carries a one-time ingest key "{keyId}.{secret}"
curl -X POST http://localhost:8080/devices -H "X-Dev-User: you@example.com" \
  -H 'Content-Type: application/json' \
  -d '{"healthRecordId":"<record-guid>","kind":"SmartRing","label":"My ring"}'

# Ingest calls — authenticate with that device key (this is the only ingest auth, in any environment)
curl -X POST http://localhost:8080/ingest/ring \
  -H "Authorization: DeviceKey <keyId>.<secret>" -H 'Content-Type: application/x-ndjson' \
  --data-binary $'{"seq":1,"kind":"hr","ts":"2026-01-01T09:00:00Z","value":62}\n'
```

## Endpoint map

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/me` | user | Resolve the caller's identity (provisioned on first call) |
| `POST` | `/me/bootstrap` | user | Idempotently ensure the caller has a personal health record |
| `GET` | `/records/` | user | List health records owned by the caller |
| `POST` | `/records/` | user | Create a health record (caller becomes owner) |
| `GET` | `/devices/?recordId=` | user | List devices on a record |
| `POST` | `/devices/` | user | Register a device; returns a one-time ingest key |
| `PUT` | `/devices/{id}` | user | Rename a device |
| `DELETE` | `/devices/{id}` | user | Retire a device and revoke its ingest keys |
| `POST` | `/ingest/ring` | device | Ingest a batch of ring point-samples (NDJSON) |
| `POST` | `/ingest/summaries` | device | Ingest a batch of device-computed summaries (NDJSON) |
| `GET` | `/health/ring` | user | Downsampled ring metric (avg/min/max/count per bucket) |
| `GET` | `/health/summaries` | user | Device-computed summaries over a time range |

**user** = OIDC JWT (or the dev header in Development), via the `ApiPolicy`. **device** = per-device API key
`Authorization: DeviceKey {keyId}.{secret}`, via the `IngestPolicy`. Errors are returned as RFC 7807
`application/problem+json` (400/403/404/409).

## Configuration

All configuration is environment-driven (standard ASP.NET `Section__Key` binding). There are no hard-coded
hosts; the values below are examples.

| Variable | Required | Example | Purpose |
|---|---|---|---|
| `ConnectionStrings__Postgres` | yes | `Host=localhost;Database=lupira_health;Username=…;Password=…` | Postgres for both schemas |
| `Auth__Authority` | prod | `https://id.example.com/application/o/health/` | OIDC issuer (token validation) |
| `Auth__Audience` | prod | `lupira-health` | Expected JWT audience |
| `Telemetry__MaintenanceEnabled` | no | `true` | Pre-provision upcoming monthly partitions (background job) |
| `ASPNETCORE_ENVIRONMENT` | no | `Development` | `Development` enables the `X-Dev-User` auth on-ramp |
| `ASPNETCORE_URLS` | no | `http://+:8080` | Listen address (container default) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | no | `https://otel.example.com` | Enables OTLP export when set; unset = no export |
| `OTEL_EXPORTER_OTLP_HEADERS` | no | `Authorization=Basic …` | OTLP auth headers |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | no | `http/protobuf` | OTLP protocol |

In Development, `Auth__Authority`/`Auth__Audience` may be omitted because the `X-Dev-User` header stands in for a
real token.

## Database & schema

Two schemas in one database, applied **deliberately** (never on boot) by a one-shot:

```bash
dotnet run --project src/LupiraHealthApi -- --apply-schema
```

This runs Marten's schema migration for the `health` document store, then applies the raw `telemetry` schema
(ring/summary tables + initial monthly partitions). Both steps are idempotent and additive. Marten's schema-diff
never touches `telemetry`.

## Deploy with Docker / Compose

The repo ships a [`Dockerfile`](Dockerfile) (multi-stage; runtime listens on `8080`) and a sample
[`deploy/compose.yaml`](deploy/compose.yaml). The compose file's hostnames and ports are **illustrative
defaults** — override them via environment to suit your host:

```bash
docker build -t lupira-health-api .
docker run -p 8080:8080 \
  -e ConnectionStrings__Postgres="Host=…;Database=lupira_health;Username=…;Password=…" \
  -e Auth__Authority="https://id.example.com/application/o/health/" \
  -e Auth__Audience="lupira-health" \
  lupira-health-api
```

Run the image once with `--apply-schema` against a fresh database before first traffic.

## Health probes

- `GET /livez` — liveness (process up; no dependency checks).
- `GET /readyz` — readiness (returns 200 only when Postgres is reachable).

## CI

GitHub Actions ([`.github/workflows`](.github/workflows)): every PR/branch builds and runs the full unit +
Testcontainers integration suite. On merge to `main` (or a `v*` tag) the workflow re-runs that suite and pushes
a container image tagged `latest` and `sha-<short>` (plus semver for tags).

## Project layout

```
src/
  LupiraHealthApi.Core/   Domain + application services + DTOs + Marten/telemetry data. No ASP.NET dependency.
  LupiraHealthApi/        Thin host: Minimal-API endpoint groups → handlers → Core services; auth; OpenAPI/Scalar.
tests/
  LupiraHealthApi.Core.Tests/     Pure unit tests (value objects, telemetry internals).
  LupiraHealthApi.Server.Tests/   Integration tests over a real Postgres (Testcontainers).
deploy/                   Sample Dockerfile compose definition.
docs/architecture.md      Design, bounded context, identity model, and the domain class diagram.
```

## License

[MIT](LICENSE) © 2026 Daniel Broström.

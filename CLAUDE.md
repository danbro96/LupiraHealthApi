# LupiraHealthApi — repo rules

Docs: `docs/architecture.md`.

## Split from LupiraLocationApi
- Here: ring vitals, the health-record container, devices. Not here: GPS, visits, trips (LupiraLocationApi).
- The only shared contract between the services is the Authentik `sub`. No shared database, schema or foreign keys.
- The ingest engine (NDJSON merge, ingest service) is duplicated in LupiraLocationApi on purpose; fix an engine bug in both. Partitions and device keys come from `Lupira.Postgres.Partitions` and `Lupira.Auth.DeviceKeys`.

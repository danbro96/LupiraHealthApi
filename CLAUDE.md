# LupiraHealthApi — repo rules

Docs: `docs/architecture.md`.

## Split from LupiraLocationApi
- Here: ring vitals, the health-record container, devices. Not here: GPS, visits, trips (LupiraLocationApi).
- The only shared contract between the services is the Authentik `sub`. No shared database, schema or foreign keys.
- The ingest engine (`PartitionManager`, NDJSON merge, ingest service) is duplicated in both repos on purpose. Fix an engine bug in both. Do not extract a shared NuGet package.

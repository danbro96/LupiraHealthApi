-- lupira-health-api: provision the `lupira_health` database on the shared medelynas-db.
-- One role, one logical database, isolated from lupira_cal (no cross-grants). The app owns the `health` schema
-- (Marten, via `--apply-schema`) AND the `telemetry` schema (raw partitioned tables, via TelemetrySchema applied by
-- the same `--apply-schema` step) — none of those tables are created here.
--
-- Apply (TrueNAS Shell), substituting a freshly generated password:
--   LUPIRA_HEALTH_DB_PW="$(openssl rand -hex 32)"; echo "$LUPIRA_HEALTH_DB_PW"   # save to your password manager
--   docker exec -i medelynas-db psql -U medelynas_admin -v app_password="'$LUPIRA_HEALTH_DB_PW'" postgres < grants.sql

CREATE ROLE lupira_health_user WITH LOGIN PASSWORD :'app_password';
CREATE DATABASE lupira_health OWNER lupira_health_user;
REVOKE ALL ON DATABASE lupira_health FROM PUBLIC;
GRANT CONNECT ON DATABASE lupira_health TO lupira_health_user;

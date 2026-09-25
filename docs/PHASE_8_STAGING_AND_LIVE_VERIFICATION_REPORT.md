# Phase 8 — Staging and Live Verification Report

Date: 2026-09-18  
Host: Windows 11 (`10.0.26200`, win-x64), Asia/Colombo  
Repository: `C:\Users\Piratheepan\Desktop\Project1`  
Verdict: **NOT READY FOR PRODUCTION**

Phase 8 remediated the missing versioned-migration artifacts and preserved the green application regression. The host still cannot run the staging stack because Docker/Compose and PostgreSQL client tooling are absent. Therefore no live PostgreSQL, RabbitMQ, cross-service, recovery, or browser workflow is reported as passed.

## 1. Environment and tooling inventory

| Tool | Actual command | Result |
|---|---|---|
| Docker Engine/Desktop | `docker --version` | **MISSING** — command not found |
| Docker Compose | `docker compose version` | **MISSING** — command not found |
| WSL | `wsl --status` | **AVAILABLE** — default `kali-linux`, WSL 2 |
| PostgreSQL client | `psql --version` | **MISSING** — command not found |
| .NET SDK/runtime | `dotnet --info` | **AVAILABLE** — SDK 8.0.423, runtime 8.0.29 |
| EF CLI | temporary local tool + `DOTNET_ROOT` | **AVAILABLE** — 8.0.23; no system-wide install |
| Java | `mvn -version` | **AVAILABLE** — Oracle Java 21.0.10 |
| Maven | `mvn -version` | **AVAILABLE** — 3.9.11 |
| Node.js | `node --version` | **AVAILABLE** — 22.17.0 |
| npm | `npm --version` | **AVAILABLE** — 11.7.0 |
| Playwright | `npx playwright --version` | **AVAILABLE** — 1.63.0, Chromium previously installed |

No privileged or system-wide installation was attempted. On this Windows host, install Docker Desktop with the WSL2 backend from `https://docs.docker.com/desktop/setup/install/windows-install/`, then install PostgreSQL client tools from `https://www.postgresql.org/download/windows/`. Reopen PowerShell and repeat the version commands before staging verification.

## 2. Architecture and Docker diagram

```text
Browser -> Next.js frontend -> nginx/YARP gateway
                                  |-- Identity ---------- PostgreSQL/identity
                                  |-- Booking + Finance - PostgreSQL/bookings
                                  |-- Hotel Operations -- PostgreSQL/hotelops
                                  |-- Field Operations -- PostgreSQL/fieldops
                                  |-- Notifications ----- PostgreSQL/notifications + Redis
                                  `-- Concierge

Booking / Hotel Ops / Field Ops / Notifications <--> RabbitMQ
Hotel Ops ------------------------------------------> external Supabase Storage
```

No connection was made to the configured external Supabase project. The current Supabase changelog was reviewed; the 2026 Data API exposure and self-hosted Postgres changes do not alter these direct local PostgreSQL migrations, but any future public-schema/Data API use must be explicitly granted and protected with RLS.

## 3. Docker Compose startup instructions

Phase 7's hardened `infra/docker-compose.yml` remains the staging definition. Use non-production-only values:

```powershell
Copy-Item .env.example .env
# Replace every CHANGE_ME value. Keep PayHere sandboxed; never configure payout credentials.
docker compose --env-file .env -f infra/docker-compose.yml config
docker compose --env-file .env -f infra/docker-compose.yml up --build -d
docker compose --env-file .env -f infra/docker-compose.yml ps
docker compose --env-file .env -f infra/docker-compose.yml logs --tail 200
```

Startup result: **NOT RUN**. Docker is unavailable, so Compose parsing, builds, container health, service discovery, gateway routes, and startup readiness are unverified.

## 4. Database migration inventory and remediation

| Database owner | Framework/artifact | Artifact result | Runtime change |
|---|---|---|---|
| Identity | Existing EF migrations | Existing partial chain retained | Existing behavior retained |
| Booking & Finance | EF Core `InitialVersionedSchema` + snapshot | **GENERATED; SQL SCRIPT PASSED** | `MigrateAsync` replaces `EnsureCreatedAsync` |
| Hotel Operations | EF Core `InitialVersionedSchema` + snapshot | **GENERATED; SQL SCRIPT PASSED** | `MigrateAsync` replaces `EnsureCreatedAsync` |
| Notifications | EF Core `InitialVersionedSchema` + snapshot | **GENERATED; SQL SCRIPT PASSED** | `Migrate` replaces `EnsureCreated` |
| Field Operations | Flyway `V1__initial_versioned_schema.sql` | **GENERATED FROM JPA METADATA; POSTGRES VALIDATION NOT RUN** | Hibernate default changed from `update` to `validate`; Flyway clean disabled |

The three API projects now explicitly reference `Microsoft.EntityFrameworkCore.Design` 8.0.8. EF tooling 8.0.23 generated idempotent deployment SQL successfully using a design-time-only local connection string; it did not connect to a database.

Representative command:

```powershell
$env:DOTNET_ROOT='C:\Users\Piratheepan\AppData\Local\Microsoft\dotnet'
$env:ConnectionStrings__DefaultConnection='Host=localhost;Database=phase8_design;Username=phase8;Password=design_time_only'
dotnet-ef migrations script --idempotent --context BookingDbContext
```

Result for Booking, Hotel Ops, Notifications: **PASSED — build and SQL generation, exit 0**.

The Flyway V1 SQL was exported from the current Hibernate model. The one-off export command produced the SQL but its Spring test context ended with 3 errors, so that attempt is classified **FAILED** and is not PostgreSQL evidence. The normal Field Ops suite subsequently passed with Flyway disabled under the H2 test profile.

Important baseline constraint: populated databases previously created by `EnsureCreated`/`ddl-auto=update` have no trustworthy migration history. Applying these initial migrations directly would collide with existing tables. A schema-specific baseline must be reviewed against a disposable clone before any existing environment is upgraded. No automatic `baseline-on-migrate` was enabled because that could conceal drift.

## 5. Fresh-database migration results

**NOT RUN.** Docker and `psql` are missing. Artifact generation passed, but no migration was executed against PostgreSQL. Table creation, PostgreSQL-specific syntax, foreign keys, checks, indexes, application startup, and repeat-deployment behavior remain unproven.

Required staging commands after tooling installation:

```powershell
docker compose --env-file .env -f infra/docker-compose.yml up -d postgres
dotnet ef database update --context BookingDbContext
dotnet ef database update --context HotelOpsDbContext
dotnet ef database update --context NotificationsDbContext
mvn -Dflyway.url=jdbc:postgresql://localhost:5432/smarthotel_fieldops flyway:migrate
```

Discover and confirm the exact Flyway Maven goal/configuration after Docker is available; do not copy credentials into shell history.

## 6. Existing-data migration results

**NOT RUN.** No disposable historical PostgreSQL schema was available, and the repository does not contain a reliable pre-feature schema snapshot. Data preservation for invoices, payroll, F&B snapshots/outbox, room claims, and audit history therefore remains unverified.

Required method: restore a sanitized backup into an isolated database, compare schema to the generated baseline, create a reviewed baseline/history entry only when exact equivalence is proven, apply later migrations, and compare row counts, hashes for immutable financial columns, foreign-key violations, and unique-key behavior. Never baseline an unknown or production database by assumption.

## 7. Live PostgreSQL results

**NOT RUN** — neither a PostgreSQL server/container nor `psql` is available. Backup and restore were not tested. The Phase 7 `pg_dump`/`pg_restore` procedure remains documentation only.

## 8. RabbitMQ integration results

**NOT RUN** — no Docker/RabbitMQ runtime. Exchanges, queues, bindings, credentials, publisher confirms, consumers, dead-letter behavior, retry bounds, and restart recovery are not live-verified. Application tests cover consumer/outbox behavior with test infrastructure only; no exactly-once claim is made.

## 9. Cross-department E2E results

| Workflow | Result | Exact reason |
|---|---|---|
| Front Office through turnover | **NOT RUN** | No live services/PostgreSQL/RabbitMQ |
| Housekeeping reject/reclean/self-inspection | **NOT RUN** | Same blocker |
| Maintenance restriction/reinspection | **NOT RUN** | Same blocker |
| F&B through Finance invoice confirmation | **NOT RUN** | Same blocker |
| Attendance through mock salary payment | **NOT RUN** | Same blocker |

No real customer payment or salary transfer was initiated.

## 10. Failure and recovery results

**PASSED at application-test level:** concurrent room-claim defenses, Finance 503 retry behavior, F&B charge idempotency, repeated workflow calls, payroll ordering, and mock payout duplication defenses.

**NOT RUN live:** broker/database outage, consumer restart, duplicate/out-of-order RabbitMQ delivery, pending outbox restart, Hotel Ops outage during check-in, ambiguous database commit, repeated checkout across services, and reconciliation visibility. Docker/RabbitMQ/PostgreSQL are the missing dependencies; install them and run the disposable stack to correct this.

## 11. Playwright browser results

Command:

```powershell
npm run test:e2e -- --reporter=list
```

Result: **SKIPPED — 0 passed, 0 failed, 9 skipped; command exit 0**.

Each configured test—Owner, Front Office Receptionist, Housekeeping Manager, Housekeeper, Maintenance Manager, Technician, Finance Manager, Chef, and Waiter—was explicitly skipped because environment credentials were absent. The live backend is also unavailable. Corrective action: start a healthy staging stack, seed the nine non-production users, supply credentials through uncommitted environment variables, and rerun. Current tests verify real-login role access only; detailed browser workflow transitions still require expansion once deterministic staging data exists.

## 12. Security verification

| Control | Result |
|---|---|
| JWT issuer/audience/signature/expiry and gateway routes | **PASSED — automated tests** |
| Department and Owner authorization boundaries | **PASSED — automated tests** |
| Worker/inspector and approval separation | **PASSED — automated tests** |
| Fake frontend JWT/room fallback absence | **PASSED — retained from Phase 7** |
| Service-to-service authentication over staging network | **NOT RUN** |
| Guest masking and sensitive-log inspection in live stack | **NOT RUN** |
| RabbitMQ/database least-privilege permissions | **NOT RUN** |
| Correlation IDs across live calls | **NOT RUN** |
| Outbox metrics/alerts | **NOT RUN** |
| Backup/restore rehearsal | **NOT RUN** |

Compose requires secrets but still uses a shared PostgreSQL superuser for services. Per-service database roles with least privilege are a deployment prerequisite.

## 13. Backend regression results

Freshly executed on this host:

| Suite | Command | Result |
|---|---|---:|
| Identity | `dotnet test SmartHotel.Identity.sln -c Release --nologo` | **PASSED 85/85, exit 0** |
| Booking & Finance | `dotnet test SmartHotel.Booking.sln -c Release --nologo` | **PASSED 120/120, exit 0** |
| Hotel Ops | `dotnet test SmartHotel.HotelOps.sln -c Release --nologo` | **PASSED 54/54, exit 0** |
| Field Ops | `mvn test -q` | **PASSED 75/75, exit 0** |
| API Gateway | `dotnet test ...SmartHotel.Gateway.IntegrationTests.csproj -c Release --nologo` | **PASSED 30/30, exit 0** |
| Phase baseline total | | **PASSED 364/364** |
| Notifications | `dotnet test -c Release --nologo` | **PASSED 19/19, exit 0** |

## 14. ESLint and frontend build

| Command | Result |
|---|---|
| `npm run lint` | **PASSED — 0 errors, 0 warnings, exit 0** |
| `npm run build` | **PASSED — Next.js 16.2.10, 40 routes, exit 0** |

## 15. Changed files

Phase 8 added or changed:

- Booking API EF design dependency, startup migration call, initial migration/designer/snapshot
- Hotel Ops API EF design dependency, startup migration call, initial migration/designer/snapshot
- Notifications API EF design dependency, startup migration call, initial migration/designer/snapshot
- Field Ops `pom.xml`, production/test configuration, and Flyway V1 baseline
- This report

The worktree already contained extensive Phase 1–7 changes and generated artifacts; unrelated changes were preserved.

## 16. Remaining blockers

1. **Critical:** Docker/Compose and `psql` are missing.
2. **Critical:** migrations have not run on fresh PostgreSQL.
3. **Critical:** no historical-schema clone exists for existing-data migration validation or safe baselining.
4. **Critical:** live cross-service, RabbitMQ, failure/recovery, and all browser role tests remain unexecuted.
5. **High:** the Field Ops V1 script needs disposable PostgreSQL validation and schema comparison.
6. **High:** Identity still contains legacy startup schema mutation behavior and needs a separately reviewed complete migration transition.
7. **High:** Booking retains legacy idempotent startup DDL following `MigrateAsync`; it must be removed only after the migration is proven equivalent on both test databases.
8. **High:** service database credentials are not least-privilege per service.
9. **High:** npm audit advisories reported in Phase 7 remain to be safely triaged.
10. **Medium:** live correlation, readiness dependencies, structured sensitive-log review, outbox dashboards, and alerting are not evidenced.

## 17. Deployment prerequisites

- Install and verify Docker Desktop/Compose and PostgreSQL client tools.
- Validate Compose with `docker compose config` before startup.
- Use disposable PostgreSQL databases and non-production RabbitMQ/Redis credentials.
- Review generated migrations and Flyway SQL with a DBA; add per-service database roles.
- Execute fresh and restored-existing-data migration rehearsals, including backup/restore and invariants.
- Remove legacy runtime DDL only after equivalence is proven.
- Start all services and require dependency-aware health checks to pass.
- Seed nine test users without committing or logging credentials.
- Complete live workflow, concurrency, failure/recovery, security, and Playwright matrices.
- Triage dependency advisories and repeat all regression tests.
- Define measured CPU/memory limits and operational alerts.

## 18. Evidence-based readiness assessment

**Application regression: PASSED.** The 364-test baseline, 19 Notification tests, lint, and 40-route production build are green after migration remediation.

**Migration artifact readiness: PARTIAL.** The missing EF and Flyway initial artifacts now exist and generate/build, but they are not PostgreSQL-executed and populated databases require deliberate baselining.

**Staging readiness: BLOCKED.** The host lacks Docker/Compose and PostgreSQL tooling.

**Production readiness: FAILED / NOT READY.** Critical migration execution, existing-data preservation, live integration, RabbitMQ recovery, security, backup/restore, and browser evidence remain incomplete. No production deployment, paid provisioning, real payment, or real salary transfer occurred.

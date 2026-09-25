# Phase 7 — Integration and Readiness Report

Date: 2026-09-18  
Repository: `C:\Users\Piratheepan\Desktop\Project1`  
Verdict: **NOT READY FOR PRODUCTION DEPLOYMENT**

The application-level regression is green, the frontend lint baseline is clean, and a reproducible Compose definition has been hardened. Production readiness cannot be claimed because PostgreSQL migration coverage is incomplete and neither the disposable live stack nor live browser workflows could be executed on this workstation.

## 1. Actual architecture

```text
Browser / Next.js frontend
          |
          v
YARP API Gateway (/api/*, JWT validation and route policy)
  |---------- Identity Service ---------- PostgreSQL: identity_db
  |---------- Booking & Finance --------- PostgreSQL: smarthotel_bookings
  |---------- Hotel Operations ---------- PostgreSQL: smarthotel_hotelops_db
  |---------- Field Operations ---------- PostgreSQL: smarthotel_fieldops
  |---------- Notification Service ------ PostgreSQL: smarthotel_notifications
  `---------- AI Concierge -------------- Chroma/local data

Booking/Hotel Ops/Field Ops/Notifications <---- RabbitMQ
Notifications <------------------------------- Redis
Hotel Ops ------------------------------------ Supabase Storage (configured externally)
```

The reviewed Identity seeder creates canonical departments before employee accounts and uses stable lookup/update behavior. Its integration and unit tests passed. The existing architecture and routes were retained.

## 2. Docker environment and startup

`infra/docker-compose.yml` now includes the frontend and the existing gateway, Identity, Booking, Hotel Ops, Field Ops, Notifications, Concierge, PostgreSQL, RabbitMQ, Redis, and nginx services. The Compose configuration now requires PostgreSQL, RabbitMQ, Redis, and biometric encryption secrets; propagates broker credentials consistently; authenticates Redis health checks; uses service DNS names internally; retains the PostgreSQL named volume; and adds frontend health/startup dependency handling.

Prepare a local-only environment from `.env.example`, replacing every `CHANGE_ME` value, then run:

```powershell
Copy-Item .env.example .env
# Edit .env with non-production secrets only.
docker compose --env-file .env -f infra/docker-compose.yml config
docker compose --env-file .env -f infra/docker-compose.yml up --build -d
docker compose --env-file .env -f infra/docker-compose.yml ps
docker compose --env-file .env -f infra/docker-compose.yml logs --tail 200
```

Result: **NOT RUN**. `docker` is not installed or on PATH, so Compose parsing, image builds, startup, service discovery, health checks, RabbitMQ topology, and live gateway routing remain unverified.

## 3. Database migration inventory

| Owner | Observed approach | Versioned coverage | Assessment |
|---|---|---:|---|
| Identity | EF Core migrations | `AddRefreshTokens`, `AddDepartmentRoleSlots`, snapshot | Partial; migration CLI could not be executed |
| Booking & Finance | EF Core model plus startup `EnsureCreatedAsync` | None found | **Critical gap** |
| Hotel Operations | EF Core model plus startup `EnsureCreatedAsync` | None found | **Critical gap** |
| Field Operations | Hibernate `ddl-auto=${SPRING_JPA_HIBERNATE_DDL_AUTO:update}` | No Flyway/Liquibase migrations found | **Critical gap** |
| Notifications | EF model plus startup `EnsureCreated` | None found | Gap for production evolution |

`infra/postgres/init-scripts/01-init-databases.sql` creates the service databases only; it is not a substitute for ordered schema migrations. The recently added department authorization, Front Office, Finance/payroll, F&B, Housekeeping, and Maintenance entities therefore do not all have deterministic, reviewable upgrade scripts. No competing migration framework was introduced during this phase.

The attempted Identity inventory command was:

```powershell
dotnet ef migrations list --no-connect
```

Result: **FAILED (tooling)**. The installed global `dotnet-ef.exe` could not resolve `hostfxr.dll`/a .NET runtime, although normal `dotnet test` commands work.

## 4. Fresh-database migration result

**NOT RUN.** Docker and `psql` are unavailable, and the migration inventory above is incomplete. No destructive reset was attempted and no result was inferred from H2 or EF in-memory tests.

## 5. Existing-data migration result

**NOT RUN.** A representative pre-migration PostgreSQL database could not be created. Data preservation, deterministic re-deployment, production indexes/constraints, and rollback behavior remain unverified.

Before deployment, each database owner must add versioned migrations from the reviewed models, back up a representative database, apply migrations to a clone, verify row counts and invariants, re-run migration deployment to prove idempotent orchestration, and perform an application smoke test.

Backup/restore rehearsal (disposable targets only):

```powershell
pg_dump --format=custom --no-owner --file smart-hotel-pre-migration.dump DATABASE_URL
createdb smart_hotel_restore_check
pg_restore --exit-on-error --no-owner --dbname smart_hotel_restore_check smart-hotel-pre-migration.dump
psql --dbname smart_hotel_restore_check --command "SELECT current_database(), now();"
```

The restore target must be resolved and confirmed as disposable before creation or replacement. Retention, encryption, access control, restore timing, and application-level invariant queries must be agreed operationally.

## 6. Live cross-service E2E

| Workflow | Result | Evidence |
|---|---|---|
| Front Office reservation through turnover | **NOT RUN** | Live PostgreSQL/RabbitMQ/services unavailable |
| Housekeeping turnover, rejection, reclean | **NOT RUN** | Same blocker |
| Maintenance restriction through reinspection | **NOT RUN** | Same blocker |
| F&B through durable Finance charge | **NOT RUN** | Same blocker |
| Attendance through mock payroll/payslip | **NOT RUN** | Same blocker |

The passing application tests exercise these rules in-process or with test doubles/H2. They are not relabeled as live E2E. No real customer payment or salary payout was initiated.

## 7. Failure and recovery

Application tests passed for concurrent room claims, Finance 503/outbox retry behavior, duplicate charge defenses, repeated workflow calls, authorization boundaries, and mock payout idempotency. Field Ops tests also exercise duplicate/retry-oriented durable workflow behavior.

Live failure injection—PostgreSQL/RabbitMQ outages, service restart during pending outbox work, duplicate/out-of-order broker delivery, and ambiguous cross-service commit outcomes—is **NOT RUN** because the disposable stack could not start. Consequently, there is no evidence here for exactly-once delivery; the design relies on durable state, idempotency, and retry where implemented.

## 8. Playwright

Playwright was added as a development dependency with Chromium, `playwright.config.ts`, nine role/access tests, environment-only credentials, and package scripts. Tests use `/staff/login` and the real backend; no mocked API is substituted.

Command:

```powershell
npm run test:e2e -- --reporter=list
```

Result: **SKIPPED — 0 passed, 0 failed, 9 skipped**. Owner, Front Office Receptionist, Housekeeping Manager, Housekeeper, Maintenance Manager, Technician, Finance Manager, Chef, and Waiter were skipped because non-production E2E credentials and a running integration backend were unavailable. Full workflow form/state-transition assertions still need implementation after a deterministic seeded integration environment exists.

## 9. Permission and security regression

**PASSED at application/integration-test level:** JWT validation and gateway route authorization; Owner and department policy tests; non-Owner cross-department denial; Front Office readiness restrictions; Finance authorization/approval tests; Housekeeping and Maintenance role separation; payroll ordering and payout idempotency; guest-facing authorization paths.

**NOT RUN live:** browser-backed cross-department checks, service-to-service authentication over the deployed network, sensitive-log inspection, and real gateway-to-service JWT propagation. These remain deployment blockers. Frontend mock JWT login and mock Google/room-data fallbacks were removed so backend/network failure can no longer mint a fake session or substitute fake operational data.

## 10. Frontend quality

Initial verified lint baseline: **52 errors, 99 warnings**.

| Command | Result |
|---|---|
| `npm run lint` | **PASSED — 0 errors, 0 warnings** |
| `npm run build` | **PASSED — Next.js 16.2.10, 40 routes generated** |

Fixes used typed error handling, removed unused code, corrected hook/ref lifecycles and dependencies, and retained existing presentation/navigation. No lint rules were disabled and no blanket suppression was added.

`npm audit` after adding Playwright reports **8 known dependency vulnerabilities: 1 moderate, 6 high, 1 critical**. They were not force-upgraded because that could introduce breaking dependency changes; triage and safe upgrades are required.

## 11. Backend regression

All results below were freshly executed on 2026-09-18.

| Suite | Result |
|---|---:|
| Identity unit + integration | **PASSED — 85/85** |
| Booking & Finance | **PASSED — 120/120** |
| Hotel Operations | **PASSED — 54/54** |
| Field Operations | **PASSED — 75/75** |
| API Gateway integration | **PASSED — 30/30** |
| Total | **PASSED — 364/364** |

Commands:

```powershell
dotnet test SmartHotel.Identity.sln -c Release --nologo
dotnet test SmartHotel.Booking.sln -c Release --nologo
dotnet test SmartHotel.HotelOps.sln -c Release --nologo
dotnet test tests/SmartHotel.Gateway.IntegrationTests/SmartHotel.Gateway.IntegrationTests.csproj -c Release --nologo
mvn test -q
```

The Maven report contained 13 suites, 75 tests, 0 failures, 0 errors, and 0 skipped.

## 12. Phase 7 changed files

The workspace already contained extensive Phase 1–6 changes and unrelated/generated files; they were preserved. Phase 7 materially changed:

- `.env.example`
- `infra/docker-compose.yml`
- `frontend/package.json` and `frontend/package-lock.json`
- `frontend/playwright.config.ts`
- `frontend/tests/e2e/role-access.spec.ts`
- `frontend/.env.e2e.example`
- `frontend/src/lib/apiError.ts`
- Auth and booking frontend modules to remove fake fallbacks
- Frontend source files requiring import, typing, hook, ref, or accessibility lint correction
- This report

Generated Playwright result directories may exist locally and must not be committed. Binary Chroma files shown as modified pre-existed this verification and were not intentionally changed in Phase 7.

## 13. Remaining defects and blockers

1. **Critical:** no complete versioned migration chain for Booking/Finance, Hotel Ops, Field Ops, and Notifications.
2. **Critical:** fresh- and existing-data PostgreSQL migration rehearsals are not run.
3. **Critical:** the live Docker stack, RabbitMQ flows, cross-service workflows, and failure injection are not run.
4. **Critical:** all nine Playwright roles were skipped; browser-based security/workflow evidence is absent.
5. **High:** service-to-service authentication and correlation-ID propagation are not consistently evidenced across all services.
6. **High:** startup `EnsureCreated`/Hibernate `ddl-auto=update` is unsuitable as the production migration strategy.
7. **High:** eight npm dependency advisories require triage.
8. **Medium:** health endpoints exist on core .NET services/gateway, but dependency-aware readiness was not live-verified and Field Ops readiness needs explicit verification.
9. **Medium:** outbox/failed-charge visibility is primarily log based; operational metrics and alerts are not demonstrated.
10. **Tooling:** Docker, `psql`, and a functioning `dotnet-ef` tool host are required on the verification machine.

## 14. Deployment prerequisites

- Install Docker Engine/Desktop with Compose v2, PostgreSQL client tools, and a `dotnet-ef` version matching the repository SDK.
- Create reviewed, ordered, non-destructive migrations for every database-owning service; disable automatic production schema mutation.
- Supply non-production JWT, PostgreSQL, RabbitMQ, Redis, biometrics, SMTP/storage, and E2E credentials through a secret store, never source control.
- Rotate any credential ever shared outside the target secret store.
- Start the disposable stack and require every dependency-aware readiness check to pass.
- Seed deterministic non-production users for all nine roles without logging passwords.
- Execute both migration rehearsals, live workflows, failure/recovery matrix, Playwright suite, and sensitive-log review.
- Establish backup retention and complete a timed restore plus invariant verification.
- Define CPU/memory limits from measured load; no reliable sizing measurement was produced in this blocked environment.
- Triage dependency advisories and rerun lint, build, audit, all 364 backend tests, and E2E after upgrades.

## 15. Evidence-based readiness assessment

**Application regression readiness: PASSED.** All 364 backend tests pass; lint is clean; the 40-route production frontend build succeeds. Security-related fake frontend fallbacks were removed, and local Compose configuration was hardened.

**Integration readiness: INCOMPLETE.** The Compose environment has not been parsed or started, migrations cannot be proven against PostgreSQL, and live service/broker/browser behavior remains untested.

**Production deployment readiness: FAILED / NOT READY.** Missing migration evidence and skipped live security/integration/browser verification are release-blocking. Phase 7 stops here; no production deployment or paid resource provisioning was performed.

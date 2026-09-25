# Finance Department E2E Testing & Security Audit

Audit date: 2026-09-17  
Scope: SmartHotel Finance Department, Booking & Payments service, API Gateway, Finance/Owner portals  
Assurance statement: This is an engineering security review and automated-test report. It is not a production security certification or a substitute for an independent penetration test.

## Executive summary

All nine Finance entities were inspected and exercised through existing and newly added automated tests. Nine security/reliability defects were verified and fixed. The most serious defects allowed a logged-in user to query another customer's payment, allowed staff to bypass provider verification through a manual-confirm endpoint, and accepted a correctly signed PayHere notification without comparing its amount/currency to the recorded payment.

The Booking/Finance suite increased from the previously reported 57 tests to 69 passing tests. Gateway integration tests increased from 27 to 30 and pass. Backend, gateway, TypeScript, and Next.js production builds pass. Frontend ESLint does not pass because the wider existing frontend currently reports 44 errors and 85 warnings in unrelated pages/components.

No real-money operation was executed. Escrow release and settlement success were tested with an isolated fake provider. The configured production adapter remains fail-closed when an eligible provider is unavailable.

## Coverage of the nine entities

| Entity | Automated evidence | Result |
|---|---|---|
| Payment transactions | Signed success/failure handling, amount/currency mismatch, duplicate checkout, IDOR, search/report/export paths | Pass |
| Escrow payments | Checkout eligibility, premature/unauthorized release denial, missing-provider `501`, verified release, settlement webhook, lifecycle records | Pass with fake provider |
| Refund requests | Duplicate key, full/partial limits, approvals, self-approval, missing provider, concurrent cumulative-limit test | Pass |
| Expense management | Cross-department submission rules, document verification, approval separation, self-approval, execution fail-closed | Pass |
| Invoice management | Customer/supplier creation, duplicate charge, lifecycle, partial/full settlement, PDF header, credit-note limits | Pass |
| Payroll management | Net calculation snapshot, verification, Manager review, mandatory Owner approval, restricted access, PDF path, no false disbursement | Pass |
| Settlement/reconciliation | Provider fees, differences, Manager-only completion, completed zero-difference workflow | Pass |
| Financial reports | Verified revenue/refund/expense/payroll calculations, date range, CSV and Excel-compatible exports, non-profit metric label | Pass |
| Financial audit logs | Actor/entity/time persistence, operation coverage, access restrictions, sensitive-value redaction | Pass with remaining DB-admin risk |

Primary test files:

- `services/booking-payments-service/SmartHotel.Booking/tests/SmartHotel.Booking.Tests/FinanceSecurityAuditTests.cs`
- `services/booking-payments-service/SmartHotel.Booking/tests/SmartHotel.Booking.Tests/FinanceOperationsTests.cs`
- `services/booking-payments-service/SmartHotel.Booking/tests/SmartHotel.Booking.Tests/FinanceAuthorizationTests.cs`
- `services/booking-payments-service/SmartHotel.Booking/tests/SmartHotel.Booking.Tests/EscrowWorkflowTests.cs`
- `services/booking-payments-service/SmartHotel.Booking/tests/SmartHotel.Booking.Tests/BookingControllerIntegrationTests.cs`
- `tests/SmartHotel.Gateway.IntegrationTests/RouteAuthorizationTests.cs`

## Role and permission matrix

| Capability | Owner | Finance Admin | Finance Manager | Finance Employee | Front Office | Other departments |
|---|---:|---:|---:|---:|---:|---:|
| Hotel-wide reports | Yes | Explicit grant only | Yes | Explicit grant only | No | No |
| Staff/permission administration | Yes | Safe grants only | No | No | No | Department-scoped HR functions only |
| Expense/refund approval | Exceptional/Owner stage | No automatic authority | Explicit approval grant | No | No | No |
| Escrow release approval | Exceptional/Owner stage | No automatic authority | Explicit approval grant | Prepare only when permitted | No | No |
| Execute provider operations | Explicit operational configuration | No automatic authority | Explicit execution grant | Explicit execution grant | No | No |
| Payroll details | Yes | Explicit payroll grant | Explicit payroll grant | Explicit payroll grant / own payslip | No | No |
| Payment status | Yes | Finance policy | Finance policy | Finance policy | Authorized booking payment status | Own booking only |
| Audit log | Yes | Explicit audit grant | Explicit audit grant | Explicit audit grant | No | No |

Backend controllers enforce these decisions. Frontend navigation is treated only as presentation and is not the security boundary.

## Verified vulnerabilities and fixes

### SEC-FIN-001 — Payment horizontal IDOR — High — Fixed

`GET /api/v1/payments/booking/{bookingId}` accepted any authenticated user and returned payment/provider identifiers for arbitrary booking IDs. Checkout-order creation had the same ownership gap.

Fix: `PaymentsController` now loads the booking and permits only the owning customer, Owner, Finance, or Front Office payment-view roles. Checkout creation permits the booking owner or Owner. Regression: `PaymentLookup_BlocksHorizontalIdor`.

### SEC-FIN-002 — Manual provider-verification bypass — High — Fixed

Front Office/Cashier roles could call the manual confirmation endpoint and mark a booking/payment successful without a provider webhook.

Fix: the endpoint now returns `410 Gone`; provider-verified webhook confirmation is mandatory. Regression: `ManualPaymentConfirmation_IsDisabledForEveryRole`.

### SEC-FIN-003 — Signed webhook amount/currency mismatch — High — Fixed

A valid signature was checked, but the signed amount and currency were not compared with the stored payment. A provider/configuration error could therefore confirm the wrong amount.

Fix: parse using invariant decimal rules and require exact stored amount/currency before any state change. Regression: `SignedWebhook_WithWrongRecordedAmount_IsRejectedWithoutStateChange`.

### SEC-FIN-004 — Concurrent cumulative refund race — High — Fixed

Two distinct refund requests could both read the same available balance before either committed.

Fix: transaction-scoped PostgreSQL advisory locking by payment reference plus the in-memory equivalent used by tests. Regression: `ConcurrentPartialRefunds_CannotExceedPaymentAmount`.

### SEC-FIN-005 — Cross-department stale-grant bypass — High — Fixed

Several operations trusted a Finance grant record without also requiring the caller's current department to be `FINANCE`.

Fix: grant checks in Finance, Finance Operations, and Escrow controllers now also require a current Finance department claim. Regression: `CrossDepartmentGrant_DoesNotBypassFinanceIsolation`.

### SEC-FIN-006 — Provider callback URL manipulation — Medium — Fixed

PayHere return/cancel/notify URLs were accepted from a client request.

Fix: provider callback destinations are now server-controlled. Browser input is ignored.

### SEC-FIN-007 — Duplicate provider/order references — Medium — Fixed

Checkout retries created multiple payment rows, and provider payment identifiers were not uniquely constrained.

Fix: idempotent order creation, application-level provider-reference replay checks, and filtered unique indexes for provider order/payment references. Regression: `CheckoutOrderCreation_IsIdempotent` and model-integrity assertions.

### SEC-FIN-008 — Payment credentials committed/defaulted — Critical credential hygiene — Code fixed; rotation required

Merchant credentials existed in tracked configuration and Docker defaults, while the provider service also had fallback credentials.

Fix: tracked values were removed, Docker Compose now requires environment injection, and the provider fails startup without secure configuration. Test credentials are isolated and explicitly non-production.

Required operational action: rotate/revoke the exposed merchant credentials because removing them from the working tree does not remove them from Git history.

### SEC-FIN-009 — Finance Employee portal denial — Medium — Fixed

The Finance hierarchy used role `Employee`, but the dashboard layout allow-list omitted it, redirecting valid Finance Employees away from their portal.

Fix: `Employee` is now an allowed dashboard role; department navigation still limits it to Finance modules.

### SEC-FIN-010 — Invalid currency caused server error — Medium — Fixed

Several endpoints could throw on malformed currency strings.

Fix: ISO three-letter validation now returns `400 Bad Request` before conversion. Regression included in `InvalidCurrency_ReturnsBadRequest_AndAuditDetailsAreRedacted`.

### SEC-FIN-011 — Sensitive text in audit details — Medium — Fixed

User-supplied reasons could include token/password/card-like values and be copied into Finance audit details.

Fix: sensitive key/value patterns are redacted before persistence. Regression verifies the secret value is absent.

## Payment security findings

- PayHere signature verification uses the provider-required MD5 construction but comparison is now constant-time.
- Merchant ID must match configured merchant ID.
- Signature values are no longer written to logs.
- Stored amount and currency are verified before confirmation.
- Browser success callbacks cannot confirm payments.
- Duplicate checkout, duplicate provider payment ID, duplicate webhook/event, release, refund, invoice, payroll source, and settlement reconciliation keys are protected by application checks and/or unique indexes.
- Payment/refund concurrency uses transaction-scoped advisory locks on PostgreSQL.
- Automated tests use configured test credentials and fake providers only.
- Card credentials are not represented in the domain or stored by these services.

## Database integrity review

Verified from EF model metadata and startup DDL:

- Monetary columns use `numeric(18,2)`/decimal semantics.
- Currency length/check constraints exist on Finance tables.
- Refund/release/settlement foreign keys use restricted deletion where financial history must survive.
- Provider event/reference, idempotency, invoice charge, payroll source, and reconciliation settlement uniqueness is modeled.
- Foreign-key/filter columns have indexes.
- Outstanding invoice, pending payroll, and open reconciliation queries use partial indexes.
- Refund submission and provider webhooks serialize concurrent processing.

Not executed: a real PostgreSQL migration/startup run. Docker and `psql` are unavailable in the audit environment. No configured database was contacted because its production/non-production status could not be proven. No data was reset or mutated.

Important deployment check: legacy databases containing duplicate provider/order references must be cleaned through an approved financial-data remediation process before the new unique indexes can be created. The application does not automatically delete financial duplicates.

## Build and test evidence

| Command/check | Result |
|---|---|
| Booking/Finance `.NET` Release tests | Pass — 69/69 |
| Gateway integration tests | Pass — 30/30 |
| Booking backend Release build | Pass — 0 warnings/errors in final suite build |
| Gateway Release build | Pass |
| Next.js production build and TypeScript | Pass — 29 routes generated |
| Frontend ESLint | Fail — 44 errors, 85 warnings in the wider pre-existing frontend |
| Dedicated PostgreSQL migration execution | Not run — Docker/psql unavailable |
| Live browser Playwright/Cypress E2E | Not run — no browser E2E framework is installed |
| Real provider payment/release/refund | Intentionally not run |

The mocked-provider E2E-style test covers verified held funds, authorized release, provider confirmation, settlement webhook, bank reconciliation, and final reconciliation completion without moving real money.

## Remaining risks and dependencies

1. Rotate the exposed PayHere merchant credentials and consider history rewriting under repository-owner supervision.
2. Run startup DDL against a disposable PostgreSQL clone and verify all indexes/constraints before production deployment.
3. Audit logs are API-immutable but are not cryptographically tamper-evident and remain alterable by privileged database administrators. Consider append-only DB permissions and external/WORM audit export.
4. No real escrow-capable, supplier-payment, or salary-transfer provider is configured. Those actions correctly remain fail-closed.
5. Add Playwright/Cypress coverage for responsive layout, download UX, route transitions, and browser-level loading/error/empty states.
6. Resolve the wider frontend ESLint baseline (44 errors/85 warnings) separately; production compilation currently succeeds.
7. The legacy manual-confirm command class remains in source for compatibility but is unreachable through the disabled API endpoint. Remove it after confirming no internal consumer requires it.
8. PayHere's MD5 signature algorithm is provider-defined; TLS, credential rotation, replay controls, and provider migration planning remain important compensating controls.

## Files materially changed by this audit

- `services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.API/Controllers/PaymentsController.cs`
- `services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.API/Controllers/FinanceController.cs`
- `services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.API/Controllers/FinanceOperationsController.cs`
- `services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.API/Controllers/EscrowController.cs`
- `services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.Application/Features/Payments/Commands/PayHereWebhookCommand.cs`
- `services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.Application/Features/Payments/Commands/CreatePayHereOrderCommand.cs`
- `services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.Infrastructure/Persistence/BookingDbContext.cs`
- `services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.Infrastructure/Services/PayHereService.cs`
- `services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.API/Program.cs`
- `services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.API/appsettings.json`
- `infra/docker-compose.yml`
- `frontend/src/app/dashboard/layout.tsx`
- Finance, Escrow, Booking API, and Gateway test files listed above.

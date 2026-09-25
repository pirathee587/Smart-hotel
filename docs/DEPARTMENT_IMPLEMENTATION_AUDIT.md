# Smart Hotel Department Implementation Audit

Audit date: 2026-09-18

## Scope and source of truth

This audit covers the current working tree, including uncommitted files. It does not assume that a controller, page, or test name proves an end-to-end workflow. Finance was inspected only to establish integration boundaries and is not proposed for redesign.

The identity seed defines five business departments: **Front Office**, **Housekeeping**, **Maintenance**, **Food & Beverage**, and **Finance**. `Unassigned` is a technical holding department. `Security` is an employee role, not a seeded department.

The effective hierarchy is Owner -> department Admin -> department Manager -> operational employee. Identity contains department-scoped creation/approval rules and one active Admin/Manager slot per department. JWTs include department claims and Field Ops enforces them on task, attendance, leave, and attendance-summary operations. Enforcement is not yet consistent in Booking and Hotel Ops.

## Department implementation matrix

| Department | Existing features | Missing or incomplete features | Backend status | Frontend status | Integration status | Tests |
|---|---|---|---|---|---|---|
| Front Office | Reservations list; guest search; payment confirmation; check-in/out; room inventory/status; complaints and review moderation; Admin/Manager/Receptionist roles | Department-specific authorization on booking/check-in/out and room mutations; desk shift/task model; auditable overrides; explicit room-readiness gate; department-scoped reporting | **Partial.** Booking, payment, complaint, review, room and kiosk APIs exist. Several actions rely on broad roles/authentication rather than Front Office department claims. | **Partial.** Real API-backed reservations and room pages exist and use shared UI. No purpose-built Front Office dashboard/work queue; generic pages expose actions based mainly on navigation. | Checkout publishes an event used for turnover cleaning. Check-in does not prove room readiness from authoritative Housekeeping state. Room state is not blocked by unresolved Maintenance work. | Booking 40/40 passed from existing binaries; Hotel Ops 34/34 passed; gateway 30/30 passed. No complete Front Office -> Housekeeping readiness authorization test. |
| Housekeeping | Housekeeping task subtype; auto-allocation; accept/reject/complete/dispatch; employee profiles; booking-checkout turnover task; department filtering; attendance/leave | Inspection workflow API/UI is incomplete; room-clean completion does not authoritatively transition room availability; no linen/inventory workflow; generic task form cannot express full housekeeping fields; audit history is allocation-focused | **Partial.** Strongest non-Finance operational backend. Department isolation is present in `TaskController`; turnover event consumer exists. No reliable Hotel Ops room-state write-back after inspection/completion. | **Partial.** Shared task, rooms, attendance, leave and reports pages. No housekeeping board/inspection UI. | Booking checkout -> Housekeeping task exists. Housekeeping -> Front Office availability is missing, so the loop is one-way. | Field Ops suite passed. Specific booking-event, allocation and task integration tests exist. No end-to-end checkout -> clean -> inspect -> available test. |
| Maintenance | Maintenance work-order subtype; dispatch/allocation; accept/reject/complete; cost and hazard fields in domain; department filtering; attendance/leave | Room/asset out-of-service lifecycle; authorization to return room to service; cost approval/Finance handoff; parts/inventory; maintenance-specific UI and SLA/audit reporting | **Partial.** Work orders exist but are treated through generic task endpoints. No authoritative coupling to Hotel Ops room state or Finance expense records. | **Partial.** Generic tasks/reports/rooms pages only. The current room status API is too broadly authorized for a safe maintenance workflow. | Missing Maintenance issue -> unavailable room -> authorized resolution. Estimated/actual cost does not produce an idempotent Finance expense/request. | Field Ops task/escalation tests and Hotel Ops room state-machine tests pass separately. No cross-service maintenance lifecycle test. |
| Food & Beverage | KDS order entity and create/read/status endpoints; Chef/Waiter identity roles; generic tasks/attendance/leave; dining knowledge in concierge | Role-name mismatch (`Cook`/`Staff` in KDS authorization versus `Chef`/`Waiter` identity roles); department claim enforcement; menu/table/room-service ordering; waiter delivery flow; charge posting; department UI; audit/idempotency | **Incomplete.** KDS is isolated and uses inconsistent RBAC vocabulary. There is no ordering/billing aggregate or Finance charge integration. | **Missing department workflow.** Navigation points Admin/Manager to generic overview/tasks/reports; operational employees only see generic tasks. No KDS page or API client. | No Restaurant/room-service charge -> Finance invoice/transaction flow. Concierge mentions dining but does not complete an operational order. | KDS service tests exist within the passing Field Ops suite, but no identity-role compatibility, frontend, charge, or cross-service tests. |
| Finance | Owner-controlled salary structures/overrides/allowances; verified attendance input; staged payroll approvals; mock payment scenarios; payslips; expense/refund/release/invoice/report APIs; audit/access grants | Outside this implementation scope. Existing duplication between legacy Field Ops payroll and Booking Finance payroll should be treated as an integration risk, not redesigned during department work. | **Implemented, preserve.** Finance and salary/payroll controllers, entities, access grants, idempotency checks and audit records exist. | **Implemented.** Owner Finance/salary pages and Finance department pages call real APIs. | Consumes verified attendance; approved leave is represented in attendance summaries, but current manager UI sends zero leave totals instead of server-derived values. Other departments do not yet post operational charges/costs. | Booking/Finance tests are included in 40/40 pass. Finance security audit document exists. |

## Shared platform status

### Identity and RBAC

- Roles are `Owner`, `Admin`, `Manager`, `Receptionist`, `Housekeeper`, `Maintenance`, `Chef`, `Waiter`, and `Security`.
- Department Admin and Manager constraints are implemented in Identity, including department ownership checks and tests.
- `RolePermissions.GetPermissionsForRole` recognizes only generic `admin`, `manager`, and `employee`; it does not map the concrete operational enum values. This permission list must not be used as authoritative department authorization until corrected.
- Owner has dedicated oversight pages. Admin and Manager share the `/dashboard` shell, with navigation derived from role and department claims. Operational staff land on the generic task page.
- The gateway authenticates broad route families but generally leaves department authorization to services. This is appropriate only where every downstream mutation performs its own department check.

### Attendance and leave

- Employee punch/history, department records, overtime decisions, missing-punch flags, leave policies, leave requests, manager decisions, audit records, and versioned attendance summaries exist.
- Task, attendance, leave, and summary controllers enforce department claims more consistently than the .NET operational services.
- The manager attendance page builds summaries with `approvedPaidLeaveDays: 0`, `approvedUnpaidLeaveDays: 0`, and an empty leave-record list. The backend accepts those client totals. Approved leave therefore is not yet an authoritative payroll input end to end.
- Field Ops still contains a legacy hourly-rate payroll service while the implemented Finance flow calculates salaries from Owner-approved structures. New department work must not revive or extend the legacy calculation path.

### Frontend and UI

- The shared dashboard layout, sidebar, typography, dark green/cream/orange palette, cards, tables, forms and React Query conventions are established and should be reused.
- Pages call real service clients; the main gap is department-specific workflow depth and backend authorization, not a lack of basic dashboard scaffolding.
- Route visibility is a usability control only. Backend services must continue to make every authorization decision.

### Cross-service consistency risks

1. Hotel Ops `PATCH /api/v1/rooms/{id}/status` accepts any authenticated caller and does not enforce department/role transition authority.
2. Booking check-in/out endpoints are authenticated but do not express Front Office department restrictions at the controller boundary.
3. Housekeeping completion does not update authoritative room readiness.
4. Maintenance work orders do not take rooms out of inventory or gate return-to-service.
5. KDS authorization names do not match Identity roles and KDS does not validate the Food & Beverage department claim.
6. Food & Beverage charges and Maintenance costs do not create idempotent Finance records.
7. Two department models exist (`Identity.Department` and `HotelOps.Department`). Gateway routes `/api/v1/departments` to Identity, making the Hotel Ops controller effectively shadowed and a duplication risk.
8. Database creation relies heavily on EF/JPA startup behavior; only Identity has checked-in EF migrations. Cross-service schema rollout and rollback are not uniformly documented.

## Recommended implementation stages

1. **Shared authorization and authoritative attendance input**
   - Add reusable department/role guards in Booking and Hotel Ops.
   - Restrict room transitions and Front Office reservation actions.
   - Derive leave totals and record IDs in the Attendance Summary service from approved leave records; do not accept financial inputs from the browser.
   - Add negative cross-department tests at service and gateway levels.

2. **Housekeeping completion**
   - Add inspection/approval transitions and audit records around existing housekeeping tasks.
   - Publish an idempotent room-readiness event after authorized inspection.
   - Consume it in Hotel Ops with state-machine checks, then expose a Housekeeping board using existing UI components.
   - Test checkout -> turnover task -> clean -> inspect -> available.

3. **Maintenance completion**
   - Couple maintenance issue creation to an out-of-service room transition.
   - Require authorized resolution/verification before returning a room to service.
   - Post approved actual costs to the existing Finance expense workflow with a stable source reference.
   - Add a maintenance work-order board and lifecycle tests.

4. **Front Office completion**
   - Gate check-in on authoritative room readiness and active maintenance state.
   - Add department-scoped desk queues, controlled overrides, and audit logging without duplicating booking or room entities.
   - Complete Front Office reporting and cross-department readiness tests.

5. **Food & Beverage completion**
   - Align `Chef`/`Waiter` role vocabulary and enforce the Food & Beverage department claim.
   - Complete KDS manager/chef/waiter workflows and frontend.
   - Add idempotent room/restaurant charge posting to existing Finance invoices/transactions.
   - Test order -> preparation -> delivery -> Finance charge.

Security-role-specific operational workflows should be considered only if the product owner confirms that Security is intended to become a department; the repository currently does not define it as one.

## Validation baseline

Executed on 2026-09-18:

| Check | Result |
|---|---|
| Frontend production build and TypeScript | **Passed** (`next build`, 35 static routes) |
| Frontend ESLint | **Failed**: 44 errors and 85 warnings. Existing issues include React `set-state-in-effect`, explicit `any`, unused symbols, and hook dependencies. |
| Identity unit tests | **Failed**: 49 passed, 2 failed. Both failures expect role `Customer` while runtime returns `Guest`. |
| Identity solution build/integration run | **Blocked by running process**: `SmartHotel.Identity.API` PID 14168 locked output DLLs. This user process was not stopped. |
| Hotel Ops tests | **Passed**: 34/34 (`--no-build`) |
| Booking/Finance tests | **Passed**: 40/40 (`--no-build`) |
| Notification tests | **Passed**: 19/19 (`--no-build`) |
| Gateway integration tests | **Passed**: 30/30 (`--no-build`) |
| Field Ops tests | **Passed** (`mvn test`) |
| Concierge tests | **Passed**: 22/22, 5 dependency deprecation warnings |

`--no-build` results validate the binaries already present in the working tree. The final verification stage must rerun clean release builds and tests after the running services are stopped or isolated to separate output directories.

## Phase 1 conclusion

Finance is materially implemented and should be preserved. Housekeeping and Maintenance have partial backend foundations but incomplete authoritative room-state loops and thin frontend workflows. Front Office has substantial booking UI/API coverage but insufficient department-specific backend authorization and readiness gating. Food & Beverage is the least complete: a disconnected KDS backend exists, while role alignment, frontend workflows, and Finance charge integration are missing.

The first implementation stage should be shared authorization plus server-derived leave/attendance integrity, because every subsequent department workflow depends on those boundaries.

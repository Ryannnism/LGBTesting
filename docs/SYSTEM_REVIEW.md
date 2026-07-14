# LGBServices — Full System Review & Remediation Plan

Date: 2026-07-14. Reviewed: `LGBApp.Backend` (ASP.NET Core 8, EF Core, SQLite/SQL Server), `LGBApp.Frontend` (React 18 + Vite + TS), tests, CI, Docker/Railway/Vercel deploy configs.

This document is written as an **execution plan for an implementing model**. Each finding has an ID, exact file references, a precise fix specification, and acceptance criteria. Follow the ordering in §10. Do not invent scope beyond what a finding specifies.

---

## 0. System overview (context for the implementer)

- **Backend**: `LGBApp.Backend/` — controllers in `Controllers/`, domain logic in static service classes in `Services/`, EF entities in `Models/`, DbContext + seeders in `Data/`. Auth is JWT (7-day expiry) with role strings: `Admin`, `User` (internal staff), `ClientAdmin`, `ClientSignatory`. Authorization is mostly imperative via `Services/AuthHelper.cs`.
- **Core domain**: Customers → CustomerPackages → JobRequests → JobRequestUnits (one per qty "session"). MOI forms (client instruction) and MOA forms (minutes/resolution) attach to jobs/units and move through workflow states (`MoiWorkflowStates`, `JobHandoffStatuses` in `Services/JobHandoffService.cs`).
- **Persistence**: Production (Railway Docker) runs **SQLite** at `/data/lgbapp.db` using `EnsureCreated()` + a hand-rolled `Data/SqliteSchemaMigrator.cs`. SQL Server path uses real EF migrations. Two schema sources exist in parallel.
- **Frontend**: no router; a single 1,891-line `src/App.tsx` holds nearly all state and switches views by a `Tab` union. API layer is `src/lib/api.ts` (fetch wrapper, JWT in `localStorage`).
- **Tests**: ~30 xUnit tests on workflow helpers only. Zero controller/integration/frontend tests. CI (`.github/workflows/ci.yml`) runs tests + a Python workflow backtest on PRs.

---

## 1. CRITICAL bugs (fix first, in this order)

### C1. Any user with job access can overwrite a whole JobRequest, including marking it Completed
- **Where**: `Controllers/JobRequestsController.cs:611-648` (`UpdateJobRequest`), `Services/JobRequestMapper.cs:35-61` (`ApplyRequest`).
- **Problem**: `PUT /api/jobrequests/{id}` requires only `AuthHelper.CanAccessJob`, which is true for client signatories tied to the job and any assigned internal staff. `ApplyRequest` then blindly writes `Customer`, `Service`, `TotalQty`, `UsedQty`, `Status`, dates, `AccountHolder` from the request — bypassing the entire MOI/MOA workflow. A signatory can set `Status = "Completed"` and a `CompletedService` row is inserted.
- **Fix spec**:
  1. In `UpdateJobRequest`, restrict full-field updates to `AuthHelper.IsAdmin(User)`. Non-admins get `403`.
  2. In `ApplyRequest`, validate `request.Status` against an allowlist `{"Pending","In Progress","Completed","Canceled"}`; return `400` from the controller for anything else.
  3. Reject `TotalQty < 1` and `TotalQty < UsedQty` with `400`.
- **Accept when**: an authenticated ClientSignatory calling `PUT /api/jobrequests/{id}` gets 403; Admin update still works; new xUnit test covers both.

### C2. Production deploys run with seeded default credentials (`password123`) and known JWT placeholder
- **Where**: `Data/InternalStaffSeeder.cs` (5 accounts, password `password123`), `Program.cs:151-215` (Sqlite branch runs `InternalStaffSeeder.Seed` unconditionally — Docker image sets `Database__Provider=Sqlite`, `ASPNETCORE_ENVIRONMENT=Production`), `appsettings.json:6` (dev JWT key committed), `appsettings.Production.json:9` (placeholder key — `Program.cs:60` only throws when the key is **null**, not when it is the placeholder).
- **Problem**: anyone reading the public repo can log in as `sharon@lgb.test / password123` (Admin) on a fresh deploy, and if the placeholder JWT key ships, can forge Admin tokens outright. `MustChangePassword` mitigates the password only until first login, not token forgery.
- **Fix spec**:
  1. In `Program.cs`, after reading `jwtKey`, throw `InvalidOperationException` if it equals either committed placeholder string or is shorter than 32 chars.
  2. Gate `InternalStaffSeeder.Seed` on `app.Environment.IsDevelopment() || Environment.GetEnvironmentVariable("SEED_STAFF") == "true"`; in non-dev seeding, read the initial password from env `SEED_STAFF_PASSWORD` and throw if unset.
  3. Add basic rate limiting on `/api/auth/login` and `/api/auth/forgot-password` using `Microsoft.AspNetCore.RateLimiting` (fixed window, e.g. 10/min/IP).
- **Accept when**: app refuses to boot in Production with a placeholder/short JWT key; no default-password accounts are created unless explicitly enabled.

### C3. Uploaded documents are lost on every production redeploy
- **Where**: `Dockerfile` (`RUN mkdir -p /data /app/uploads`; Railway volume is mounted at `/data` only), `Services/JobItemDocumentStorage.cs:5-12` (default root = `ContentRootPath/uploads/job-items`, override env `LGB_UPLOAD_ROOT`).
- **Problem**: DB survives in `/data`, but files go to `/app/uploads` inside the ephemeral container layer. Every deploy/restart deletes all MOI/MOA/supporting documents while their DB rows remain → downloads 404 ("File not found on disk").
- **Fix spec**: set `ENV LGB_UPLOAD_ROOT=/data/uploads` in the Dockerfile (and document it in `docs/DEPLOYMENT.md`). Optional follow-up is finding E7 (S3 adapter).
- **Accept when**: uploads land under `/data/uploads` in the container.

### C4. MOI can be approved from any state, skipping recommendation and client sign-off
- **Where**: `Controllers/MOIFormsController.cs:421-433` (`ApproveMoi`) → `Services/JobHandoffService.cs:258` (`OnMoiApprovedAsync`) — no `WorkflowState` precondition anywhere on this path. Compare with `SubmitForApproval` which does check state.
- **Fix spec**: at the top of `ApproveMoi`, return `400` unless `form.WorkflowState` is one of the states that legitimately awaits Sharon's sign-off (per `MoiWorkflowStates` ordering used in `JobHandoffService`/`PackageItemStatusResolver` — expected: `Recommended` or `PendingMoiApproval`; confirm against `OnMoiRecommendedAsync` output before coding). Keep `admin-override` as the deliberate escape hatch (it already exists at line 435).
- **Accept when**: approving a `Draft` MOI returns 400; the normal recommend→approve flow still passes the CI workflow backtest (`scripts/workflow_backtest.py`).

### C5. Invoice number generation collides under concurrency and after growth past a day
- **Where**: `Controllers/InvoicesController.cs:71-76` — `count = Invoices.CountAsync(); InvoiceNumber = $"INV-{date}-{count+1:D4}"` with a **unique index** on `InvoiceNumber` (`Data/AppDbContext.cs:345`).
- **Problem**: two concurrent creates compute the same count → second `SaveChangesAsync` throws `DbUpdateException` → unhandled 500. Count also never resets per day, and deleting rows can reuse numbers.
- **Fix spec**: generate `INV-{yyyyMMdd}-{suffix}` where suffix = (max existing suffix for today's prefix) + 1, computed inside a retry loop that catches `DbUpdateException` on unique violation and retries up to 3 times.
- **Accept when**: two parallel POSTs both succeed with distinct numbers (test with `Task.WhenAll` against the SQLite test factory).

### C6. Multi-company signatories see the wrong job set
- **Where**: `Controllers/JobRequestsController.cs:56-62` filters external users by `AuthHelper.CurrentCustomerId` only, while the token also carries `accessible_customer_ids` (`Services/JwtTokenService.cs:34-39`) and `AuthHelper.GetAccessibleCustomerIds` exists precisely for this (`Services/AuthHelper.cs:44-59`).
- **Problem**: a ClientSignatory granted access to several companies (via `SignatoryCustomerAccess`) only ever sees jobs of their primary `customer_id`; if `CustomerId` is null they see nothing.
- **Fix spec**: for external users replace the filter with `var ids = AuthHelper.GetAccessibleCustomerIds(User); jobs = jobs.Where(j => j.CustomerId.HasValue && ids.Contains(j.CustomerId.Value))`. Also push this filter into the DB query (see EF1) rather than filtering the fully-loaded list.
- **Accept when**: a signatory with two `SignatoryCustomerAccess` rows receives jobs from both customers.

---

## 2. Small bugs

### S1. Case-inconsistent email uniqueness
- `Controllers/AuthController.cs:38`, `Controllers/UsersController.cs:101,161` check duplicates with case-sensitive `==`, but login matches case-insensitively (`AuthController.cs:70`). `Foo@x.com` + `foo@x.com` can both exist; login then resolves arbitrarily.
- **Fix**: normalize emails to lower+trim on write (`Register`, `CreateUser`, `UpdateUser`, seeders) and compare with `ToLower()` on duplicate checks. Add a one-off startup/SQL cleanup note, not code, for existing dupes.

### S2. `CompletedServices` visibility filter never matches multi-assignee jobs
- `Controllers/CompletedServicesController.cs:39-40` compares `JobAssignedTo.ToLower() == userName` but `JobAssignedTo` is a comma-joined name list (`Services/JobRequestUnitService.cs:130`). Staff on shared jobs see an empty history.
- **Fix**: use `(", " + s.JobAssignedTo + ", ").ToLower().Contains(", " + userName.ToLower() + ", ")` pattern or fetch-then-filter in memory on split names (result sets are small).

### S3. Wrong row deleted when reverting a completed job
- `Services/JobRequestUnitService.cs:166-178` (`RemoveLatestCompletedServiceRecordAsync`) matches on Customer+Service+AccountHolder strings and deletes the newest match — with two same-named jobs it deletes the wrong record. Also `Controllers/JobRequestsController.cs:625-644` can insert duplicate `CompletedService` rows if status is toggled Completed→Pending→Completed via PUT.
- **Fix**: add nullable `JobRequestId` column to `CompletedService` (+ `SqliteSchemaMigrator.EnsureColumn` + EF migration), stamp it at insert points (`JobRequestsController.cs:571,631`), and delete by `JobRequestId` ordered by `CreatedAt`. Guard the PUT path: skip insert when a non-reverted `CompletedService` for that `JobRequestId` already exists.

### S4. Culture-dependent date parsing
- `Services/JobRequestMapper.cs:47,51`, `Controllers/JobRequestsController.cs:351` use bare `DateTime.TryParse` (server culture decides day/month order).
- **Fix**: parse with `CultureInfo.InvariantCulture` and explicit formats (`yyyy-MM-dd` first); mirror what `Services/DateOnlyHelper.cs` does — reuse it where the value is date-only.

### S5. MOA/MOI resolution silently falls back to the wrong form
- `Controllers/JobRequestsController.cs:716-734` (`ResolveMoaFormForHandoffAsync`): when `unitNumber` is provided but the unit doesn't exist, it falls through to "any MOA on the job", so an approval can land on another session's form.
- **Fix**: if `unitNumber.HasValue && job.TotalQty > 1` and the unit lookup fails, return `null` (caller already 400s on null).

### S6. Unauthenticated external users can create MOI forms for arbitrary companies
- `Controllers/MOIFormsController.cs:81-156`: when `request.JobId` is null there is **no access check at all** — any authenticated user may create a floating MOI naming any `Company` string.
- **Fix**: when `JobId` is null, require `AuthHelper.IsInternalStaff(User)` or (for external users) resolve the customer from `request.Company` and require `AuthHelper.CanAccessCustomer(User, customer.CustomerId)`.

### S7. `Recommend` allows recommending from `Draft`
- `Controllers/MOIFormsController.cs:384-419` only rejects `Approved`. Add a positive state check: allow only `PendingRecommendation` (and `PendingPrep` if the backtest requires it — verify against `scripts/workflow_backtest.py`).

### S8. `AuthHelper.CanAccessJob(user, assignedUserId, jobAssignedTo)` short-circuits the name fallback
- `Services/AuthHelper.cs:107-126`: when both `userId` and `assignedUserId` have values but differ, it returns false without checking the comma-list `jobAssignedTo` — inconsistent with `IsAssignedToJob`'s intent. Low impact (legacy path) but make the mismatch fall through to the name check instead of returning.

### S9. Frontend dead/risky code
- `src/lib/supabase.ts` creates a Supabase client that **no file imports**; `@supabase/supabase-js` is a dependency. Delete both (file + package.json entry).
- `src/App.tsx` has ~8 bare `catch {}` blocks (lines ~186-233) that silently swallow load failures — surface them via the existing toast state.

### S10. `GetJobRequests` leaks 404 semantics
- `Controllers/JobRequestsController.cs:41-48`: unknown `customerPackageId` returns 404 for the whole collection; return an empty list or 400 instead (frontend treats 404 as an error toast).

---

## 3. Validation gaps (input-level)

| ID | Where | Gap | Fix |
|----|-------|-----|-----|
| V1 | `Controllers/UsersController.cs:96` (`CreateUser`) | No password length/strength check (empty password accepted); no email format check | Enforce ≥6 chars (match `AuthController`) + `[EmailAddress]`-style regex; return 400 |
| V2 | DTOs in `Models/DTOs/` | Almost no data annotations; controllers rely on ad-hoc checks | Add `[Required]`, `[MaxLength]` mirroring `AppDbContext` maxlengths on Create/Update DTOs (users, customers, job requests, invoices, forms) |
| V3 | `Controllers/JobItemDocumentsController.cs:75-126` | No extension/content-type allowlist on upload; stored `ContentType` is client-controlled | Allowlist: pdf, png, jpg, jpeg, docx, xlsx, msg, eml; infer content type server-side from extension; reject others with 400 |
| V4 | `Controllers/InvoicesController.cs:56` | `Amount` unvalidated (negative/zero allowed) | Require `Amount > 0`, `Currency` length ≤ 10 |
| V5 | `Services/JobRequestMapper.cs` | `TotalQty`/`UsedQty` unbounded (covered in C1.3) | See C1 |
| V6 | `Controllers/AuthController.cs:110` + `PasswordResetService.cs:111` | Password min length duplicated as magic number 6 | Extract `PasswordPolicy.MinLength` constant used by all four sites (change-password, reset, create-user, register) |

## 4. Verification / authorization gaps (business-rule level)

| ID | Where | Gap | Fix |
|----|-------|-----|-----|
| A1 | C1/C4/S6/S7 above | State-machine transitions not verified server-side | Covered above |
| A2 | `Controllers/JobRequestsController.cs:363-484` (`handoff`) | `start-prep`/`start-reso` let **any assigned internal staff** move status with no check that the current handoff state precedes the target | Add precondition: `start-prep` only from `ClientSubmitted`/empty; `start-reso` only from `PendingPrep`. Use `JobHandoffStatuses.Ordered` to compare |
| A3 | `Controllers/CompletedServicesController.cs` | External users (ClientAdmin/Signatory) hit the name-match branch and can search all records via the `search` param before filtering — verify the filter composes (it does — `Where` before search is AND) but customer scoping by name is fragile | After S3 adds `JobRequestId`, scope external users by `CustomerId` join instead of name |
| A4 | `Middleware/MustChangePasswordMiddleware.cs` | Path allowlist string-prefix match (`StartsWith`) means `/api/auth/change-password-anything` passes | Compare exact path segments |
| A5 | JWT lifetime | 7-day tokens, no revocation: deleted/demoted users keep full access up to 7 days (`Controllers/UsersController.cs:193` delete doesn't invalidate tokens) | Short-term: reduce to 24h. Extension E5 covers refresh tokens properly |

---

## 5. Error handling — current state and target

**Current**: no `app.UseExceptionHandler` / ProblemDetails in `Program.cs`; unhandled exceptions surface as bare 500s (frontend `formatApiError` in `src/lib/api.ts:541-568` special-cases raw stack traces, which confirms they leak in dev). Controllers mix error shapes: `BadRequest("string")`, `BadRequest(new { message })`, `ConflictObjectResult`, silent `catch (Exception)` in `AuthController.cs:94`. Domain services throw `InvalidOperationException` and controllers selectively catch (`JobRequestsController.cs:241-252`), including a **message-text match** (`AuthController.cs:90` — `ex.Message.Contains("wait a minute")`).

**Target spec** (implement as one PR):
1. Add `builder.Services.AddProblemDetails()` and `app.UseExceptionHandler()` returning RFC7807 JSON with a correlation id; log the exception via `ILogger`.
2. Create `Services/DomainException.cs` (message + optional `StatusCode`); replace the `InvalidOperationException` throws in `PasswordResetService`, `JobRequestAssignmentService` with it; map to 400/429 in the exception handler. Delete the message-text catch in `AuthController`.
3. Standardize every controller error body to `{ message: string, errors?: string[] }` — the frontend already parses exactly this (`formatApiError`). Grep for `BadRequest("` and wrap plain strings.
4. Frontend: add a top-level React error boundary component wrapping the App shell; route the silent `catch {}` blocks (S9) into the toast.

---

## 6. Database structure findings

1. **Dual schema pipelines (highest risk)**: SQLite = `EnsureCreated` + `SqliteSchemaMigrator` (473 lines of raw DDL, `EnsureColumn` calls); SQL Server = EF migrations (`Migrations/`, last one 2026-06-09 — already missing later columns like `JobRequests.InternalHandoffStatus` unless snapshot was updated). **Plan**: pick EF migrations for both providers; generate a SQLite migration set, make startup run `Database.Migrate()` in both branches, and retire `SqliteSchemaMigrator` after one release where it and migrations coexist idempotently. This is a prerequisite for adding any new column cleanly.
2. **String-based relationships**: `MOIForm.Company` → customer resolved by company-name string (`WorkflowService.ResolveCustomerForCompanyAsync`); `JobRequest.Customer`, `AccountHolder`, `JobAssignedTo` are denormalized names; `CompletedService` has no FK at all. Renaming a company or holder silently orphans forms. **Plan**: add nullable `CustomerId` to `MOIForm`/`MOAForm`, backfill from company match at startup (one-off), prefer the FK in `ResolveCustomerForCompanyAsync`, keep the string as display fallback. `CompletedService.JobRequestId` is S3.
3. **JSON-blob columns** (`FormDataJson`, `PricingJson`, `InvoiceByPartyIdsJson`, `MoiJson`, approval records) are acceptable for this app's scale, but add a `SchemaVersion` int to `MOIForm`/`MOAForm` now so future shape changes are migratable.
4. **Missing indexes**: `JobRequests(CustomerId)`, `JobRequests(CustomerPackageId)`, `JobRequests(Status)`, `MOIForms(JobRequestId)`, `MOAForms(JobRequestId)`, `JobItemDocuments(JobRequestId)`, `CompletedServices(DateCompleted)`. EF creates FK indexes automatically for real FKs — MOI/MOA/JobRequest FKs exist so verify in the snapshot; add the `Status`/`DateCompleted` ones explicitly.
5. **`Invoice` has no FK constraints** to Customer/JobRequest (plain int columns, only checked in controller). Add real FKs with `SetNull` for `JobRequestId`, `Restrict` for `CustomerId`.
6. **Cascade review**: `Customer` delete cascades packages/holders and SetNulls jobs — deleting a customer strands jobs with a stale `Customer` name and live forms. Either soft-delete customers (add `IsDeleted`) or block delete when jobs exist. Recommend: block with 409 listing job count.

---

## 7. Runtime & deployment risks

1. **C2/C3 above** (seeded creds, ephemeral uploads) are the two production-killers.
2. **SQLite + EF per-request writes with no transactions**: flows like `CustomersController.UpdateCustomer` (`Controllers/CustomersController.cs:86-133`) run 4+ `SaveChangesAsync` calls; a crash mid-way leaves signatories/jobs half-synced. Wrap each multi-step controller action in `await using var tx = await _context.Database.BeginTransactionAsync(); ... await tx.CommitAsync();` — start with CustomersController (create/update), JobRequestsController (`assign`, `progress`, `handoff`), MOI/MOA approve paths.
3. **Startup does heavy synchronous seeding** (`Program.cs:151-223`, `GetAwaiter().GetResult()` chains) before binding the port; Railway healthcheck allows 120s but `SEED_FULL=true` boots can exceed it. Move the `SEED_FULL` block behind the same CLI-command pattern as `reset-dev-db` (run once manually), keep boot seeding to staff+catalog.
4. **CORS**: `Program.cs:16` falls back to `AllowAnyOrigin` in production whenever `Cors:AllowedOrigins` is empty — fail closed instead: throw or log-and-deny when non-dev and no origins configured.
5. **Delete-and-recreate of AccountHolders on every customer update** (`CustomersController.cs:101-119`) churns IDs that the frontend and dedup tooling reference; replace with diff-by-Id upsert.
6. **JWT in `localStorage`** (`src/lib/api.ts`) — acceptable trade-off for this app, but document it; XSS anywhere = token theft. The upload allowlist (V3) plus `Content-Disposition: attachment` on downloads (already set via `PhysicalFile` with filename) keeps the API origin XSS-clean.

---

## 8. Efficiency findings

| ID | Where | Problem | Fix |
|----|-------|---------|-----|
| EF1 | `Controllers/JobRequestsController.cs:31-79` | Loads **every** job with Units→Assignees→User, then filters per-user in memory | Apply customer/status filters in the query (`query.Where(...)` before `ToListAsync`); for external users filter by accessible ids in SQL (see C6) |
| EF2 | `Controllers/MOIFormsController.cs:60-67` | N+1: `ResolveCustomerForCompanyAsync` per form | Batch: collect distinct companies, one query, dictionary lookup |
| EF3 | `Controllers/CustomersController.cs:22-42` + `GetCompletedServices` | Unpaginated full-table reads | Add `page`/`pageSize` (default 50, cap 200) query params; frontend tables already client-side paginate so wire through incrementally |
| EF4 | `Services/JobRequestUnitService.cs:111-121` (`RefreshJobAggregateAsync`) | Re-queries assignees per unit inside a loop (`SyncUnitAssigneeFieldsAsync` each) | It already includes Assignees→User in the outer query; make `SyncUnitAssigneeFields` use the loaded collection instead of re-querying |
| EF5 | `Controllers/JobRequestsController.cs` various | Pattern `SaveChanges` → immediately re-fetch `JobQuery().First` (3 round trips per action) | After C-fixes land, return the tracked entity (it's already up to date) instead of re-querying; low priority |
| EF6 | `src/App.tsx` | God component: every tab change re-renders all state; 8 parallel fetches on mount regardless of role | Extension E1 (routing/split) covers this; don't micro-optimize before then |

---

## 9. Modular extensions (build after fixes; each is independently shippable)

- **E1. Frontend routing & decomposition**: introduce `react-router-dom`, split `App.tsx` into route-level pages (Dashboard, Customers, Jobs, Forms, Portal, Admin) with a shared auth context; move data fetching into per-page hooks (`src/hooks/`). Success: `App.tsx` < 300 lines, deep links work, no behavior change.
- **E2. Real invoice PDF**: `InvoicesController.DownloadPdf` currently emits a `.txt`. Add QuestPDF (MIT for small business) generating a branded invoice; extend `Invoice` with line items table (`InvoiceLine`: description, qty, unitPrice) once §6.1 migration consolidation is done.
- **E3. Audit log**: append-only `AuditEvent` table (actor, action, entityType, entityId, beforeJson, afterJson, at). Write from the state-transition points in `JobHandoffService` and the admin CRUD controllers. Read-only Admin UI tab.
- **E4. Email notification digests**: `WorkflowNotifier` already emails per event; add per-user notification preferences + a daily digest option (background `IHostedService` timer, group unsent `AppNotification` rows).
- **E5. Refresh tokens / session management**: 30-min access tokens + rotating refresh tokens in a `RefreshToken` table; revoke on user delete/role change (closes A5). Frontend: transparent refresh in the `request` wrapper on 401.
- **E6. Reporting module**: monthly completed-services and package-utilization exports (CSV first, charts later) — data already exists in `CompletedServices` + `CustomerPackages`.
- **E7. Pluggable document storage**: extract `IDocumentStorage` interface from `JobItemDocumentStorage` (local disk impl now, S3/R2 impl later). Prereq: C3.
- **E8. Client-facing schedule/calendar**: `PackageScheduleItems` already sync from units; expose an ICS feed endpoint per customer (`/api/clientportal/schedule.ics`, token-in-query with a dedicated signed key).

---

## 10. Execution order for the implementing model

1. **Wave 1 — security/data-loss (C1→C6)**, each as its own commit with a test. Nothing else in these commits.
2. **Wave 2 — §5 error-handling PR** (ProblemDetails + DomainException + frontend boundary).
3. **Wave 3 — small bugs S1–S10 and validation V1–V6** (small commits, one finding each).
4. **Wave 4 — §6.1 migration consolidation** (its own PR; verify both providers boot + all tests + backtest).
5. **Wave 5 — remaining DB items (§6.2–6.6), runtime items (§7.2–7.5), efficiency EF1–EF4.**
6. **Wave 6 — extensions E1+, in the order the owner picks.**

### Guardrails (read before every change)
- Run `dotnet test LGBApp.Backend.Tests` after every commit; run `python3 scripts/workflow_backtest.py` against a locally running API after any change touching `JobHandoffService`, `MOIFormsController`, `MOAFormsController`, or `JobRequestsController`.
- Never edit files under `out/`, `obj/`, `bin/`, or the committed `Migrations/*Designer.cs` by hand.
- Any new column must be added in **both** `SqliteSchemaMigrator.EnsureColumn` and an EF migration until Wave 4 lands.
- Preserve the existing JSON error shape `{ message, errors? }` — `src/lib/api.ts formatApiError` depends on it.
- Status strings (`"Pending"`, `"In Progress"`, `"Completed"`, handoff statuses, MOI workflow states) are compared literally across backend and frontend (`src/lib/packageItemStatus.ts`); never rename them.
- When a fix spec here conflicts with observed behavior of `scripts/workflow_backtest.py`, the backtest wins — adjust the state allowlist, not the workflow.

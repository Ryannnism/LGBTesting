# LGBServices — System Review #3: D1 Feature Audit, Deeper Edge Cases, and Two Fixes Applied

Date: 2026-07-15. Method: same as Review #2 — the backend was booted against scratch SQLite DBs and driven end-to-end through the real HTTP API as four concurrent role sessions (Admin/Sharon, internal staff/Nita, ClientAdmin, ClientSignatory Alice+Bob) plus a second tenant (Globex/Carl). This round focused on the **new D1 "AdminBypass" workflow-mode feature**, the durability of Review #2's N1–N4 fixes, and data-integrity edges around concurrency and timezones.

**Unlike the previous two reviews, I applied two fixes directly this round** (both verified live, all 73 unit tests green). They are documented in §1 as *done*. Everything else is findings for the implementing model.

---

## 0. Context: what moved since Review #2

`git log` shows Review #2 was implemented:
```
adec1d9 Implement Review #2 design decisions D1, D3, and D4.
a2aef77 Fix Review #2 live simulation findings N1–N4.
```
Verified live this round:
- **N1** (revert leaves handoff stuck) — fixed; couldn't reproduce.
- **D3** (signature required for sign-off) — enforced: MOI `client-approve` without a signature now returns 400 "A signature is required."
- **D1** (workflow choice) — new `AdminBypass` mode shipped: clients pick MOI/MOA *or* send Sharon a note. This closed the old "client closes Service jobs with no governance" gap — MoiMoa tasks can no longer be client-closed before the workflow completes, and AdminBypass tasks can only be closed by Sharon.
- Test count: 62 → **73**.

---

## 1. Fixes applied this round (DONE — verified live)

### F1 (was Critical) — Optimistic-concurrency guard rejected every legitimate save on non-UTC servers
- **Symptom I reproduced**: the first-ever save on a brand-new MOA, passing back the exact `updatedAt` the GET returned, got **HTTP 409** "updated by someone else." A *blind* save (no `expectedUpdatedAt`) got 200 but ran with no protection. Two parallel saves both got 409. The MOI `PUT` path failed identically. The **frontend does send `expectedUpdatedAt`** (`src/lib/api.ts:1111,1217,1231,1240`), so this broke all collaborative form saves.
- **Root cause**: `Services/FormConcurrencyHelper.cs` compared `storedUtc.ToUniversalTime()` (from `form.UpdatedAt`) against the client timestamp. SQLite/EF returns `DateTime` with `Kind=Unspecified`; calling `ToUniversalTime()` on an Unspecified value shifts it by the **server's local offset**. The test host is UTC+8, so every comparison was off by 8 hours → guaranteed false conflict. It only "worked" on UTC hosts (like a default CI/Railway container) by luck — **this is exactly the kind of environment-dependent bug that hides in CI and breaks in production.**
- **Fix applied**: normalize `storedUtc` symmetrically — treat `Kind=Unspecified` as UTC via `DateTime.SpecifyKind(..., Utc)` instead of `ToUniversalTime()`, mirroring the expected-side handling already in the same method.
- **Verified**: single save with token → **200** (was 409); genuine race → **one 200, one 409** (correct optimistic concurrency preserved). This fix is directly relevant to the Postgres migration — see the DateTime section of `POSTGRES_MIGRATION_GUIDE.md`.

### F2 (was Medium) — AdminBypass requests were invisible in Sharon's work list
- **Symptom I reproduced**: a client choosing AdminBypass fired a notification to Sharon, but the job did **not** appear in her main job list (`GET /api/jobrequests`). She could only reach it by clicking the notification's deep-link. If she missed/dismissed the notification, the request silently stalled — with no MOI/MOA, nothing else surfaces it.
- **Root cause**: `Services/InternalWorkVisibilityHelper.cs` `PostIntakeUnitHandoffs` (the "released to internal" set) did not include `AdminBypass`, so the client-release gate filtered these jobs out of internal views.
- **Fix applied**: added `JobHandoffStatuses.AdminBypass` to `PostIntakeUnitHandoffs`.
- **Verified**: after the fix, Sharon sees the AdminBypass job in her list (handoff `AdminBypass`) and can complete it. Unassigned prep staff (Nita) still don't see it (correct — it's an admin action).

---

## 2. Remaining findings (build these)

### R1 — Stale AdminBypass notification/state after a mode reversal (Low)
- **Repro**: Alice sets job to AdminBypass (notification sent to Sharon). The client cannot self-revert (`issue-moi` returns 400 "Contact your LGB admin"). If an **admin** later flips the unit back to MoiMoa via `workflow-choice`, the unit's `AdminBypassNote/At/ByUserId` are cleared (`ClientJobsController.cs:127-137`) but the previously-sent `AppNotification` (eventType `admin_bypass`) is **not** marked read/deleted. Sharon keeps a dangling "client wants bypass" alert for work that is now MOI/MOA.
- **Fix spec**: when `workflow-choice` switches a unit/job from AdminBypass to MoiMoa, mark any unread `admin_bypass` notifications for that `JobRequestId` as read (or delete them). Do it in the MoiMoa branch of `ChooseWorkflow`.
- **Accept when**: flipping bypass→MoiMoa clears the matching bypass notification.

### R2 — Cross-tenant form endpoints still check state before authorization (Low, carried from R#2 N4 — partially open)
- **Repro**: Carl (Globex) calling `client-approve` on an Acme MOA returned 400 (phase message), not 404, when the form wasn't in a sign-off phase — the handoff-state check runs before `CanAccessCustomer`. No data leaks, but it's an existence/phase oracle for another tenant's forms.
- **Note**: Review #2 flagged this as N4; verify whether the fix was applied to *all four* form actions (`client-approve`, `client-reject`, `submit-for-approval`, `recommend`) on both MOI and MOA controllers. In this round the MOA `client-approve` path still ordered state before tenant check. **Fix spec**: move the `CanAccessCustomer` check to immediately after the form load; return 404 for forms the caller can't access.

### R3 — AdminBypass completion writes a `CompletedService` with empty `JobAssignedTo` (Low / reporting)
- When Sharon closes an AdminBypass job, the `CompletedService` row has `JobAssignedTo = ""` (no prep staff were ever assigned). Completed-services filtering for non-admin staff is by name-match on `JobAssignedTo`, so these rows are admin-only in history. Probably fine, but confirm AdminBypass work should be attributed to Sharon (set `JobAssignedTo = actor name`) so it appears in her completed view and in reporting.

---

## 3. Things re-verified SOLID this round (do not touch)

- **D1 guardrails**: note length validated (≥8 chars, whitespace-only rejected); a non-`NeedsMoi` signatory (Bob) is correctly forbidden from `workflow-choice`; MoiMoa tasks cannot be client-closed pre-workflow; AdminBypass tasks reject client completion ("Sharon will mark it complete").
- **D3**: signature genuinely required on MOI client sign-off.
- **Cross-tenant isolation**: Carl (Globex) blocked from Acme jobs/forms/docs; Acme ClientAdmin blocked from `/api/customers/{globexId}`. Every role sees only its own company.
- **Multi-session partial release**, **double-approval guards**, **intake authorization**, **user-management escalation blocks**, **OTP enumeration-safety + cooldown** — all still correct.

---

## 4. Guardrails for the implementing model
- Run `dotnet test LGBApp.Backend.Tests` after each commit (must stay at 73+ green).
- The local `scripts/workflow_backtest.py` requires seeded staff whose passwords are still `password123` **and** `MustChangePassword=false`; in a fresh dev DB the seed sets `MustChangePassword=true`, so the backtest 403s at login unless you first run `dotnet run -- reset-dev-password sharon@lgb.test` (or seed with reset). This is a harness detail, not a bug — don't "fix" the middleware for it.
- Never rename status/handoff/workflow-mode string literals; they're compared verbatim across backend and `src/lib/packageItemStatus.ts`.
- New columns go in both an EF migration and `SqliteSchemaMigrator` until the hand migrator is retired.

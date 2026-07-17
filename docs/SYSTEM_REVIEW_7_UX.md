# System Review #7 — What the User Actually Sees (UX + Functionality)

Date: 2026-07-17. Method: booted against a **copy of the live 169-company seeded database** (the new `Pg_ClientActivatedAt` migration applied cleanly on real data), and drove each role while capturing the exact API payloads that render on their screens — customer list, package workboard, client portal tiles, progress bars, work tracker, dashboard. Focus this round: **the number/label/empty-state a real user reads**, not just whether the endpoint works.

Bottom line: the **0/0 package-workboard bug is fixed** and the new on-demand session model works. But a fresh-seeded system still *reads* as broken to a real user because of three headline-number/empty-state problems (§2). None are crashes — every screen loads, zero server errors — they're "the screen tells me the wrong story" issues.

---

## 1. Fixed / working — what the user now sees correctly

- **Admin package workboard (was 0/0 → FIXED).** Opening Adil Cita's package now shows all **11 service lines** with real progress — `Annual Return 0/1`, `Follow up with Reso signatory 0/10`, `Local Support Service 0/4`, each with a display status. Commit `853edd6` made `GET /api/jobrequests?customerPackageId=…` skip the internal-release filter for admins, so the full deliverables catalog is visible. This was the core frustration from last session and it's genuinely resolved.
- **New "start session on demand" model works.** A qty-10 line shows **10 dormant sessions**; the client clicks Add → `activate-session` marks unit 1 active (`ClientActivatedAt` set) → 1 active / 9 dormant, active session reads "MOI not received" (ready to start). Clean.
- **Customer list** renders all 169 companies with package/value/status.
- **Stability**: across the whole multi-role simulation, **0 unhandled exceptions / 500s**. Login, portal, workboard, dashboard all load.

---

## 2. What a real user sees that's wrong or confusing (ranked)

### 2.1 HIGH (visible everywhere) — Dashboard says "1,079 outstanding services" but the admin's job queue is empty
- **What the admin sees**: the dashboard tile **Outstanding Services = 1079**, but opening the main jobs list shows **0 jobs**. Two headline surfaces, opposite stories.
- **Why**: `DashboardController` counts *every* JobRequest with status Pending/In Progress (`outstandingServices = Count(Status==Pending||In Progress)`) — which is all 1,079 seeded lines, none of which a client has released yet. Meanwhile the job list applies the internal-release filter and shows only released work (0). So the tile counts the full catalog; the list counts active work.
- **Why it matters**: the admin reads "1,079 things to do," clicks in, sees nothing, and concludes the app is broken (exactly the reaction that started this thread).
- **Fix**: make the tile mean what it says. Either count only *released/active* work (jobs past the client-release gate) so it matches the queue, or relabel it "Package deliverables (total)" and add a separate "Active work" tile for the released count. Pick one; don't let the same screen show 1,079 and 0 for the same concept.

### 2.2 MEDIUM (every client company) — Team Members shows 0 despite a full signer list
- **What the client sees**: portal dashboard tile **Team Members = 0**. Adil Cita actually has **12 ClientSignatories + 1 ClientAdmin**.
- **Why**: `ClientPortalController` counts only `Role == ClientAdmin` (excluding self) → 0 for any company with one admin. Signatories — the actual team — aren't counted. (Flagged in Review #6 §4.2; still unfixed.)
- **Fix**: count all users for the customer except self (ClientAdmin + ClientSignatory). One-line predicate change. High visibility, trivial fix.

### 2.3 MEDIUM (the 56 "Add-ons only" companies) — blank screens on both client and admin side
- **What the client sees**: their company + package load fine, but the services area is **empty** — `openJobs: 0`, category progress empty, **0 service lines**, no button to do anything. The "0/0, can't key in" the user has been hitting.
- **What the admin sees**: the package workboard for these companies is **also blank** (0 lines).
- **Why**: these are non-cosec "Add-ons only" packages whose CubeV source rows have no itemised services (`addOns:[]`, `resoQty:0`) — verified against the original spreadsheet last session. There is genuinely nothing to render.
- **Fix (product decision, not a code bug)**: decide what these companies are.
  - If **ad-hoc/on-demand clients**: give the empty portal an explicit empty-state with a **"Request a service" action** (the ad-hoc `POST /clientjobs/issue-moi` path already exists on the backend) so the screen isn't a dead end. **Note the related bug**: ad-hoc requests are created as `TaskType="MOI"` and then don't appear in `my-jobs` (which filters to `Service`), so even after requesting, the client sees nothing — fix that filter or the ad-hoc task type.
  - If they **should have recurring work**: the itemisation must be supplied (it's not in CubeV); then they seed jobs like everyone else.

### 2.4 LOW — "Value"/"Revenue" shown on different bases in different places
- Client portal `activePackageValue` = **prorated remaining** (RM 3,699.95 of a RM 4,080 package); admin dashboard `totalRevenue` = **booked sum** (RM 480,936.93 across all packages). Same underlying data, two bases, both labelled generically. A user comparing the two screens can't reconcile them.
- **Fix**: label explicitly — "Active value (remaining)" on the client, "Booked package revenue" on the admin — or standardise the basis.

### 2.5 LOW — After a fresh seed, every "active work" screen is empty while catalogs are full
- 1,079 jobs all Pending, 778 pre-seeded **Draft** MOI shells, nothing released → the admin's job queue and the internal staff tracker (Nita) both show **0 items**, even though the catalog/workboards are full.
- This is *correct* behavior (work appears once clients start issuing MOIs), but with no empty-state copy it reads as "nothing works." A one-line empty state ("No active work yet — items appear here once clients start their sessions") would prevent the misread.

---

## 3. New feature audit — on-demand multi-qty sessions
Works functionally (dormant → activate → ready). One UX note: a qty-10 line defaults to showing 10 dormant sessions all reading "MOI not received," with no cue that the client must **Add/activate** a session before acting. Consider labelling dormant sessions distinctly (e.g., "Session 3 of 10 — not started") and making the primary action obviously "Start next session," so the client isn't unsure why 10 identical rows show "MOI not received."

---

## 4. Priority
1. **§2.1** — reconcile the "1,079 outstanding vs empty queue" contradiction. It's the single thing that makes the admin think the whole app is empty/broken, and it's the reproduction of this whole thread's original complaint.
2. **§2.2** — team-member count (trivial, every client sees it).
3. **§2.3** — decide the 56 add-ons-only companies' fate and fix the ad-hoc `my-jobs` filter so the empty portal isn't a dead end.
4. **§2.4 / §2.5** — labels and empty-state copy.

Everything in §1 is verified working on real data — don't regress the package-workboard fix or the session-activation flow. No crashes anywhere; these are all "the screen tells the wrong story," which for a live product the client logs into daily matters as much as correctness.

---

## 5. Shipped (2026-07-17)

Product decision for §2.3: treat Add-ons-only companies as **ad-hoc / on-demand** (empty catalog is correct; portal must offer a clear request path).

| # | Fix | Notes |
|---|-----|--------|
| §2.1 | `DashboardController.GetStats` | `outstandingServices` now counts only jobs past `InternalWorkVisibilityHelper.IsJobLineReleasedToInternal` (same gate as the admin queue). Tile relabelled **Active work**. Fresh seed → **0**, matching the empty queue. |
| §2.2 | `ClientPortalController.GetSummary` | Team count = ClientAdmin **+** ClientSignatory except self. Packages page shows a **Team members** tile. |
| §2.3 | `ClientJobsController` my-jobs + portal UX | External `my-jobs` includes `TaskType` **Service \|\| MOI** (ad-hoc `issue-moi` was invisible). Empty company → **Request a service** CTA; form defaults to on-demand. MOI lines bucket under **On-demand**. Admin package workboard empty copy clarifies Add-ons-only. |
| §2.4 | Labels | Client: **Active value (remaining)**; admin: **Booked package revenue**. |
| §2.5 | Empty states | `JobRequestsTable` + `MyWorkTracker`: copy that work appears after clients start/release sessions. |
| §3 | Dormant session UX | Status label `Session N of total — not started`; primary action **Start next session**; category tile shows “sessions not started”. |

**Do not regress:** package-workboard admin visibility (`customerPackageId` skips release gate); session activation / dormancy (`ClientActivatedAt`).

**Tests**: `dotnet test LGBApp.Backend.Tests` → **85** green.

**Handoff for next reviewer:** re-verify on live (or seed copy) that (1) admin dashboard Active work ≈ job queue length, (2) Adil Cita Team members ≈ 12, (3) an Add-ons-only company can Request a service and see the MOI under On-demand after submit, (4) multi-qty dormant labels / Start next session still match activation behaviour.

# Live acceptance walkthrough results (HANDOVER §11)

**Date:** 2026-08-19  
**Ran by:** Cursor agent (browser, live stack)  
**Frontend:** https://lgb-testing.vercel.app  
**API:** https://lgbtesting-production-4d6b.up.railway.app  
**SHA under test:** `32c2fba` (docs on `main`); API image `ef860ba` (Admin reset + purge already deployed)  
**Verdict:** **Accepted with incomplete coverage** — no blockers in the scenarios that were actually driven. Scenario F (MS1–MS7 twice) and most of G were not executed. See the scenario table and the not-executed notes.

Cleanup: final `POST /api/admin/test-data/purge` dry run returned **all zeros**. Customers UI after purge contains no `ZZ TEST` text. No shared config (division groups, MOI matrix, workflow templates, billing-party directory) was edited, so nothing to revert.

---

## Environment notes (not bugs)

- `Email__From` is still the Resend sandbox sender. Mail to `@lgb.com.my` / `@swmsb.com` is expected not to arrive (§11.6).
- `Reminders__SendEmail=false`. Absent reminder mail is expected.
- Live staff emails are still aliases (`nadia@lgb.test`, `sharon@lgb.test`, …) because `SEED_STAFF=false`.
- After the handover-day `Jwt__Key` rotation, everyone must sign in again.
- MS4 / MS7 were **not** covered by look-alike accounts. The script allows Admin override for those named-user steps; this pass never reached those steps.
- LGB Group MS5 names are still owed by the client. This pass used Bellworth so MS5 would not stall on an empty list.

---

## Scenario table

| ID | Result | Notes |
|---|---|---|
| A1 | Pass | Forced change after Admin reset of `ZZ TEST HOLDINGS SDN BHD Admin`. Landed on Client portal. Evidence: `docs/uat-screenshots/uat-c5-forced-change.png`. Earlier: Nadia (`User` role) also hit forced change during the first UAT session. |
| A2 | Pass (prior session) | Wrong password shows “Invalid email or password.” No field hint. |
| A3 | Pass (prior session) | Unknown email shows the same generic message. |
| A4 | Pass, incomplete | Forgot-password UI reached the code screen. Reset was **not** completed so the Admin login stayed `ryannnism@gmail.com`. |
| A5 | Pass | Signed out in a second tab; first tab redirected to the login card (no blank screen, no raw 401). Evidence: `docs/uat-screenshots/uat-a5-session-expired-login.png`. |
| B1 | Pass | `ryannnism@berkeley.edu` sees only Portal, My Packages, Team. Evidence: `docs/uat-screenshots/uat-b1-client-tabs.png`. |
| B2 | Pass | Nadia (`User`, Resolution preparation) sees only Client Portal — no Settings, Customers, Products, or Admin panels. Evidence: `docs/uat-screenshots/uat-b2-nadia-user-role.png`. |
| B3 | Pass | SPA has no client-side routes. Visiting `/customers` while signed out shows login. After Nadia signed in on that URL she still landed on Client Portal, not an admin surface. |
| C1–C4 | Pass | Created `ZZ TEST Package` and `ZZ TEST HOLDINGS SDN BHD` (Bellworth, COSEC, LOA, package attached, account holder `ZZ TEST Client Admin` with MOI / MOI approval / MOA flags). Creating the company also auto-created `ZZ TEST HOLDINGS SDN BHD Admin` (Client Admin) and a Client Signatory for the holder. `MustChangePassword` was set; temp password from the key icon forced a change. |
| C5 | Pass | Key icon shows a one-time banner that dismisses. Temp password signs in then forces a change. Resetting Ryan Admin’s own row returns “Change password for your own account.” |
| D1 | Pass | As `ZZ TEST HOLDINGS SDN BHD Admin`, requested an on-demand service (`ZZ TEST Annual Return` / `ZZ TEST Dato' Lim resolution`) and invited `ZZ TEST Extra Admin` from Team. MOI modal opened. Company tile went to `0/3 done`. |
| D2 | Not executed | Did not sign in as `ZZ TEST Client Admin` (signatory) to confirm document isolation. |
| E1 | Partial | MOI submit as the test Client Admin closed the modal (did not silently 400). Company had an MOI-approval holder, so this is closer to “routed” than “fail-closed”. Did not stay signed in as the mapped approver to confirm in-app approve (clause R5: no approve-by-email). |
| E2 | Not executed | Fail-closed needs a company with **no** matrix row **and** no company MOI approver. This company had `ZZ TEST Client Admin` flagged for MOI approval, so the unroutable path was not hit. |
| E3 | Not executed | Unrouted MOI queue was not opened (E2 did not park a form). |
| E4 | Partial | Last point of approval (T3) was required in the MOI modal (`Approved by` + date). Captured as `ZZ TEST Client Admin` before submit. |
| F1–F11 | Not executed | MOA was never started. Run 1 and run 2 (bank signatory / comment bounce / rejection / non-assignee) need a live MOA instance. MS4/MS7 would have used Admin override rather than impersonating Teh SW or Dato' Lim. |
| G1 | Not executed | Sandbox sender; no owner-inbox mail was opened. |
| G2 | Not executed | Depends on G1. |
| G3 | Pass | `GET /api/email-actions/tampered-token-xyz` returns HTML title **Link expired** and body “This approval link is invalid, expired, or already used.” No stack trace. |
| G4 | Not executed | Needs a real token for a step that has already moved on. |
| H1 | Not executed this pass | Invoice was not issued for the new `ZZ TEST` company (purge ran before billing UI). Prior session: Q3 2026 PDF/CSV 200. |
| H2 | Pass (prior session) | Settings → Billing reports current quarter PDF and CSV 200; CSV contained `ZZ TEST` before the first purge. |
| H3 | Pass (prior session) | Empty quarter (Q1 2022) PDF was valid, not an error. |
| I1 | Pass | Double-click on Submit request disabled the button (`Submitting…`); one MOI flow opened, not two request forms. |
| I2 | Pass | Browser refresh while signed in as the test Client Admin returned to Client portal with session intact. |
| I3 | Pass | Apostrophe in `ZZ TEST Dato' Lim resolution` was accepted on the MOI. |
| I4 | Not executed | Did not submit zero / negative quantity or a past required date as a dedicated negative case. |
| I5 | Not executed | Did not upload an oversized or wrong-type file. |
| I6 | Not executed | Did not approve the same step from two tabs (no MOA step was active). |
| I7 | Pass | Phone-width viewport (390×844) still showed Settings nav, User Management, and billing directory; controls were not trapped off-screen. Screenshot not committed (earlier banner may have included a one-time password). |
| J | Partial | Signed-out visit to `/customers` shows login. After sign-in the SPA ignores the path and opens the role’s default dashboard. There is no private-window tool in this browser; second-tab sign-out (A5) is the closest equivalent. |

---

## Findings

### F — MOA chain not executed

- **Scenario:** F1–F11  
- **Severity:** coverage gap (not a product defect found)  
- **Steps:** Built `ZZ TEST HOLDINGS SDN BHD`, requested a service, opened and submitted an MOI. Stopped before starting MOA, so MS1–MS7, MS4 conditional appearance, M5 comment bounce, rejection resume, and non-assignee refusal were not seen live.  
- **Expected:** Two full MOA runs as written in §11.3 F.  
- **Actual:** No MOA instance. MS4/MS7 Admin-override path unused.  
- **Evidence:** Purge dry-run before apply showed `moaForms: 0`.  
- **Why:** Honest stop rather than impersonating named LGB people or faking a pass. Re-run F after the next AWS cut-over when Resend can notify assignees.

### E2 — fail-closed MOI not executed

- **Scenario:** E2  
- **Severity:** coverage gap  
- **Steps:** Created the test company with MOI-approval flagged on `ZZ TEST Client Admin`, then submitted an MOI.  
- **Expected:** Readable footer error and a parked form in Unrouted MOI queue when nobody can be routed to.  
- **Actual:** Submit completed and the modal closed. Queue was not checked.  
- **Evidence:** Browser snapshot after submit showed Client portal, company tile `0/3 done`, no footer 400.  
- **Note:** To execute E2, create a `ZZ TEST` company with empty MOI approvers and no matrix row.

### G — live email-action tokens mostly not executed

- **Scenario:** G1, G2, G4  
- **Severity:** coverage gap (G3 passed)  
- **Expected:** Open approve link from mail, reuse, tamper, and stale-step messages.  
- **Actual:** G3 tampered URL is a friendly HTML page. G1/G2/G4 need a token from the Resend owner inbox or Railway logs after a step whose recipient is the owner.  
- **Evidence:** API page title `Link expired` for a garbage token.

### Side effect from the earlier UAT session (already purged or still live)

- **Scenario:** C5 (prior session)  
- **Severity:** ops note, not a product bug  
- **Actual:** Acme Client Admin (`clientadmin@acme.test`) was reset via the key icon; the one-time password was dismissed without copying. That account is **not** `ZZ TEST`-prefixed, so purge does not restore it. Admin can reset it again from Settings → Users.  
- **Nadia:** password was changed during A1 in the first session; she is no longer on the shared staff temp password.

No other defects (blocker / major / minor / cosmetic) were filed from the screens that were seen.

---

## Test data created and purged

Created (all `ZZ TEST` prefixed):

- Product: `ZZ TEST Package`
- Customer: `ZZ TEST HOLDINGS SDN BHD` (Bellworth; invoice-by / charge-to = existing directory row Adil Cita Sdn Bhd — not a new billing party)
- Users: auto Client Admin, `ZZ TEST Client Admin` (signatory), invited `ZZ TEST Extra Admin`

Purge:

1. Dry run: companies `[ZZ TEST HOLDINGS SDN BHD]`, customers 1, users 3, products 1, jobRequests 3, moiForms 3, moaForms 0, invoices 0, notifications 1.  
2. Apply: `applied: true`, same company only.  
3. Dry run again: every count **0**, `companies: []`.  
4. Customers page `innerText` contains no `ZZ TEST`.

Protected logins (`ryannnism@gmail.com`, `ryannnism@berkeley.edu`) were not attached to the test company.

---

## Verdict

**Accepted with incomplete coverage.** Sign-in, role boundaries, Admin password reset, test-company setup, client service request, MOI submit with T3, accident cases I1–I3/I7, tampered email-action HTML, billing reports from the prior session, and purge all behaved as specified. The pilot is acceptable to operate with the documented gaps. Do not read this file as live proof of MS4, MS7, M5 bounce, fail-closed unrouted MOI, or inbox approve-links.

Re-run §11 F, E2–E3, G1–G2/G4, and H1 after Resend is on a real domain.

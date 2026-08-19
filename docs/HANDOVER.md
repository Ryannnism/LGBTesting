# LGB Services — Product Handover

**Date:** 2026-08-19 (ready to send)  
**Product:** LGB Services (MOI / MOA company-secretarial workflow)  
**Status:** **Pilot is up and ready to hand over.** Vercel UI + Railway API + Postgres. Live API image `ef860ba` (Admin password reset, test-data purge, `Jwt__Key` rotated). Git `main` on both remotes also has the §11 walkthrough results.  
**Handover reproduction path:** Terraform UAT (Lightsail + RDS SQL Server) in [`infra/terraform/uat/`](../infra/terraform/uat/README.md)  
**Canonical review trail:** [`SYSTEM_REVIEW_7_UX.md`](./SYSTEM_REVIEW_7_UX.md)  
**Walkthrough results:** [`UAT_LIVE_WALKTHROUGH_RESULTS.md`](./UAT_LIVE_WALKTHROUGH_RESULTS.md)  
**Audience:** next engineer, ops owner, or client admin taking ownership

This document is the single place for **what shipped**, **how to operate it**, **who gets which emails**, **where secrets live**, and **what must still be closed** so the product does not need open-ended future enhancement.

### Send-off in one page

The product is complete on our side except two owned leftovers. Do these on the day you take it:

1. **Everyone must sign in again.** `Jwt__Key` was rotated 19 Aug 2026.
2. **Distribute the staff temp password** from `~/lgb-handover-temp-password.txt` (outside the repo), then delete that file. Nadia already changed hers during the walkthrough. Live staff emails are still the `@lgb.test` aliases (`SEED_STAFF=false`, so CubeV addresses were never renamed in this database).
3. **Acme Client Admin** (`clientadmin@acme.test`) was reset during UAT and is not `ZZ TEST`-prefixed, so purge did not restore it. Reset it from Settings → Users if you still need that login.
4. **AWS backend team (Resend account + DNS):** verify an org sending domain, point `Email__From` at it, rotate `Email__ResendApiKey`, then set `Reminders__SendEmail=true` and watch one R3 and one M3. Do not flip reminders first.
5. **Client:** supply LGB Group’s mandatory MOA approver names (MS5). CubeV left that column blank. Until then an LGB MOA stalls at MS5 unless Admin uses the Start-MOA override. Do not invent names.

The live database is clean of walkthrough data. Re-run §11 F (MOA chain), E2 (fail-closed MOI), and G (inbox approve-links) after Resend is on a real domain.

**What to send:** this file. Point the recipient at [`UAT_LIVE_WALKTHROUGH_RESULTS.md`](./UAT_LIVE_WALKTHROUGH_RESULTS.md) in the same repo (do not paste passwords into the email). Send the staff temp password out of band from `~/lgb-handover-temp-password.txt`, then delete that file. Do not attach Railway variables, `.env`, or git history dumps.

### Live check (2026-08-19, after §7.1 close-out and walkthrough)

| Check | Result |
|---|---|
| `https://lgb-testing.vercel.app` | 200 — login UI |
| Wrong-password sign-in | API 401 `Invalid email or password.` shown in the form (CORS preflight 204 from Vercel origin) |
| `GET /api/health` | 200 `{ status: ok }` |
| Railway `LGBTesting` | SUCCESS, 1 replica, commit `ef860ba` |
| Git `main` | Both remotes include the walkthrough results (`8476bc1`) and this send-off (`191e076`) |
| Postgres | Data survived the outage: 169 customers, 779 MOIs, 1079 jobs, 281 users, 0 keyless tables |
| Schema | All 9 Postgres migrations applied including `Pg_RepairPgloaderSchema` (30 PK, 33 FK, 76 indexes) |
| `SEED_STAFF` | `false` |
| Reminders / From | Worker ticks every 15 min, log-only; `Email__From` is still the Resend sandbox sender |
| §11 walkthrough | **Accepted with incomplete coverage** — no blockers on the screens that were driven. MS1–MS7 twice, fail-closed unrouted MOI, and inbox approve-links were not executed. See [`UAT_LIVE_WALKTHROUGH_RESULTS.md`](./UAT_LIVE_WALKTHROUGH_RESULTS.md) |
| Test-data purge | Final dry run all zeros. Customers UI has no `ZZ TEST`. Protected logins left in place |

To reproduce a standalone UAT (not Railway): apply Terraform, then ship the GitHub Actions zip — see §2.4.

---

## 1. System at a glance

| Layer | Where | URL / note |
|---|---|---|
| Frontend | Vercel | https://lgb-testing.vercel.app |
| API | Railway (`LGBTesting`) | https://lgbtesting-production-4d6b.up.railway.app |
| Health | API | `GET /api/health` |
| Database | Railway Postgres | `Database__Provider=Postgres` |
| Uploads | Railway volume | `LGB_UPLOAD_ROOT=/data/uploads` (if configured) |
| Email | Resend | `Email__ResendApiKey` on Railway |
| Auth | JWT (API) | `Jwt__Key` on Railway |

**Repos**

| Remote | GitHub | Purpose |
|---|---|---|
| `testing` | https://github.com/Ryannnism/LGBTesting | **Live deploy source** (Railway + Vercel watch this) |
| `origin` | https://github.com/danra69-hash/LGBServices | Mirror / secondary |

**Rule:** after code or config changes that should go live, commit and **push both remotes** (`testing` and `origin`) to `main`. Never commit `.env`, Resend keys, JWT keys, or connection strings.

**Local workspace:** `/Users/ryannnism/LGBServices`

---

## 2. How to push / deploy

### 2.1 Normal code change

```bash
cd /Users/ryannnism/LGBServices
# run tests first — CI does not gate deploys
dotnet test LGBApp.Backend.Tests/LGBApp.Backend.Tests.csproj
git status
git add <relevant files only>   # never git add -A with secrets or .claude/
git commit -m "…"
git push testing main
git push origin main
```

- **Railway** rebuilds the API from `testing` (`Dockerfile` + `railway.toml`).
- **Vercel** rebuilds the frontend from the same repo (`vercel.json`).

### 2.2 Frontend API base

Split deploy: set Vercel env `VITE_API_BASE` to the Railway public API URL (see `LGBApp.Frontend/.env.production.example`). If unset, the UI expects same-origin `/api` proxying.

### 2.3 One-shot data seeds (not every deploy)

| Action | How | When |
|---|---|---|
| Internal staff | Boot with `SEED_STAFF=true` + `SEED_STAFF_PASSWORD` | Once per empty DB; then set `SEED_STAFF=false` |
| CubeV customers / packages / jobs | `dotnet run --project LGBApp.Backend -- seed-full` | Once (or re-run for SOURCE upsert) |
| MOI approval matrix | Seeded from `Data/Seed/moi-approval-matrix.json` on boot / ensure | After matrix file changes |
| Workdone → completed services | Part of `seed-full` importer | Re-run only when workbook changes |

Full CubeV procedure: [`CUBEV_SEED_RUNBOOK.md`](./CUBEV_SEED_RUNBOOK.md).  
Go-live checklist: [`deploy/GO_LIVE.md`](./deploy/GO_LIVE.md).  
Postgres cutover: [`POSTGRES_MIGRATION_GUIDE.md`](./POSTGRES_MIGRATION_GUIDE.md).

### 2.4 Reproduce UAT without Railway (Terraform artifact)

This is the handover reproduction path. It does **not** clone the live Railway Postgres — it stands up a fresh Lightsail VM + RDS SQL Server in `ap-southeast-1`.

```bash
cd infra/terraform/uat
cp terraform.tfvars.example terraform.tfvars   # set admin_ssh_cidrs to YOUR_IP/32
terraform init
terraform apply
terraform output -raw lightsail_static_ip
terraform output -raw connection_string        # → /etc/lgbapp/lgbapp.env
```

Then:

1. One-time VM setup: [`deploy/aws-lightsail-uat.md`](./deploy/aws-lightsail-uat.md) (nginx, .NET 8, systemd `EnvironmentFile`).
2. `CREATE DATABASE LGBApp;` on RDS (SQL Server Express does not create a named DB).
3. Push branch `uat` or run **UAT Build & Deploy** — the zip artifact is `lgbapp-uat-release` (30-day GitHub retention).
4. GitHub secrets: `UAT_SSH_HOST`, `UAT_SSH_USER=ubuntu`, `UAT_SSH_PRIVATE_KEY` from Terraform outputs.

Never commit `terraform.tfvars`, `*.tfstate`, or RDS passwords.

---

---

## 3. Enhancements shipped (recent, high-signal)

HEAD: **`ef860ba`** on the live API (Admin reset + purge). Git `main` send-off is `191e076`. Tests: **152** passing at last full run.

### 3.1 Close-out build (8 Aug 2026)

Built the §7 remainder in dependency order. Trail in [`SYSTEM_REVIEW_7_UX.md`](./SYSTEM_REVIEW_7_UX.md) §14.

| Commit | What it does |
|---|---|
| `6b3d7dc` | MOA chain actually starts — the sequential UI was flag-gated off, and MS1 matched job titles against `Users.Name`, so it issued tokens with empty emails |
| `681b93a` | Admins can edit the MS5 mandatory approver list (the PUT previously dropped it) |
| `857db76` | **M1** stage-1 broadcast to legal + secretarial, scoped to LGB / Bellworth / SWM |
| `c0119a9` | **T3** MOI last point of approval persisted, required with LOA, and preferred by MS7 |
| `bcb6a4d` | **M5** any approver comment bounces to all cosec and holds the step; in-app reject added |
| `15b79d3` | **MS6/C3** cosec inserts approvers into a running chain, with step renumbering |
| `c99ab34` | Unroutable MOIs park for Admin assignment instead of skipping client approval |
| `9f008ed` | **B5** quarterly billing report — PDF + CSV + JSON, Admin-only; `IssuedAt` now stamped |
| `2aac242` | Restored the primary keys, foreign keys and indexes a pgloader import silently dropped — this is what had been failing every deploy since 17 July |

### 3.2 Earlier waves

| Commit / area | What it does |
|---|---|
| `2631764` | Real Cosec / Legal staff emails (CubeV); aliases from `@lgb.test` |
| `4616372` | MOI Approval Matrix 1:1 (hide client AllRequired / AnyOne UI) |
| `865e697` | Admin MOA company list + Start-MOA override; package-complete notify; workdone → `CompletedServices`; SOURCE upsert rows 2–167 |
| `d5bcf14` | MS5 Group mandatory = override → company JSON → `DivisionGroup.MandatoryMoaApproversJson`; matrix miss logging |
| `150c775` | **W1** `ReminderWorker` + `ReminderLog` + R3/R4/M3/M4 caps; default **log-only** |
| `e92e041` | **W4** no-login MOA approve/reject via email token (`/api/email-actions/{token}`, 72h single-use) |
| `1c84dc5` | **B6** real PDF invoices (QuestPDF), not `.txt` |
| Earlier waves | Review #2–#5 Postgres/resilience; Print Pack; multi-qty sessions; UX label/count fixes; MOA chain MS1–MS7 |

### 3.3 Handover-day close-out (19 Aug 2026)

| Commit | What it does |
|---|---|
| `ef860ba` | Admin-side password reset (`POST /api/users/{id}/reset-password` + Settings key icon) and Admin-only `ZZ TEST` purge. Live API image. `Jwt__Key` and staff seed password rotated on Railway after this deploy |
| `32c2fba` | HANDOVER §7.1/§11, SR7 §14.5, reminder-email runbook for the AWS team |
| `8476bc1` | Live §11 walkthrough results and screenshots |
| `191e076` | This HANDOVER send-off refresh: live Cosec aliases, what to send, walkthrough coverage |

**Tests:** 152 passing at last full run (`AdminPasswordResetTests` + `TestDataPurgeTests` included). Always re-run before push.

---

## 4. API keys & environment (names only — never paste secrets here)

Secrets live in **Railway → LGBTesting service → Variables**. Do not put them in git.

### 4.1 Required / live today

| Variable | Purpose | Notes |
|---|---|---|
| `Database__Provider` | `Postgres` | Live is Postgres |
| `ConnectionStrings__DefaultConnection` | Npgsql connection | Or Railway `DATABASE_URL` if wired |
| `Jwt__Key` | Sign JWTs | ≥32 chars; **rotate if ever exposed in chat/logs** |
| `Jwt__Issuer` / `Jwt__Audience` | Token claims | Match appsettings if set |
| `Cors__AllowedOrigins__0` | SPA origin | `https://lgb-testing.vercel.app` |
| `App__PublicFrontendUrl` | Links in emails / UI | `https://lgb-testing.vercel.app` |
| `App__PublicApiUrl` | **Required for W4** email action links | Railway public API URL |
| `Email__ResendApiKey` | Resend send | **Rotate if exposed**; never commit |
| `Email__From` | From header | Currently Resend onboarding sender — verify a real domain for production |
| `SEED_STAFF` | Staff seeder gate | Must be **`false`** after one-shot seed |
| `SEED_STAFF_PASSWORD` | Shared password for seeded staff | Users have `MustChangePassword`; rotate after pilot |
| `ASPNETCORE_ENVIRONMENT` | `Production` | |
| `DISABLE_HTTPS_REDIRECTION` | Often `true` behind Railway TLS | |

### 4.2 Optional / not yet flipped

| Variable | Default | When to set |
|---|---|---|
| `Reminders__SendEmail` | `false` (appsettings) | Logs are verified — `ReminderLogs` holds real R3/R4 rows. Set `true` **only after** a sending domain is verified (§4.4), otherwise the mail reaches nobody |
| `Reminders__IntervalMinutes` | worker default | Tune poll cadence if needed |
| `LGB_UPLOAD_ROOT` | `/data/uploads` | Volume mount for file storage |

### 4.3 Vercel

| Variable | Purpose |
|---|---|
| `VITE_API_BASE` | Railway API base URL for browser calls |
| `VITE_SUPABASE_URL` / `VITE_SUPABASE_PUBLISHABLE_KEY` | Only if using Supabase auth features |

### 4.4 Email delivery caveat

`Email__From` using `onboarding@resend.dev` only reliably delivers to the Resend account owner until a **verified sending domain** is configured. For real executive mail, verify `lgb.com.my` (or chosen domain) in Resend and update `Email__From`.

### 4.5 Security hygiene (do this on handover day)

1. Rotate `Email__ResendApiKey` in Resend + Railway. **Still outstanding — AWS backend team.**
2. Rotate `Jwt__Key` (forces re-login for everyone). **Done 19 Aug 2026.**
3. Confirm `SEED_STAFF=false`. **Already false.**
4. Change seeded shared password / force password resets for Sharon, Poh Li, and test admins. **Done 19 Aug 2026** via `POST /api/users/{id}/reset-password`. The OTP forgot-password path still needs a verified sender; the Admin key icon in Settings → Users is the supported reset path until then. The one-time temp password was written to `~/lgb-handover-temp-password.txt` (outside the repo) — distribute out of band and delete the file afterwards.
5. Confirm no secrets in git history for this branch.

### 4.6 Test-data purge (live acceptance only)

There is no staging server, so the §11 walkthrough runs against the live database. `POST /api/admin/test-data/purge` (Admin only) removes that data. Dry run unless `?apply=true`. The prefix `ZZ TEST` is hard-coded rather than caller-supplied, so a request cannot widen the blast radius. It refuses if more than five companies match, and it never deletes the two seeded live-test logins (`ryannnism@gmail.com`, `ryannnism@berkeley.edu`). Shared config (division groups, matrix rows, workflow templates, billing parties) is left alone — revert those by hand if you changed them during a pass.

**19 Aug 2026 pass:** dry run matched `ZZ TEST HOLDINGS SDN BHD` only, apply succeeded, second dry run was all zeros, Customers UI had no `ZZ TEST`. No shared config was edited. Acme Client Admin is not prefixed, so it was not restored — reset it from Settings → Users if needed.

---

## 5. User guide (by role)

### 5.1 Sign-in

1. Open https://lgb-testing.vercel.app  
2. Sign in with the assigned email + password. After the 19 Aug `Jwt__Key` rotation, any old session is invalid — use the login form again.  
3. Cosec staff on this live database use `@lgb.test` aliases (§6.2), not the CubeV addresses. Shared temp password is in `~/lgb-handover-temp-password.txt` until they change it (`MustChangePassword`). Nadia already changed hers during the walkthrough.  
4. If a user is locked out and mail is still sandboxed, Admin resets them from Settings → Users (key icon). Do not use forgot-password for production staff until Resend is on a real domain.

### 5.2 Client (ClientAdmin / company user)

| Task | Where / how |
|---|---|
| Start MOI | Company / services → create MOI form |
| Approvers | **Matrix-bound** for matched requesters — no free AllRequired/AnyOne picker |
| Submit for approval | Submit; HOD (matrix approver) is notified |
| Track status | Dashboard / form detail |
| Start MOA (if allowed) | Follow company flow; Admin may override Start-MOA from admin tools |
| Multi-qty | Start additional sessions on demand when package allows |

### 5.3 Cosec / internal User (resolution prep)

| Task | Where / how |
|---|---|
| Work queue | Admin / package workboard, task lists |
| Print pack | Export / Print pack action on task packs |
| Do not mark complete | While workflow mode is still **Unset** — blocked by design |
| Package complete | When last work finishes, package-complete handoff can notify |

### 5.4 Approver (MOI HOD / MOA step assignee)

| Task | How |
|---|---|
| MOI approve / reject | **Login required** — open link to frontend, sign in, act on form. This is per clause R5, not a missing feature |
| MOA approve / reject | Login **or** one-time email link (72h, single use) if W4 email was sent |
| **Leaving a comment on a MOA step** | A comment **bounces the form back to cosec** and the step stays open, whether you approve or reject (clause M5). Approve with the comment box empty if you simply mean yes |
| Reminders | Engine evaluates every interval; emails only if `Reminders__SendEmail=true` |

### 5.5 Admin (Sharon / Poh Li / Ryan Admin)

| Task | Where / how |
|---|---|
| Intake approval | MOI intake queue |
| Recommend / approve MOI | Per capabilities on user record |
| MOA company list / Start-MOA override | Admin MOA tools |
| Set company `MoaApproversJson` / Group mandatory | Company / division group admin — **required for LGB Group MS5** |
| **MOIs waiting for an approver** | Admin dashboard queue. A form lands here when the requester matches no Approval Matrix row and the company has no MOI approver. Enter the approver's name and email to release it — it will not proceed on its own, by design |
| **Add approvers to a running MOA** | Open the MOA, use the add-approver control in the chain section (MS6/C3). They are inserted after the current step and everyone later shifts down one |
| **Quarterly billing report (B5)** | Admin → Reports. Pick year and quarter, download PDF or CSV. Covers invoices in the quarter, package value and quota used. There is no Finance role — the Finance Head signs in with an Admin account |
| Invoices | List + download **PDF** (`{id}/pdf`) |
| Staff / seed | Prefer UI user management; avoid re-running `SEED_STAFF=true` on live |
| **Reset a user's password** | Settings → Users → key icon. Returns a temporary password once. Or `POST /api/users/{id}/reset-password`. Cannot reset your own account this way — use Change password. |
| **Purge §11 test data** | `POST /api/admin/test-data/purge` (dry run) then `?apply=true`. Only `ZZ TEST` companies. |

### 5.6 Typical happy path (MOI → MOA)

```
Client submits MOI
  → Matrix binds HOD (1:1)
  → HOD approves (login)
  → Cosec / Legal MOA chain (MS1–MS7 per template)
  → MOA assignees act (login or email link)
  → Package / billing as configured
  → Invoice PDF available
```

### 5.7 Useful ops commands

```bash
# Health
curl -s https://lgbtesting-production-4d6b.up.railway.app/api/health

# Local API (dev)
cd LGBApp.Backend && ASPNETCORE_ENVIRONMENT=Development dotnet run

# Full CubeV + workdone import (production connection string required)
dotnet run --project LGBApp.Backend -- seed-full

# Tests
dotnet test LGBApp.Backend.Tests/LGBApp.Backend.Tests.csproj

# Quarterly billing report (B5) — Admin token required
curl -s -H "Authorization: Bearer $TOKEN" \
  "https://lgbtesting-production-4d6b.up.railway.app/api/reports/billing/quarterly?year=2026&quarter=3&format=csv"
```

**Schema health check** — run this against Postgres after any restore or re-import. It must return no rows; see §7.3.

```sql
SELECT rel.relname AS table_without_primary_key
FROM pg_class rel
JOIN pg_namespace ns ON ns.oid = rel.relnamespace
WHERE ns.nspname = 'public' AND rel.relkind = 'r'
  AND NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid = rel.oid AND c.contype = 'p');
```

---

## 6. Directory of emails

### 6.1 System / ops accounts

| Role | Email | Notes |
|---|---|---|
| Live test Admin | `ryannnism@gmail.com` | Seeded Admin; Cosec capabilities |
| Live test ClientAdmin | `ryannnism@berkeley.edu` | Client-side testing |
| Intake also includes | `danra69@gmail.com` | Intake approver list |

### 6.2 Internal Cosec / Legal (seeded staff)

| Name | CubeV email (intended) | Live login today | Role | Job |
|---|---|---|---|---|
| Sharon | `sharon@lgb.com.my` | `sharon@lgb.test` | Admin | Senior Manager, Company Secretarial |
| Ng Poh Li | `pohli.ng@taliworks.com.my` | `ngpohli@lgb.test` | Admin | Senior Manager, Company Secretarial |
| Nita | `dzatin.zaharuddin@taliworks.com.my` | `nita@lgb.test` | User | Resolution preparation |
| Siti | `zalila.zainal@lgb.com.my` | `siti@lgb.test` | User | Resolution preparation |
| Nadia | `nadia.rahman@taliworks.com.my` | `nadia@lgb.test` | User | Resolution preparation |
| Datin Raj | `raj@taliworks.com.my` | CubeV email | User | Group Legal (MOA approve + signatory) |
| Seet Mei | `seetmei.lee@taliworks.com.my` | CubeV email | User | Group Legal |
| Dee Nee | `deenee.ooi@taliworks.com.my` | CubeV email | User | Group Legal |
| Sutina | `sutina.sujeno@taliworks.com.my` | CubeV email | User | Group Legal |

Source: `LGBApp.Backend/Data/InternalStaffSeeder.cs`.

`SEED_STAFF=false` on Railway, so the alias→CubeV rename never ran for Cosec. Sign those five in with `@lgb.test`. Group Legal was seeded on CubeV addresses (no alias row). Rename Cosec in Settings → Users when you want production inboxes, or boot once with `SEED_STAFF=true` (that also resets passwords from `SEED_STAFF_PASSWORD` — do not do this casually).

### 6.3 MOI Approval Matrix — HOD approvers (unique)

| Group | Approver | Email |
|---|---|---|
| LGB | Datin Irene | `irene@lgb.com.my` |
| LGB | Tai Kok Hong | `khtai@lgb.com.my` |
| LGB | Shally Lim | `shally@lgb.com.my` |
| LGB | Sean | `seanlim@lgb.com.my` |
| LGB | David Au Yeong | `david.auyeong@taliworks.com.my` |
| LGB | Sia Kwee Siam | `kweesiam.sia@taliworks.com.my` |
| LGB | Kevin Teoh | `kevin.teoh@exitra.com.my` |
| LGB | Sam Lau | `sam.lau@ecoleaf.com.my` |
| BELLWORTH | Kevin Kuok | `kkuok@bellworth.com.my` |
| SWM | Bin Lay Thiam | `laythiam.bin@swmsb.com` |
| SWM | Ho De Leong | `hdl@swmsb.com` |
| SWM | Goay Sook Min | `soonming.goay@swmsb.com` |
| SWM | Tn Hj Norlisam | `norlisam.nordin@swmsb.com` |

### 6.4 MOI Approval Matrix — full requester → approver map

Authoritative file: `LGBApp.Backend/Data/Seed/moi-approval-matrix.json` (36 rows; SWM has duplicate Shirley→Ho De Leong entries as in source).

| Group | Requester | Requester email | Approver | Approver email |
|---|---|---|---|---|
| BELLWORTH | Evelyn Lui | `evelyn.lui@bellworth.com.my` | Kevin Kuok | `kkuok@bellworth.com.my` |
| BELLWORTH | Gan Kah Mun | `kahmun.gan@bellworth.com.my` | Kevin Kuok | `kkuok@bellworth.com.my` |
| BELLWORTH | Jasylyn Lim | `jaslyn.lim@bellworth.com.my` | Kevin Kuok | `kkuok@bellworth.com.my` |
| BELLWORTH | Lam Kok Choong | `kokchoong.lam@bellworth.com.my` | Kevin Kuok | `kkuok@bellworth.com.my` |
| BELLWORTH | Siti Farah | `norfarahhanim.ghani@bellworth.com.my` | Kevin Kuok | `kkuok@bellworth.com.my` |
| BELLWORTH | Wong Wai Ling | `waileng.wong@bellworth.com.my` | Kevin Kuok | `kkuok@bellworth.com.my` |
| LGB | Adeline Liew | `adeline@parkwood.my` | Sean | `seanlim@lgb.com.my` |
| LGB | Casper Wong | `casper.wong@parkwood.my` | Sean | `seanlim@lgb.com.my` |
| LGB | Danny Ng | `danny.ng@lgb.com.my` | Sean | `seanlim@lgb.com.my` |
| LGB | Jess Hee | `jess.hee@lgb.com.my` | Shally Lim | `shally@lgb.com.my` |
| LGB | Justin Hor | `justin.hor@parkwood.my` | Sean | `seanlim@lgb.com.my` |
| LGB | Kam Kah Ken | `kahken.kam@wellcocapital.com` | Sean | `seanlim@lgb.com.my` |
| LGB | Keneth Ng | `kenneth.ng@lgb.com.my` | Tai Kok Hong | `khtai@lgb.com.my` |
| LGB | Kevin Teoh | `kevin.teoh@exitra.com.my` | Kevin Teoh | `kevin.teoh@exitra.com.my` |
| LGB | Khoo May Lin | `maylin@lgb.com.my` | Tai Kok Hong | `khtai@lgb.com.my` |
| LGB | Lenny Wong | `lenny.wong@lgb.com.my` | Datin Irene | `irene@lgb.com.my` |
| LGB | Magdelene Fong | `magdeline.fong@gsl-realty.com` | Shally Lim | `shally@lgb.com.my` |
| LGB | Ong Teng Yew | `tengyew.ong@gsl-development.com` | Shally Lim | `shally@lgb.com.my` |
| LGB | Rosenne Cheok | `rosenne.cheok@lgb.com.my` | Datin Irene | `irene@lgb.com.my` |
| LGB | Sam Kit Phun | `kitphun.sam@lgb.com.my` | Tai Kok Hong | `khtai@lgb.com.my` |
| LGB | Sam Lau | `sam.lau@ecoleaf.com.my` | Sam Lau | `sam.lau@ecoleaf.com.my` |
| LGB | Shermay Loh | `shermay.loh@lgb.com.my` | Datin Irene | `irene@lgb.com.my` |
| LGB | Sia Kwee Siam | `kweesiam.sia@taliworks.com.my` | David Au Yeong | `david.auyeong@taliworks.com.my` |
| LGB | Stephanie Chai | `stephanie.chai@taliworks.com.my` | Sia Kwee Siam | `kweesiam.sia@taliworks.com.my` |
| LGB | Steven Chan | `steven.chan@lgb.com.my` | Shally Lim | `shally@lgb.com.my` |
| LGB | Tai Kok Hong | `khtai@lgb.com.my` | Datin Irene | `irene@lgb.com.my` |
| LGB | Tan Yee Ting | `yeeting.tan@lgb.com.my` | Shally Lim | `shally@lgb.com.my` |
| LGB | Tenney Lee | `tenny.lee@lgb.com.my` | Tai Kok Hong | `khtai@lgb.com.my` |
| LGB | Tiew Siong Yee | `siongyee.tiew@lgb.com.my` | Shally Lim | `shally@lgb.com.my` |
| SWM | Bin Lay Thiam | `laythiam.bin@swmsb.com` | Ho De Leong | `hdl@swmsb.com` |
| SWM | Shirley Nicholas | `shirley.nicholas@swmsb.com` | Ho De Leong | `hdl@swmsb.com` |
| SWM | Shirley Nicholas | `shirley.nicholas@swmsb.com` | Tn Hj Norlisam | `norlisam.nordin@swmsb.com` |
| SWM | Tan Swee Hock | `sweehock.tan@swmsb.com` | Goay Sook Min | `soonming.goay@swmsb.com` |
| SWM | Tong Sheau Wei | `sheauwei.tong@swmsb.com` | Bin Lay Thiam | `laythiam.bin@swmsb.com` |
| SWM | Yvonne Kong | `yeefong.kong@swmsb.com` | Bin Lay Thiam | `laythiam.bin@swmsb.com` |

### 6.5 MOA Group mandatory names (MS5 defaults)

From `WorkflowConfigSeeder` (names resolved to users / company JSON at runtime):

| Group | Mandatory MOA approvers (seed default) |
|---|---|
| BELLWORTH | Kevin Kuok |
| SWM | Janice Lim, Ho De Leong, Shirley Nicholas |
| LGB | **Empty until Admin sets** company override or group JSON |

Named MOA steps still reference **Teh SW** (banking) and **Dato' Lim** (final) on relevant templates — ensure those users exist with matching display names before live SWM/LGB final chains.

### 6.6 Source workbook

CubeV / billing / SOURCE data: `docs/source/COSEC_Billing_Tracking_2026_CubeV.xlsx`  
Built seed: `LGBApp.Backend/Data/Seed/cubev-init.json`

**Do not invent routing emails.** Update the matrix JSON or CubeV workbook, then reseed.

---

## 7. Where the system is lacking — and how to finish it

The product should be treated as **complete after the close-out list below**. These are not “nice to haves”; they are remaining CubeV / Review #7 obligations. Do them in order, then stop enhancing unless the client changes the flowchart.

**As of 8 Aug 2026 the code side of CubeV close-out is finished.** As of 19 Aug 2026 Railway is restored and serving `ef860ba`. Item 10 is closed apart from the Resend API key. Items 1 and 2 belong to the AWS backend team with the Resend account. The first live §11 walkthrough is recorded; coverage gaps are listed there, not as product defects.

### 7.1 Close-out checklist

Items 3–9 are **built and deployed** as of `2aac242` (8 Aug 2026); see [`SYSTEM_REVIEW_7_UX.md`](./SYSTEM_REVIEW_7_UX.md) §14 for the per-item trail. Items 1 and 2 are handed to the AWS backend team with the Resend account. Item 10 is done except for the Resend API key.

| # | Gap | Status | What is left |
|---|---|---|---|
| 1 | Reminder **emails** off | ⚠️ engine verified | `ReminderLogs` holds real R3/R4 rows and the worker ticks every 15 min, so the pipeline is proven. Set `Reminders__SendEmail=true` **after** item 2, then watch one live reminder. Flipping it first sends executive mail from a sandbox address that reaches nobody. **Owner: AWS backend team** — see the runbook below the table. |
| 2 | Resend **from-domain** | ❌ blocked on DNS | Verify a real domain in Resend, point `Email__From` at it, retest one WorkflowNotifier send. Until then delivery is limited to the Resend account owner (§4.4). **Owner: AWS backend team** (they hold the Resend account and DNS). |
| 3 | **MS6 / C3** Cosec mid-flight insert | ✅ | — `WorkflowService.InsertCosecStepAsync` + chain UI control |
| 4 | **B5** quarterly billing report | ✅ | — `GET /api/reports/billing/quarterly?year=&quarter=&format=pdf\|csv`, Admin tab UI. No Finance role exists; the Finance Head uses an Admin account |
| 5 | **M1** stage-1 broadcast | ✅ | — legal + secretarial, scoped to LGB / Bellworth / SWM, notification-only |
| 6 | **T3 / M5** last-point-of-approval + bounce-on-comment | ✅ | — persisted on `MOIForm` and preferred by MS7; any approver comment bounces to all cosec and holds the step |
| 7 | Matrix **fail-open** | ✅ | — unrouted forms park and submit returns an actionable 400; clear them from the Admin queue. The company-approver fallback is deliberately kept |
| 8 | LGB Group **MS5 empty** | ⚠️ blocked on client data | CubeV's Approval Matrix leaves the mandatory-approver column **blank for LGB GROUP**, so there is nothing to copy. With an empty list `WorkflowService.ResolveAssigneeName` returns the literal `"MOA approvers (none set)"`, no user matches it in `CanUserApproveStepAsync`, and `ApprovalActionTokenService` issues only a generic Admin-forward link — an LGB MOA would stall at MS5. Interim path: supply approvers per form via the Start-MOA override, or enter them in Admin → Workflow config once the client confirms the names. Do not invent them. |
| 9 | MOI still **login-only** | ✅ conformant by spec | **Nothing to build.** Clause R5 requires MOI approvers to log in, so login-only is correct. Earlier entries treating this as a gap were wrong |
| 10 | Secret rotation | ⚠️ Resend key only | `Jwt__Key` rotated 19 Aug 2026 (everyone must sign in again). Accounts still on the seeded password were reset to a fresh temp value; `SEED_STAFF=false` and `SEED_STAFF_PASSWORD` updated. Admin-side reset: `POST /api/users/{id}/reset-password` plus the key icon in Settings → Users, so future resets need no email. **Leave `Email__ResendApiKey` rotation to the AWS backend team.** See §4.5 |
| 11 | Live API replica | ✅ revived 19 Aug 2026 | Railway `LGBTesting` SUCCESS on `ef860ba`. Git `main` also has walkthrough results. Take a Postgres dump before AWS. |

**Reminder email runbook (item 1) — AWS backend team.** Do not flip this until item 2 is done. 1) Verify an org domain in Resend. 2) Set `Email__From` to an address on it. 3) Set `Reminders__SendEmail=true`. 4) Within 15 minutes (worker interval) confirm one R3 (`MoiHodReminder`) and one M3 (`MoaApproverReminder`) arrive, and that a matching `ReminderLogs` row shows the send flag true. The worker was confirmed still ticking after the 19 Aug revival (`ReminderWorker started`, `SELECT` against `ReminderLogs`).

### 7.2 Explicitly out of scope (do not build)

- New standalone “workdone module” UI (importer into `CompletedServices` is enough)
- Full dual-entry accounting ledger
- Inventing emails not present in CubeV / matrix / staff seed
- Replacing Postgres with SQLite for production

### 7.3 Acceptance criteria for “no further enhancement”

The product is **done** when all of the following are true:

1. `Reminders__SendEmail=true` and at least one R3 and one M3 email observed in production. — **outstanding, AWS backend team** (do after 2)
2. Resend sends from a verified org domain. — **outstanding, AWS backend team**
3. ✅ MS6/C3 works in a live dry-run (Cosec inserts mid-flight; step applies).
4. ✅ Finance can download or receive a **3-monthly** billing report (B5).
5. ✅ Stage-1 broadcast (M1) matches flowchart for LGB / Bellworth / SWM.
6. ✅ T3/M5 last-point + bounce-on-comment behave per flowchart.
7. ✅ Matrix unmatched requesters cannot silently skip HOD (fail-closed or Admin path).
8. LGB MS5 mandatory list populated for companies that go live. — **blocked on client data.** CubeV has no LGB names; an empty list stalls MS5. Editable in Admin once the client supplies them.
9. Secrets rotated; `SEED_STAFF=false`; `dotnet test` green; both remotes on same `main` SHA. — `Jwt__Key` and staff seed password rotated 19 Aug 2026; Resend API key left to the AWS backend team. `SEED_STAFF=false`, 152 tests green. Live API `ef860ba`; git `main` includes the walkthrough results.
10. ✅ This handover + SR7 (now §14) updated to mark each item. First §11 pass recorded in [`UAT_LIVE_WALKTHROUGH_RESULTS.md`](./UAT_LIVE_WALKTHROUGH_RESULTS.md) — accepted with incomplete coverage (MOA chain, fail-closed MOI, inbox links). Re-run those after Resend.

**Also verify the schema after any Postgres restore or re-import.** A `drop indexes` load in July left production with no primary keys for three weeks, which surfaced only as an unrelated migration failure that blocked every deploy. `Pg_RepairPgloaderSchema` fixed it and the backend now warns on boot, but run the key-verification query in [`POSTGRES_MIGRATION_GUIDE.md`](./POSTGRES_MIGRATION_GUIDE.md) §8 acceptance if the database is ever reloaded.

After that: **operations and data only** (new companies, matrix row edits, password resets) — not feature work. The first §11 pass is attached; re-run F / E2 / G after the AWS Resend cut-over before calling the remaining items done.

---

## 8. Architecture & code map (quick)

| Concern | Location |
|---|---|
| API entry | `LGBApp.Backend/` |
| Frontend | `LGBApp.Frontend/` |
| MOI matrix seed | `Data/Seed/moi-approval-matrix.json` |
| Staff seed | `Data/InternalStaffSeeder.cs` |
| MOA templates / MS5 | `Data/WorkflowConfigSeeder.cs` |
| Reminders | `Services/ReminderWorker.cs`, `ReminderEvaluationService.cs` |
| Email actions (W4) | `Services/ApprovalActionTokenService.cs`, email-actions controller |
| Invoice PDF (B6) | Invoice PDF generation (QuestPDF) |
| Quarterly report (B5) | `Controllers/ReportsController.cs`, `Services/BillingReportService.cs`, `BillingReportPdfService.cs` |
| MOI last point of approval (T3) | `Services/LastPointOfApprovalService.cs` |
| MOA chain runtime (MS6/C3, MS7) | `Services/WorkflowService.cs` |
| MOI routing / fail-closed | `Services/JobHandoffService.cs` |
| Admin password reset | `Services/AdminPasswordResetService.cs`, `POST /api/users/{id}/reset-password` |
| Test-data purge | `Services/TestDataPurgeService.cs`, `Controllers/AdminTestDataController.cs` (`POST /api/admin/test-data/purge`) |
| Notifier | `Services/WorkflowNotifier.cs` |
| Dual DB migrations | Postgres EF + `SqliteSchemaMigrator` — **always update both** for schema changes |
| Postgres schema repair | `Migrations/Postgres/20260717095000_Pg_RepairPgloaderSchema.cs` — read its header before touching an imported database |
| Review / UX debt log | `docs/SYSTEM_REVIEW_7_UX.md` |

---

## 9. Related documents

| Doc | Use |
|---|---|
| [`SYSTEM_REVIEW_7_UX.md`](./SYSTEM_REVIEW_7_UX.md) | Full Review #7 + CubeV conformance trail |
| [`UAT_LIVE_WALKTHROUGH_RESULTS.md`](./UAT_LIVE_WALKTHROUGH_RESULTS.md) | 19 Aug 2026 live §11 pass — verdict and coverage gaps |
| [`uat-screenshots/`](./uat-screenshots/) | Screenshots from that pass (A5, B1, B2, C5) |
| [`CUBEV_SEED_RUNBOOK.md`](./CUBEV_SEED_RUNBOOK.md) | One-shot customer seed |
| [`deploy/GO_LIVE.md`](./deploy/GO_LIVE.md) | Railway / Vercel first bring-up |
| [`deploy/aws-lightsail-uat.md`](./deploy/aws-lightsail-uat.md) | Handover UAT: Terraform + Lightsail + RDS |
| [`../infra/terraform/uat/README.md`](../infra/terraform/uat/README.md) | Terraform apply / outputs / destroy |
| [`POSTGRES_MIGRATION_GUIDE.md`](./POSTGRES_MIGRATION_GUIDE.md) | SQLite → Postgres |
| CubeV xlsx under `docs/source/` | Authoritative routing / billing / SOURCE |

---

## 10. Emergency contacts (fill on transfer)

| Role | Name | Contact |
|---|---|---|
| Product owner (client) | | |
| Cosec lead (Sharon / Poh Li) | | |
| Engineering owner | | |
| Resend / Railway / Vercel billing owner | | |

---

## 11. Live acceptance walkthrough (run after the §7.1 close-out)

**First pass (19 Aug 2026):** recorded in [`UAT_LIVE_WALKTHROUGH_RESULTS.md`](./UAT_LIVE_WALKTHROUGH_RESULTS.md). Verdict: accepted with incomplete coverage. No product blockers on the screens that were driven. Not executed: F (MS1–MS7 twice), E2/E3 (fail-closed unrouted MOI), G1/G2/G4 (inbox approve-links), D2, I4–I6. MS4/MS7 Admin override was unused because no MOA was started. Live DB purged to a zero-row dry run afterwards.

Run this again after any AWS cut-over (especially after Resend is on a real domain). It is a scripted
pretend-to-be-a-real-user pass over the live stack. Work through it in a browser, not with curl —
the point is to catch what a person hits, not what the API returns.

Record results in `docs/UAT_LIVE_WALKTHROUGH_RESULTS.md` (format in §11.5) and
commit it. A scenario is not "passed" until someone has actually seen the expected screen.

### 11.1 Environment and accounts

- Frontend: `https://lgb-testing.vercel.app` — API: `https://lgbtesting-production-4d6b.up.railway.app`
- Admin: `ryannnism@gmail.com` (seeded Admin, full capability flags)
- Client: `ryannnism@berkeley.edu` (seeded live-test client)
- Staff without oversight rights: a live Cosec `User` row such as `nadia@lgb.test` (aliases, not CubeV
  addresses — §6.2). Use one to prove least privilege.

### 11.2 Ground rules

1. **Create only prefixed test data.** Every company, product, package, and user you create must start
   with `ZZ TEST`. This is not cosmetic: the §11.4 purge finds test data by that prefix and by the
   graph hanging off it, so anything named otherwise will be left behind in the live database. Never
   edit, approve, or delete a real customer, invoice, or workflow. Keep a running list as you go.
2. **Email delivery is sandboxed.** `Email__From` is still `onboarding@resend.dev`, so Resend only
   delivers to the account owner's inbox. A notification that does not arrive for `@lgb.com.my` or
   `@swmsb.com` is expected, not a bug. Verify those cases from the Railway logs instead: the
   notifier and `ApprovalActionTokenService` log the recipient and the issued link.
3. **Reminders are off on purpose** (`Reminders__SendEmail=false`). Absent reminder mail is not a bug.
4. Do not rotate secrets, change environment variables, or run migrations during the walkthrough.
5. The `auth` endpoints are rate limited. On a failed sign-in, wait rather than retrying in a loop.
6. Take a screenshot at each expected-result checkpoint and reference it in the findings file.

### 11.3 Scenarios

**A. Sign-in and first run**
1. Sign in as an account that was just reset — expect a forced "change password" screen before any
   app content. Set a new password and land on the dashboard.
2. Sign in with a wrong password — expect "Invalid email or password", no hint about which was wrong.
3. Sign in with an unknown email — expect the same generic message (no account enumeration).
4. Use "Forgot password" for the owner inbox — the code should arrive; complete the reset and sign in.
5. Leave a tab open, sign out in another, then act in the first — expect a clean redirect to sign-in,
   not a blank screen or a raw 401.

Bug if: a temporary password gets you into the app without a change prompt; any error names the field
that was wrong; the app white-screens on an expired session.

**B. Role boundaries**
1. As the client (`ryannnism@berkeley.edu`) confirm you only see Portal, My Packages, Team.
2. As a `User`-role staff account confirm Settings, Customers, and Products are not reachable, and that
   the Admin panels (Users, Billing reports, Unrouted MOI queue, Workflow config) are absent.
3. Try to reach an admin surface directly by URL as each of those two accounts.

Bug if: any client or non-oversight staff account can open an admin panel, see another company's data,
or load a customer list.

**C. Admin setup — build a company you can drive end to end**

The point of this scenario is that **every** MOA step resolves to an account you control, so scenario F
can walk MS1 to MS7 without ever signing in as a real LGB person.

1. Create `ZZ TEST HOLDINGS SDN BHD`. Set the division group to Bellworth or SWM (they have real MS5
   mandatory approvers, so the step is exercised rather than stalled — see §11.6 for LGB).
2. Create these accounts, all prefixed, and note which chain step each one covers:
   - `ZZ TEST Client Admin` (ClientAdmin on the test company) — covers MS2 as the MOI requester
   - `ZZ TEST Client Signatory` (ClientSignatory) — the client-visibility checks in D
   - `ZZ TEST MOI Approver` — covers MS3; set it as the company's MOI approval holder
   - `ZZ TEST MOA Approver` — covers MS5; put this name in the company's MOA approvers so it takes
     priority over the group list (`Customer.MoaApproversJson` beats the division group)
   - `ZZ TEST Cosec` — covers MS6, the mid-flight insert
   Give the internal ones the MOA approval capability so they can act on a step.
3. Set the company's account holders, LOA holders and MOI/MOA approver flags to those test accounts.
   Create a `ZZ TEST` product (the field is the package name) and attach a package.
4. Confirm each new account lands with `MustChangePassword` set.
5. In Settings → Users, use the key icon to reset a `ZZ TEST` user: the temporary password shows once,
   dismisses, and works for one sign-in that then forces a change. Try resetting your own account —
   expect a clear "use Change password" message, not a silent failure.

Only MS1, MS4 and MS7 cannot be bound to a test account by configuration: MS1 resolves by job title
across internal staff (the Admin you are signed in as already matches "Senior Manager, Company
Secretarial"), and MS4 / MS7 are `NamedUser` steps fixed to "Teh SW" and "Dato' Lim". Scenario F says
how to cover those without creating look-alike accounts.

Bug if: the temporary password panel persists after navigation, the password does not work, the reset
succeeds on your own account, or a capability flag you set does not survive a reload.

**D. Client journey**
1. As the `ZZ TEST` client admin, request a service from My Packages and add a team member.
2. As the client signatory, confirm you see only your own documents.

Bug if: a signatory sees another holder's documents, or a submitted request never appears for staff.

**E. MOI, including the fail-closed path (item 7)**
1. Submit an MOI for a company that has a matrix row — it should route to the mapped approver.
   Confirm MOI approval requires signing in (clause R5): there must be no approve-by-email link.
2. Submit an MOI for a `ZZ TEST` company with no matrix row and no company approver — expect a
   readable error in the modal footer explaining nobody can be routed to, and the form parked.
3. Clear it from Settings → Unrouted MOI queue by assigning an approver, then confirm it proceeds.
4. With LOA set, capture the last point of approval (T3) and confirm it is required before submit.

Bug if: an unroutable MOI silently advances past client approval, the error is a bare "400" or
"something went wrong", or the parked form never appears in the Admin queue.

**F. MOA chain MS1-MS7 — full coverage, twice**

Run the chain **two** times so both the conditional step and the bounce get a clean pass.

*Run 1 — no bank signatory, straight through:*
1. Start the MOA workflow and confirm the stage-1 broadcast (M1) reaches legal plus secretarial for
   that division group only, and nobody outside it.
2. Confirm MS4 (Teh SW) does **not** appear, because the company is not flagged bank signatory.
3. Approve in order, signing in as each holder: MS1 as the Admin (its job title matches the step),
   MS2 as `ZZ TEST Client Admin`, MS3 as `ZZ TEST MOI Approver`, MS5 as `ZZ TEST MOA Approver`.
4. Before MS7, insert `ZZ TEST Cosec` mid-flight (MS6 / C3). Confirm it lands directly after the active
   step, that later steps renumber rather than duplicating an order, and then approve it as that user.
5. MS7 is a `NamedUser` step fixed to "Dato' Lim". Cover it the honest way: capture a last point of
   approval on the MOI (T3) naming one of your test accounts, and confirm MS7 now shows that name
   instead of Dato' Lim and can be approved by that account. If no last point was captured, advance it
   with `POST /api/workflow-instances/moa/{moaFormId}/admin-override` as Admin instead.
6. Confirm the workflow reaches a completed state and the form reflects it.

*Run 2 — bank signatory, comment bounce and rejection:*
7. Flag the company as bank signatory, start a fresh MOA, and confirm MS4 now **does** appear.
8. Advance MS4 with the Admin override endpoint — "Teh SW" is a real person and must not be
   impersonated with a look-alike account. Note in your findings that MS4's own approval path is
   covered by unit tests rather than live.
9. At the next step, approve **with a comment** — expect the M5 bounce: all cosec notified, the step
   held rather than advanced.
10. Then reject a step and confirm the same bounce path with a rejection state, and that the chain can
    be resumed afterwards.
11. Try to approve a step while signed in as an account that is not its assignee — expect a refusal.

Bug if: a step advances despite a comment, the inserted step lands out of order, MS4 appears for the
non-bank-signatory run or is missing for the bank-signatory run, the captured last point does not
override Dato' Lim at MS7, a non-assignee can advance a step, or the override endpoint is reachable by
a non-Admin.

**G. Email action links (W4)**
1. Find a step whose recipient resolves to the owner inbox and open the approve link from the email.
2. Click the same link again — expect a used-token message.
3. Tamper with a character in the token — expect a friendly invalid-link page.
4. Open a link for a step that has already moved on — expect a clear "no longer required" message.

Bug if: any of these returns a stack trace, a raw JSON error, or double-applies the approval.

**H. Billing**
1. Create an invoice for the `ZZ TEST` company, issue it, and confirm the issued timestamp appears and
   the PDF downloads and opens.
2. Settings → Billing reports: download the current quarter as PDF and as CSV. Check the figures match
   the invoices you just created, including package value and quota used.
3. Request a quarter with no data — expect an empty but valid report, not an error.

Bug if: a report 500s, the PDF is corrupt, CSV columns are misaligned, or issuing an invoice twice
changes the timestamp.

**I. Things a real person does by accident**
1. Double-click every submit button — no duplicate records.
2. Refresh and use the browser back button mid-form — no lost session, no half-saved record.
3. Paste an apostrophe and a very long string into names (for example `Dato' Lim` and 300 characters).
4. Enter zero, a negative number, and a past date where quantities and dates are accepted.
5. Upload an oversized file and a wrong file type.
6. Approve the same step from two tabs at once — expect one success and one clear conflict message.
7. Resize to a phone width and confirm the main screens remain usable.

Bug if: a duplicate is created, an unhandled exception surfaces, a validation message is missing or
unreadable, or the layout traps a control off-screen.

**J. API is reachable but the browser is not signed in**
Open the app in a private window and hit a deep link — expect the sign-in screen, then a return to the
requested view after signing in.

### 11.4 Clean up — leave no trace

The walkthrough runs on the live database, so cleanup is mandatory, not optional. Use the purge tool
rather than deleting by hand: the ordinary delete endpoints cannot remove invoices, notifications,
reminder logs or action tokens, and an invoice will block the customer delete outright.

1. Dry run first, as Admin:
   `POST /api/admin/test-data/purge` — returns the companies matched and a count per table, and
   changes nothing.
2. Read the counts. They should match what you created. If a company you do not recognise appears,
   **stop** and investigate before applying.
3. Apply: `POST /api/admin/test-data/purge?apply=true`. It deletes invoices, notifications and reminder
   logs first, then the forms (cascading workflow instances, steps and action tokens), then jobs
   (cascading units, assignees, service job forms and documents), then test users and products, then
   the customer (cascading holders, packages, completed services and signatory access), and finally the
   uploaded files from the volume.
4. Dry run again — every count must be zero.
5. Revert by hand the things the purge deliberately does not touch, because they are shared config:
   any division group mandatory-approver edit, MOI approval matrix row, workflow template change, or
   billing party you added during the pass.
6. Walk the UI once more as Admin — Customers, Operations, Settings, Billing reports — and confirm no
   `ZZ TEST` row is visible anywhere and no real record was modified.

The two seeded live-test logins (`ryannnism@gmail.com`, `ryannnism@berkeley.edu`) are protected from
the purge on purpose. If you attached one of them to the test company, re-point it afterwards.

### 11.5 Findings format

The 19 Aug 2026 file already exists. For a later pass, update it (or append a dated section) with the SHA under test, who ran it, and one
entry per finding:

- **Scenario** (for example F4) and severity: blocker / major / minor / cosmetic
- **Steps to reproduce** as clicks, not API calls
- **Expected** vs **Actual**
- **Evidence**: screenshot path or the Railway log line
- **Verdict line at the end**: accepted, or accepted with the listed defects

### 11.6 Known non-bugs (do not raise these)

- Mail not arriving for anyone except the Resend account owner — sandbox sender, §7.1 item 2.
- No reminder emails — `Reminders__SendEmail=false`, §7.1 item 1.
- LGB Group MOA stalling at MS5 — CubeV never supplied LGB mandatory approvers, §7.1 item 8. Use the
  Start-MOA override to get past it during testing.
- MOI approval requiring sign-in — clause R5, by design, §7.1 item 9.
- Everyone signed out after the handover-day `Jwt__Key` rotation.

---

*End of handover. Prefer updating this file when close-out items in §7 flip to done rather than starting a new doc.*

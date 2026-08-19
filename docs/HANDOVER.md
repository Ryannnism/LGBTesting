# LGB Services — Product Handover

**Date:** 2026-08-19 (refreshed from live check)  
**Product:** LGB Services (MOI / MOA company-secretarial workflow)  
**Status:** **Pilot is up.** Vercel UI + Railway API + Postgres. Redeployed 19 Aug 2026 21:38 UTC (`0e55cc5`). Git `main` is ahead at `a8e6ca2` (docs / login copy / seeder fix — not yet on the API replica).  
**Handover reproduction path:** Terraform UAT (Lightsail + RDS SQL Server) in [`infra/terraform/uat/`](../infra/terraform/uat/README.md)  
**Canonical review trail:** [`SYSTEM_REVIEW_7_UX.md`](./SYSTEM_REVIEW_7_UX.md)  
**Audience:** next engineer, ops owner, or client admin taking ownership

This document is the single place for **what shipped**, **how to operate it**, **who gets which emails**, **where secrets live**, and **what must still be closed** so the product does not need open-ended future enhancement.

### Live check (2026-08-19, after Railway revival)

| Check | Result |
|---|---|
| `https://lgb-testing.vercel.app` | 200 — login UI |
| Wrong-password sign-in | API 401 `Invalid email or password.` shown in the form (CORS preflight 204 from Vercel origin) |
| `GET /api/health` | 200 `{ status: ok }` |
| Railway `LGBTesting` | SUCCESS, 1 replica, commit `0e55cc5` |
| Postgres | Data survived the outage: 169 customers, 779 MOIs, 1079 jobs, 281 users, 0 keyless tables |
| Schema | All 9 Postgres migrations applied including `Pg_RepairPgloaderSchema` (30 PK, 33 FK, 76 indexes) |
| `SEED_STAFF` | `false` |
| Reminders / From | Worker ticks every 15 min, log-only; `Email__From` is still the Resend sandbox sender |

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

HEAD: **`2aac242`**.

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

**Tests:** 143 passing at last full run. Always re-run before push.

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

1. Rotate `Email__ResendApiKey` in Resend + Railway.  
2. Rotate `Jwt__Key` (forces re-login for everyone).  
3. Confirm `SEED_STAFF=false`.  
4. Change seeded shared password / force password resets for Sharon, Poh Li, and test admins.  
5. Confirm no secrets in git history for this branch.

---

## 5. User guide (by role)

### 5.1 Sign-in

1. Open https://lgb-testing.vercel.app  
2. Sign in with the assigned email + password.  
3. First login after staff seed: change password when prompted (`MustChangePassword`).

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

| Name | Email | Role | Job |
|---|---|---|---|
| Sharon | `sharon@lgb.com.my` | Admin | Senior Manager, Company Secretarial |
| Ng Poh Li | `pohli.ng@taliworks.com.my` | Admin | Senior Manager, Company Secretarial |
| Nita | `dzatin.zaharuddin@taliworks.com.my` | User | Resolution preparation |
| Siti | `zalila.zainal@lgb.com.my` | User | Resolution preparation |
| Nadia | `nadia.rahman@taliworks.com.my` | User | Resolution preparation |
| Datin Raj | `raj@taliworks.com.my` | User | Group Legal (MOA approve + signatory) |
| Seet Mei | `seetmei.lee@taliworks.com.my` | User | Group Legal |
| Dee Nee | `deenee.ooi@taliworks.com.my` | User | Group Legal |
| Sutina | `sutina.sujeno@taliworks.com.my` | User | Group Legal |

Source: `LGBApp.Backend/Data/InternalStaffSeeder.cs`.

Legacy aliases (migrated on seed): `sharon@lgb.test` → Sharon, `ngpohli@lgb.test` → Poh Li, etc.

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

**As of 8 Aug 2026 the code side of CubeV close-out is finished.** As of 19 Aug 2026 the **live API replica is gone** — restore Railway before treating the pilot as usable. Ops items 1, 2, 10 remain, plus item 11.

### 7.1 Close-out checklist

Items 3–9 are **built and deployed** as of `2aac242` (8 Aug 2026); see [`SYSTEM_REVIEW_7_UX.md`](./SYSTEM_REVIEW_7_UX.md) §14 for the per-item trail. Only 1, 2 and 10 remain, and all three need the client's Resend account or DNS rather than code.

| # | Gap | Status | What is left |
|---|---|---|---|
| 1 | Reminder **emails** off | ⚠️ engine verified | `ReminderLogs` holds real R3/R4 rows and the worker ticks every 15 min, so the pipeline is proven. Set `Reminders__SendEmail=true` **after** item 2, then watch one live reminder. Flipping it first sends executive mail from a sandbox address that reaches nobody |
| 2 | Resend **from-domain** | ❌ blocked on DNS | Verify a real domain in Resend, point `Email__From` at it, retest one WorkflowNotifier send. Until then delivery is limited to the Resend account owner (§4.4) |
| 3 | **MS6 / C3** Cosec mid-flight insert | ✅ | — `WorkflowService.InsertCosecStepAsync` + chain UI control |
| 4 | **B5** quarterly billing report | ✅ | — `GET /api/reports/billing/quarterly?year=&quarter=&format=pdf\|csv`, Admin tab UI. No Finance role exists; the Finance Head uses an Admin account |
| 5 | **M1** stage-1 broadcast | ✅ | — legal + secretarial, scoped to LGB / Bellworth / SWM, notification-only |
| 6 | **T3 / M5** last-point-of-approval + bounce-on-comment | ✅ | — persisted on `MOIForm` and preferred by MS7; any approver comment bounces to all cosec and holds the step |
| 7 | Matrix **fail-open** | ✅ | — unrouted forms park and submit returns an actionable 400; clear them from the Admin queue. The company-approver fallback is deliberately kept |
| 8 | LGB Group **MS5 empty** | ✅ code, ops data pending | The list is now editable in Admin → Workflow config. **Enter the LGB names from CubeV before the first live LGB MOA** — do not invent them |
| 9 | MOI still **login-only** | ✅ conformant by spec | **Nothing to build.** Clause R5 requires MOI approvers to log in, so login-only is correct. Earlier entries treating this as a gap were wrong |
| 10 | Secret rotation | ❌ handover-day action | Rotate `Email__ResendApiKey` (Resend dashboard) and `Jwt__Key` (signs everyone out — pick the moment). `SEED_STAFF=false` is already set; 19 of 21 staff still carry `MustChangePassword`. See §4.5 |
| 11 | Live API replica | ✅ revived 19 Aug 2026 | Railway `LGBTesting` SUCCESS. Redeploy `a8e6ca2` when convenient so API matches git `main`. Take a Postgres dump before AWS. |

### 7.2 Explicitly out of scope (do not build)

- New standalone “workdone module” UI (importer into `CompletedServices` is enough)
- Full dual-entry accounting ledger
- Inventing emails not present in CubeV / matrix / staff seed
- Replacing Postgres with SQLite for production

### 7.3 Acceptance criteria for “no further enhancement”

The product is **done** when all of the following are true:

1. `Reminders__SendEmail=true` and at least one R3 and one M3 email observed in production. — **outstanding** (do after 2)
2. Resend sends from a verified org domain. — **outstanding**
3. ✅ MS6/C3 works in a live dry-run (Cosec inserts mid-flight; step applies).
4. ✅ Finance can download or receive a **3-monthly** billing report (B5).
5. ✅ Stage-1 broadcast (M1) matches flowchart for LGB / Bellworth / SWM.
6. ✅ T3/M5 last-point + bounce-on-comment behave per flowchart.
7. ✅ Matrix unmatched requesters cannot silently skip HOD (fail-closed or Admin path).
8. LGB MS5 mandatory list populated for companies that go live. — **data entry, editable in Admin**
9. Secrets rotated; `SEED_STAFF=false`; `dotnet test` green; both remotes on same `main` SHA. — `SEED_STAFF=false`, 143 tests green and both remotes on `2aac242`; **rotation outstanding**
10. ✅ This handover + SR7 (now §14) updated to mark each item.

**Also verify the schema after any Postgres restore or re-import.** A `drop indexes` load in July left production with no primary keys for three weeks, which surfaced only as an unrelated migration failure that blocked every deploy. `Pg_RepairPgloaderSchema` fixed it and the backend now warns on boot, but run the key-verification query in [`POSTGRES_MIGRATION_GUIDE.md`](./POSTGRES_MIGRATION_GUIDE.md) §8 acceptance if the database is ever reloaded.

After that: **operations and data only** (new companies, matrix row edits, password resets) — not feature work.

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
| Notifier | `Services/WorkflowNotifier.cs` |
| Dual DB migrations | Postgres EF + `SqliteSchemaMigrator` — **always update both** for schema changes |
| Postgres schema repair | `Migrations/Postgres/20260717095000_Pg_RepairPgloaderSchema.cs` — read its header before touching an imported database |
| Review / UX debt log | `docs/SYSTEM_REVIEW_7_UX.md` |

---

## 9. Related documents

| Doc | Use |
|---|---|
| [`SYSTEM_REVIEW_7_UX.md`](./SYSTEM_REVIEW_7_UX.md) | Full Review #7 + CubeV conformance trail |
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

*End of handover. Prefer updating this file when close-out items in §7 flip to done rather than starting a new doc.*

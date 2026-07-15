# Go live (simple path)

Frontend is already on Vercel. This gets the **API** online so Sign In works.

Skip Supabase for now. Use **Railway** (free trial / hobby) + SQLite — good enough to go live and demo.

## 1. Deploy the API (Railway)

1. Go to [railway.app](https://railway.app) → login with GitHub  
2. **New Project** → **Deploy from GitHub repo** → `Ryannnism/LGBTesting`  
3. It should pick up the root `Dockerfile`  
4. Attach a **Volume** at `/data` for uploads (`LGB_UPLOAD_ROOT=/data/uploads`)  
5. Add a **Postgres** plugin (managed). Cutover steps: [`POSTGRES_MIGRATION_GUIDE.md`](../POSTGRES_MIGRATION_GUIDE.md).  
6. **Variables** (Settings → Variables):

```
ASPNETCORE_ENVIRONMENT=Production
Database__Provider=Postgres
ConnectionStrings__DefaultConnection=Host=postgres.railway.internal;Port=5432;Database=railway;Username=postgres;Password=<from Postgres plugin>;SSL Mode=Disable
# (or paste Postgres DATABASE_URL if you prefer URI form — Npgsql accepts both)
Jwt__Key=<paste-a-long-random-string-at-least-32-chars>
Jwt__Issuer=LGBApp.Backend
Jwt__Audience=LGBApp.Frontend
Cors__AllowedOrigins__0=https://lgb-testing.vercel.app
DISABLE_HTTPS_REDIRECTION=true
AllowedHosts=*
App__PublicFrontendUrl=https://lgb-testing.vercel.app
Email__From=LGB Services <noreply@your-verified-domain.com>
Email__ResendApiKey=<your-resend-api-key>
LGB_UPLOAD_ROOT=/data/uploads
```

> **Live (beneficial-vitality / LGBTesting)** already runs `Database__Provider=Postgres` against the Railway Postgres plugin. SQLite file remains on the volume as rollback only.

6. **Settings → Networking → Generate Domain**  
   Copy the URL, e.g. `https://lgbtesting-production-xxxx.up.railway.app`

First boot seeds staff + product catalog only. After staff exist you can remove `SEED_STAFF` / `SEED_STAFF_PASSWORD`.

To import the full CubeV customer book (heavy — run once via Railway shell, not on every deploy):

```
dotnet LGBApp.Backend.dll seed-full
```

`Cors__AllowedOrigins__0` is **required** in Production (app refuses to boot without it).

### Email (forgot password OTP + MOI/MOA alerts)

- Create a free [Resend](https://resend.com) account, verify a sending domain (or use `onboarding@resend.dev` only for testing to your own inbox).
- Set `Email__ResendApiKey` and `Email__From` as above.
- **Without** `Email__ResendApiKey`, the API still works: OTP codes and approval emails are **written to Railway logs** only (logging sink). Useful for local/dev.

## 2. Point Vercel at the API

1. Vercel → project → **Settings → Environment Variables**  
2. Set:

```
VITE_API_BASE=https://YOUR-RAILWAY-URL.up.railway.app
```

(no trailing slash)

3. **Redeploy** the frontend  

## 3. Smoke test

1. Open https://lgb-testing.vercel.app  
2. Sign in with a seeded account (SQLite seeds staff), e.g. `sharon@lgb.test` / `password123`  
3. You’ll be asked to change password on first login  
4. **Forgot password:** request a code → check email (or Railway logs if Resend is unset) → reset  
5. Push an MOI/MOA for client approval → signatory gets in-app + email notice  

## Optional: Postgres instead of SQLite

The API now supports `Database__Provider=Postgres` (Npgsql). Schema is created by
`Migrations/Postgres/Pg_Baseline` (SQLite migrations stay for local/Railway until cutover).
Full cutover steps (data copy via pgloader, UTC DateTimes, rollback): see
[`docs/POSTGRES_MIGRATION_GUIDE.md`](../POSTGRES_MIGRATION_GUIDE.md).

Example Railway variables after a managed Postgres is attached:

```
Database__Provider=Postgres
ConnectionStrings__DefaultConnection=Host=xxx;Port=5432;Database=lgbapp;Username=…;Password=…;SSL Mode=Require
```

Keep the `/data` volume for uploads (`LGB_UPLOAD_ROOT=/data/uploads`) — files are not in the DB.

## Later (real production)

- Cut over DB to managed Postgres (guide above) or Azure SQL  
- Host API on Azure App Service  
- See [DEPLOYMENT.md](../DEPLOYMENT.md)  

Supabase can wait until you want storage/auth migration — not needed to go live.

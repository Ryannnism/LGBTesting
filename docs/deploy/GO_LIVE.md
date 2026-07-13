# Go live (simple path)

Frontend is already on Vercel. This gets the **API** online so Sign In works.

Skip Supabase for now. Use **Railway** (free trial / hobby) + SQLite — good enough to go live and demo.

## 1. Deploy the API (Railway)

1. Go to [railway.app](https://railway.app) → login with GitHub  
2. **New Project** → **Deploy from GitHub repo** → `Ryannnism/LGBTesting`  
3. It should pick up the root `Dockerfile`  
4. Add a **Volume** mounted at `/data` (keeps the SQLite DB)  
5. **Variables** (Settings → Variables):

```
ASPNETCORE_ENVIRONMENT=Production
Database__Provider=Sqlite
ConnectionStrings__DefaultConnection=Data Source=/data/lgbapp.db
Jwt__Key=<paste-a-long-random-string-at-least-32-chars>
Jwt__Issuer=LGBApp.Backend
Jwt__Audience=LGBApp.Frontend
Cors__AllowedOrigins__0=https://lgb-testing.vercel.app
DISABLE_HTTPS_REDIRECTION=true
AllowedHosts=*
```

6. **Settings → Networking → Generate Domain**  
   Copy the URL, e.g. `https://lgbtesting-production-xxxx.up.railway.app`

First boot may take 1–2 minutes while it seeds the DB.

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

## Later (real production)

- Move DB to Azure SQL / Postgres  
- Host API on Azure App Service  
- See [DEPLOYMENT.md](../DEPLOYMENT.md)  

Supabase can wait until you want storage/auth migration — not needed to go live.

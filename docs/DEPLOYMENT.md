# Production deployment guide

This document is the go-live runbook for LGB Services: moving from local SQLite dev to a hosted environment where many internal staff and client users can access MOI/MOA workflows reliably.

## Architecture overview

```
                         HTTPS
                           │
                    ┌──────▼──────┐
                    │ Nginx / CDN │
                    │  (optional) │
                    └──────┬──────┘
           ┌─────────────────┼─────────────────┐
           │                 │                 │
    React static build   /api/* proxy      TLS cert
    (dist/)                  │
                    ┌────────▼────────┐
                    │  .NET 8 API     │
                    └────────┬────────┘
              ┌──────────────┼──────────────┐
              │              │              │
        SQL Server      uploads/       JWT + DB
        (Azure SQL)     persistent     secrets
                          disk
```

**Recommended for first production:** one API instance, Azure SQL (or SQL Server), persistent disk for uploads, single custom domain with nginx serving the UI and proxying `/api`.

This app is workflow-heavy (forms, approvals, PDF uploads), not a high-traffic consumer site. A modest single-server setup comfortably supports many concurrent LGB and client users.

---

## What changes from local dev

| Concern | Development | Production |
|---------|-------------|------------|
| Database | SQLite (`lgbapp-dev.db`) | **SQL Server** via EF migrations |
| Demo data | Auto-seeded (Acme Corp, test users) | **Not seeded** — create real customers and users |
| Reference data | Form templates, workflows, catalog | **Auto-seeded on startup** (idempotent) |
| API host | `http://localhost:5003` | HTTPS behind reverse proxy |
| Frontend | Vite dev server `:5173` | `npm run build` → static files |
| Uploads | `LGBApp.Backend/uploads/` (dev) | Set `LGB_UPLOAD_ROOT` to persistent storage (Docker/Railway: `/data/uploads`) |
| JWT | Dev key in `appsettings.json` | Long random secret via env vars |
| CORS | Any origin (dev) | Restricted to your frontend URL |
| Swagger | Enabled | Disabled (`IsDevelopment` only) |

---

## Prerequisites

- .NET 8 SDK (build machine)
- Node.js 18+ (build frontend)
- SQL Server database (Azure SQL recommended)
- Linux VM or Azure App Service (or similar) with HTTPS
- Domain name and TLS certificate (Let's Encrypt or platform-managed)

---

## 1. Provision SQL Server

Create an empty database, e.g. `LGBApp`.

Connection string format (Azure SQL):

```
Server=tcp:YOUR_SERVER.database.windows.net,1433;Database=LGBApp;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=False;
```

On first API start with `Database:Provider` = `SqlServer`, the app will:

1. Run EF Core migrations (`context.Database.Migrate()`)
2. Seed MOI/MOA form templates, workflow definitions, and division groups (only if tables are empty)
3. Sync the product/service catalog

It will **not** create demo customers or `*.test` users.

---

## 2. Configure secrets

Copy and customize:

- [`LGBApp.Backend/appsettings.Production.json`](../LGBApp.Backend/appsettings.Production.json) — template only; override secrets via environment variables
- [`docs/deploy/azure-app-service.env.example`](deploy/azure-app-service.env.example) — Azure App Service keys
- [`LGBApp.Frontend/.env.production.example`](../LGBApp.Frontend/.env.production.example) — frontend API URL

### Required environment variables

| Variable | Purpose |
|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__Provider` | `SqlServer` |
| `ConnectionStrings__DefaultConnection` | SQL connection string |
| `Jwt__Key` | Random secret, **at least 32 characters** |
| `Jwt__Issuer` | `LGBApp.Backend` (default) |
| `Jwt__Audience` | `LGBApp.Frontend` (default) |
| `Cors__AllowedOrigins__0` | `https://your-frontend-domain` (when UI and API are on different origins) |

ASP.NET Core maps `__` in env vars to `:` in configuration.

**Never commit production passwords or JWT keys to git.**

---

## 3. Build and publish the API

```bash
cd LGBApp.Backend
dotnet publish -c Release -o ./publish
```

Deploy the `publish/` folder to your server (e.g. `/var/www/lgbapp/api`).

Example systemd unit: [`docs/deploy/lgbapp-api.service.example`](deploy/lgbapp-api.service.example)

Verify locally against SQL before going public:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export Database__Provider=SqlServer
export ConnectionStrings__DefaultConnection="Server=...;Database=LGBApp;..."
export Jwt__Key="$(openssl rand -base64 48)"
dotnet run
```

---

## 4. Build the frontend

### Option A — Same domain (recommended)

Nginx serves `dist/` and proxies `/api` to the backend. Users see one URL; no CORS configuration needed if everything is same-origin.

```bash
cd LGBApp.Frontend
npm ci
npm run build
# Deploy dist/ to /var/www/lgbapp/frontend
```

Leave `VITE_API_BASE` empty (default).

### Option B — Separate API subdomain

```bash
VITE_API_BASE=https://api.example.com npm run build
```

Set `Cors__AllowedOrigins__0=https://app.example.com` on the API.

---

## 5. Reverse proxy (nginx)

Full example: [`docs/deploy/nginx.conf.example`](deploy/nginx.conf.example)

Key points:

- `client_max_body_size 50m` — MOI/MOA PDF attachments
- Proxy `/api/` to `http://127.0.0.1:5003`
- Serve `dist/` as static files
- Terminate TLS at nginx (or Cloudflare / load balancer)

---

## 6. Persistent file uploads

Uploaded documents are stored on disk at:

```
{LGBApp.Backend content root}/uploads/job-items/{jobId}/{moi|moa|supporting}/...
```

**Requirements:**

- The `uploads/` directory must survive deploys and restarts
- If you run **more than one API instance**, all instances must share the same storage (NFS, Azure Files mount, or future blob storage)

### Azure App Service

Mount Azure Files to `/home/uploads` and symlink or configure the app content root accordingly. See [Azure Files for App Service](https://learn.microsoft.com/azure/app-service/configure-connect-to-azure-storage).

### VPS

Keep `uploads/` outside the deploy folder and symlink:

```bash
ln -s /var/data/lgbapp/uploads /var/www/lgbapp/api/uploads
```

**Back up `uploads/`** on the same schedule as the database.

---

## 7. Create the first admin user

Production does not seed Sharon or other internal accounts. After migrations:

1. **Preferred:** Use your admin UI once bootstrap access exists — `POST /api/users` (requires an authenticated Admin).
2. **Bootstrap:** Temporarily run the API with a one-off script or insert the first Admin via SQL using the app's password hasher (see placeholder [`docs/deploy/create-first-admin.sql.example`](deploy/create-first-admin.sql.example)).
3. Create remaining LGB staff and client company users through the CRM/admin flows in the app.

Do not use dev passwords (`password123`) in production.

---

## 8. Azure App Service quick path

1. **Azure SQL** — create server + database
2. **App Service** — Linux, .NET 8, deploy `publish/` output (GitHub Actions, zip deploy, or `az webapp deploy`)
3. **Application settings** — copy from [`azure-app-service.env.example`](deploy/azure-app-service.env.example)
4. **Static Web Apps** or **second App Service** — host `LGBApp.Frontend/dist`
5. **Custom domains** + managed certificates on both
6. **Azure Files** — optional mount for `uploads/`

---

## 9. Security checklist

- [ ] `Jwt__Key` is a unique, long random value
- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] HTTPS only (HSTS via nginx or platform)
- [ ] `Cors:AllowedOrigins` lists only your real frontend URL (when split deploy)
- [ ] SQL firewall allows only your API hosts
- [ ] No demo `@*.test` accounts in production DB
- [ ] Database and `uploads/` backups enabled
- [ ] `appsettings.*.local.json` and `.env` files are not committed

---

## 10. Smoke test after deploy

1. Log in as production Admin
2. Create a customer and client admin user
3. Client: open MOI draft → attach PDF → confirm file appears immediately
4. Client: submit MOI for approval
5. Internal: Sharon intake approve → MOA chain → client MOA approve
6. Download an uploaded document from the item folder panel
7. Restart API — confirm uploads and logins still work

---

## 11. Scaling later

| Symptom | Action |
|---------|--------|
| Slow queries | Upgrade SQL tier; review indexes |
| High API CPU | Second instance + load balancer |
| Multiple API instances | **Must** move uploads to shared disk or blob storage |
| Large files | Increase `client_max_body_size`; consider blob + CDN |

---

## 12. CI/CD (optional next step)

Not included in the repo yet. Typical pipeline:

1. `dotnet publish` + `npm run build` on merge to `main`
2. Run EF migrations (or let API migrate on startup)
3. Deploy API artifact + `dist/` to staging
4. Run smoke tests
5. Promote to production

---

## File reference

| File | Purpose |
|------|---------|
| [`appsettings.Production.json`](../LGBApp.Backend/appsettings.Production.json) | Production config template |
| [`nginx.conf.example`](deploy/nginx.conf.example) | Single-domain nginx |
| [`lgbapp-api.service.example`](deploy/lgbapp-api.service.example) | systemd unit |
| [`azure-app-service.env.example`](deploy/azure-app-service.env.example) | Azure settings |
| [`.env.production.example`](../LGBApp.Frontend/.env.production.example) | Frontend API base URL |

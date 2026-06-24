# AWS UAT — Lightsail VM + RDS SQL Server

UAT stack for LGB Services:

- **Terraform** — provision Lightsail + RDS — [`infra/terraform/uat/README.md`](../../infra/terraform/uat/README.md)
- **GitHub Actions** — test, build zip artifact, optional deploy to Lightsail
- **Amazon Lightsail** — Linux VM (nginx + .NET 8 API)
- **Amazon RDS for SQL Server** — managed database, EF migrations on API startup

## 0. Provision with Terraform (recommended)

```bash
cd infra/terraform/uat
cp terraform.tfvars.example terraform.tfvars   # set admin_ssh_cidrs to your IP/32
terraform init && terraform apply
terraform output -raw lightsail_static_ip
terraform output -raw connection_string
```

Use outputs for GitHub secrets and `/etc/lgbapp/lgbapp.env`. Manual RDS/Lightsail steps below are optional if you use Terraform.

---

## Architecture

```
Internet → Lightsail (nginx :443)
              ├── /          → React static (dist)
              └── /api/*     → Kestrel :5003 (.NET API)
                                    ↓
                           RDS SQL Server (SG: Lightsail IP only)
```

Uploads (MOI/MOA PDFs) live on the Lightsail disk at `/var/data/lgbapp/uploads` (symlinked into the API).

---

## 1. RDS SQL Server (manual — skip if using Terraform)

1. In **AWS RDS**, create **Microsoft SQL Server** (Express or Standard; smallest tier is fine for UAT).
2. Note the **endpoint**, port `1433`, master username/password.
3. Create database `LGBApp` (or let the app create tables via EF migrate on first start — empty DB is enough).
4. **Security group**: allow inbound **1433** only from your Lightsail instance private IP / VPC peering.
   - Simplest UAT: put Lightsail and RDS in the same region; use RDS **public** access **only** if you must, locked to the Lightsail static IP.
   - Better: Lightsail VPC peering + private RDS (no public RDS).

Connection string (see [`lightsail.env.example`](lightsail.env.example)):

```
Server=your-instance.region.rds.amazonaws.com,1433;Database=LGBApp;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;
```

On first API start with `Database__Provider=SqlServer`, the app runs `Database.Migrate()` and seeds templates/catalog (not demo customers).

---

## 2. Lightsail VM (one-time setup)

**Instance:** Ubuntu 22.04+, 2 GB RAM minimum, static IP attached.

### Install runtime

```bash
sudo apt update
sudo apt install -y nginx unzip rsync

# .NET 8 runtime — https://learn.microsoft.com/dotnet/core/install/linux-ubuntu
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install -y aspnetcore-runtime-8.0
```

### App layout

```bash
sudo mkdir -p /var/www/lgbapp/{api,frontend,scripts,uploads}
sudo mkdir -p /var/data/lgbapp/uploads
sudo ln -sfn /var/data/lgbapp/uploads /var/www/lgbapp/api/uploads
sudo chown -R www-data:www-data /var/www/lgbapp /var/data/lgbapp
```

### Environment + systemd

```bash
sudo mkdir -p /etc/lgbapp
sudo cp docs/deploy/lightsail.env.example /etc/lgbapp/lgbapp.env
sudo nano /etc/lgbapp/lgbapp.env   # RDS connection string, JWT key, domain
sudo chmod 600 /etc/lgbapp/lgbapp.env
sudo cp docs/deploy/lgbapp-api.service.example /etc/systemd/system/lgbapp-api.service
sudo systemctl daemon-reload
sudo systemctl enable lgbapp-api
```

Copy deploy script once (or it arrives in each release zip):

```bash
sudo cp scripts/deploy-lightsail-remote.sh /var/www/lgbapp/scripts/
sudo chmod 755 /var/www/lgbapp/scripts/deploy-lightsail-remote.sh
```

### nginx

```bash
sudo cp docs/deploy/nginx.conf.example /etc/nginx/sites-available/lgbapp
# Edit server_name, ssl paths, root /var/www/lgbapp/frontend
sudo ln -s /etc/nginx/sites-available/lgbapp /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

TLS: `sudo apt install certbot python3-certbot-nginx && sudo certbot --nginx -d uat.yourdomain.com`

### First admin

Production/UAT does **not** seed `sharon@lgb.test`. Create the first Admin via your app after deploy, or see [`create-first-admin.sql.example`](create-first-admin.sql.example).

---

## 3. GitHub Actions

Workflow: [`.github/workflows/uat.yml`](../../.github/workflows/uat.yml)

| Trigger | What happens |
|---------|----------------|
| Push to **`uat`** branch | Test → build zip → **deploy** to Lightsail |
| **Actions → UAT Build & Deploy → Run workflow** | Test → build; deploy if you check “Deploy” |

### GitHub secrets (repo → Settings → Secrets)

| Secret | Value |
|--------|--------|
| `UAT_SSH_HOST` | Lightsail static IP or hostname |
| `UAT_SSH_USER` | `ubuntu` (or your SSH user) |
| `UAT_SSH_PRIVATE_KEY` | Private key matching Lightsail SSH key pair |

### GitHub environment (optional)

Create environment **`uat`** under Settings → Environments for approval gates before deploy.

### Artifact

Every build uploads **`lgbapp-uat-release`** zip (30-day retention). Download from the Actions run if you want to deploy manually:

```bash
scp out/lgbapp-release.zip ubuntu@YOUR_IP:/tmp/
ssh ubuntu@YOUR_IP 'sudo bash /var/www/lgbapp/scripts/deploy-lightsail-remote.sh /tmp/lgbapp-release.zip'
```

---

## 4. Local build (without CI)

```bash
chmod +x scripts/*.sh
./scripts/package-release.sh
# → out/lgbapp-release.zip
```

---

## 5. Smoke test

1. `https://uat.yourdomain.com` loads the UI
2. Login as Admin
3. Create customer → MOI → intake → MOA
4. Upload a PDF on a form
5. `sudo systemctl restart lgbapp-api` — data and uploads still present

---

## 6. Branch workflow suggestion

| Branch | Purpose |
|--------|---------|
| `main` | CI tests only ([`ci.yml`](../../.github/workflows/ci.yml)) |
| `uat` | Build + auto-deploy to Lightsail UAT |

Merge `main` → `uat` when you want to ship to UAT.

---

## Cost ballpark (UAT)

- Lightsail $10–20/mo (small instance)
- RDS SQL Server — most expensive piece; use smallest Express/Web tier for UAT
- Static IP included with Lightsail

---

## Related files

| File | Purpose |
|------|---------|
| [`lightsail.env.example`](lightsail.env.example) | API environment template |
| [`lgbapp-api.service.example`](lgbapp-api.service.example) | systemd unit |
| [`nginx.conf.example`](nginx.conf.example) | Reverse proxy |
| [`scripts/build-release.sh`](../../scripts/build-release.sh) | CI/local build |
| [`scripts/deploy-lightsail-remote.sh`](../../scripts/deploy-lightsail-remote.sh) | VM deploy script |

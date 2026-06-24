# LGB UAT — Terraform (Lightsail + RDS SQL Server)

Infrastructure as code for the AWS UAT environment described in [`docs/deploy/aws-lightsail-uat.md`](../../../docs/deploy/aws-lightsail-uat.md).

## What this creates

| Resource | Purpose |
|----------|---------|
| Lightsail static IP | Stable IP for DNS + GitHub `UAT_SSH_HOST` |
| Lightsail Ubuntu VM | nginx + .NET API (configured manually or via deploy workflow) |
| Lightsail SSH key pair | Deploy key for GitHub Actions |
| RDS SQL Server Express | App database (`LGBApp`) |
| Security group | Port **1433** only from Lightsail static IP |

**UAT trade-off:** RDS is `publicly_accessible = true` but the security group allows **only** the Lightsail IP — not the open internet. For production, use private RDS + VPC peering.

## Prerequisites

1. [Terraform](https://developer.hashicorp.com/terraform/install) **1.5+**
2. AWS account with permissions for Lightsail + RDS + EC2 (VPC/SG)
3. AWS CLI credentials configured (`aws configure` or environment variables)

```bash
aws sts get-caller-identity
```

## Quick start

```bash
cd infra/terraform/uat

cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars — set admin_ssh_cidrs to YOUR_IP/32

terraform init
terraform plan
terraform apply
```

After apply, save sensitive outputs:

```bash
terraform output -raw lightsail_ssh_private_key_pem > ~/.ssh/lgb-uat.pem
chmod 600 ~/.ssh/lgb-uat.pem

terraform output -raw connection_string
# → use in /etc/lgbapp/lgbapp.env on the VM
```

### GitHub Actions secrets

| Secret | Source |
|--------|--------|
| `UAT_SSH_HOST` | `terraform output -raw lightsail_static_ip` |
| `UAT_SSH_USER` | `ubuntu` |
| `UAT_SSH_PRIVATE_KEY` | contents of `lgb-uat.pem` |

## Post-terraform VM setup

Terraform does **not** install nginx, .NET, or deploy the app. On the Lightsail instance:

1. SSH: `ssh -i ~/.ssh/lgb-uat.pem ubuntu@STATIC_IP`
2. Follow **[aws-lightsail-uat.md](../../../docs/deploy/aws-lightsail-uat.md)** §2 (runtime, nginx, systemd)
3. Put `connection_string` output into `/etc/lgbapp/lgbapp.env`
4. Deploy app via GitHub Actions (`uat` branch) or manual zip

## Remote state (recommended for teams)

Uncomment the `backend "s3"` block in `versions.tf`, create:

```bash
aws s3 mb s3://lgb-terraform-state --region ap-southeast-1
aws dynamodb create-table \
  --table-name lgb-terraform-locks \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --region ap-southeast-1
```

Then `terraform init -migrate-state`.

## Variables

See [`variables.tf`](variables.tf) and [`terraform.tfvars.example`](terraform.tfvars.example).

| Variable | Default | Notes |
|----------|---------|-------|
| `aws_region` | `ap-southeast-1` | Same region for Lightsail + RDS |
| `lightsail_bundle_id` | `small_3_0` | 2 GB RAM |
| `admin_ssh_cidrs` | `0.0.0.0/0` | **Change this** before apply |
| `db_engine_version` | SQL Server 2019 Express | Check AWS for latest in your region |

## Destroy

```bash
terraform destroy
```

Removes Lightsail instance, static IP, RDS instance, and security group. **RDS skip_final_snapshot = true** — data is not retained.

## Troubleshooting

| Issue | Fix |
|-------|-----|
| No default VPC | Create a VPC or switch to explicit VPC module |
| RDS engine version not found | Run `aws rds describe-db-engine-versions --engine sqlserver-ex` and update `db_engine_version` |
| Cannot connect from VM to RDS | Re-apply after Lightsail IP is attached; verify SG uses `/32` of static IP |
| SQL login fails | Use outputs `rds_username` / `rds_password`; SQL Server logins are not `admin` on Linux — use `lgbadmin` default |

## Cost note

RDS SQL Server is the main cost driver. Lightsail `small_3_0` is ~$12/mo; RDS `db.t3.micro` SQL Express is significantly more — review AWS pricing before `apply`.

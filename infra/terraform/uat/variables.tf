variable "aws_region" {
  description = "AWS region for Lightsail and RDS (use the same region for both)."
  type        = string
  default     = "ap-southeast-1"
}

variable "project_name" {
  description = "Prefix for resource names."
  type        = string
  default     = "lgb"
}

variable "environment" {
  description = "Environment label (uat, staging, prod)."
  type        = string
  default     = "uat"
}

variable "lightsail_bundle_id" {
  description = "Lightsail instance size. Examples: small_3_0 (2GB), medium_3_0 (4GB)."
  type        = string
  default     = "small_3_0"
}

variable "lightsail_blueprint_id" {
  description = "Lightsail OS image."
  type        = string
  default     = "ubuntu_22_04"
}

variable "admin_ssh_cidrs" {
  description = "CIDRs allowed to SSH to the Lightsail instance (your IP/32 recommended)."
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

variable "db_name" {
  description = "SQL Server database name created on RDS."
  type        = string
  default     = "LGBApp"
}

variable "db_username" {
  description = "RDS SQL Server master login (SQL Server reserved names like 'admin' are allowed on RDS)."
  type        = string
  default     = "lgbadmin"
}

variable "db_allocated_storage_gb" {
  description = "RDS allocated storage in GB (SQL Server minimum is 20)."
  type        = number
  default     = 20
}

variable "db_engine_version" {
  description = "RDS SQL Server Express engine version."
  type        = string
  default     = "15.00.4316.3.v1"
}

variable "db_publicly_accessible" {
  description = "UAT: true so Lightsail can reach RDS without VPC peering. Lock SG to Lightsail IP only."
  type        = bool
  default     = true
}

variable "db_backup_retention_days" {
  description = "RDS backup retention (0 disables backups — not recommended even for UAT)."
  type        = number
  default     = 3
}

variable "deletion_protection" {
  description = "Prevent accidental RDS deletion."
  type        = bool
  default     = false
}

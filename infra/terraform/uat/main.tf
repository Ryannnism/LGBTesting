locals {
  common_tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }

  # Lightsail AZ must be a single letter suffix in the chosen region (e.g. ap-southeast-1a).
  lightsail_az = "${var.aws_region}a"
}

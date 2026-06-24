output "lightsail_static_ip" {
  description = "Attach this IP to your domain / GitHub secret UAT_SSH_HOST."
  value       = aws_lightsail_static_ip.uat.ip_address
}

output "lightsail_instance_name" {
  value = aws_lightsail_instance.uat.name
}

output "lightsail_ssh_user" {
  description = "Default SSH user for Ubuntu Lightsail images."
  value       = "ubuntu"
}

output "lightsail_ssh_private_key_pem" {
  description = "Save as deploy key; add to GitHub secret UAT_SSH_PRIVATE_KEY."
  value       = aws_lightsail_key_pair.uat.private_key
  sensitive   = true
}

output "rds_endpoint" {
  description = "RDS hostname (no port)."
  value       = aws_db_instance.uat.address
}

output "rds_port" {
  value = aws_db_instance.uat.port
}

output "rds_database_name" {
  value = var.db_name
}

output "rds_username" {
  value = var.db_username
}

output "rds_password" {
  description = "Store in /etc/lgbapp/lgbapp.env — do not commit."
  value       = random_password.db.result
  sensitive   = true
}

output "connection_string" {
  description = "Paste into ConnectionStrings__DefaultConnection on the VM."
  value       = "Server=${aws_db_instance.uat.address},${aws_db_instance.uat.port};Database=${var.db_name};User Id=${var.db_username};Password=${random_password.db.result};Encrypt=True;TrustServerCertificate=True;"
  sensitive   = true
}

output "github_actions_secrets_hint" {
  description = "Values to configure in GitHub repository secrets."
  value = {
    UAT_SSH_HOST = aws_lightsail_static_ip.uat.ip_address
    UAT_SSH_USER = "ubuntu"
  }
}

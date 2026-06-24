resource "random_password" "db" {
  length  = 24
  special = true
  # SQL Server passwords cannot contain /, ", or @ in some contexts.
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

resource "aws_db_subnet_group" "uat" {
  name       = "${var.project_name}-${var.environment}-sql"
  subnet_ids = data.aws_subnets.default.ids

  tags = merge(local.common_tags, {
    Name = "${var.project_name}-${var.environment}-sql"
  })
}

resource "aws_db_instance" "uat" {
  identifier = "${var.project_name}-${var.environment}-sql"

  engine         = "sqlserver-ex"
  engine_version = var.db_engine_version
  license_model  = "license-included"

  instance_class    = "db.t3.micro"
  allocated_storage = var.db_allocated_storage_gb
  storage_type      = "gp3"

  db_name  = var.db_name
  username = var.db_username
  password = random_password.db.result

  db_subnet_group_name   = aws_db_subnet_group.uat.name
  vpc_security_group_ids = [aws_security_group.rds_sql.id]

  publicly_accessible = var.db_publicly_accessible
  multi_az            = false

  backup_retention_period = var.db_backup_retention_days
  skip_final_snapshot     = true
  deletion_protection     = var.deletion_protection

  apply_immediately = true

  tags = merge(local.common_tags, {
    Name = "${var.project_name}-${var.environment}-sql"
  })
}

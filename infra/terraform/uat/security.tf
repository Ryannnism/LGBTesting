data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

resource "aws_security_group" "rds_sql" {
  name        = "${var.project_name}-${var.environment}-rds-sql"
  description = "RDS SQL Server for LGB ${var.environment} — Lightsail static IP only"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    description = "SQL Server from Lightsail UAT"
    from_port   = 1433
    to_port     = 1433
    protocol    = "tcp"
    cidr_blocks = ["${aws_lightsail_static_ip.uat.ip_address}/32"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, {
    Name = "${var.project_name}-${var.environment}-rds-sql"
  })
}

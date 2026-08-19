resource "aws_lightsail_key_pair" "uat" {
  name = "${var.project_name}-${var.environment}-key"
}

resource "aws_lightsail_static_ip" "uat" {
  name = "${var.project_name}-${var.environment}-ip"
}

resource "aws_lightsail_instance" "uat" {
  name              = "${var.project_name}-${var.environment}"
  availability_zone = local.lightsail_az
  blueprint_id      = var.lightsail_blueprint_id
  bundle_id         = var.lightsail_bundle_id
  key_pair_name     = aws_lightsail_key_pair.uat.name

  tags = local.common_tags
}

resource "aws_lightsail_static_ip_attachment" "uat" {
  static_ip_name = aws_lightsail_static_ip.uat.name
  instance_name  = aws_lightsail_instance.uat.name
}

resource "aws_lightsail_instance_public_ports" "uat" {
  instance_name = aws_lightsail_instance.uat.name

  port_info {
    protocol  = "tcp"
    from_port = 22
    to_port   = 22
    cidrs     = var.admin_ssh_cidrs
  }

  port_info {
    protocol  = "tcp"
    from_port = 80
    to_port   = 80
    cidrs     = ["0.0.0.0/0"]
  }

  port_info {
    protocol  = "tcp"
    from_port = 443
    to_port   = 443
    cidrs     = ["0.0.0.0/0"]
  }
}

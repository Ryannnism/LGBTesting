terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Uncomment after creating the S3 bucket and DynamoDB table (see README).
  # backend "s3" {
  #   bucket         = "lgb-terraform-state"
  #   key            = "uat/terraform.tfstate"
  #   region         = "ap-southeast-1"
  #   dynamodb_table = "lgb-terraform-locks"
  #   encrypt        = true
  # }
}

provider "aws" {
  region = var.aws_region
}

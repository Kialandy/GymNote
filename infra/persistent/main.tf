terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region = "ap-southeast-1"

  # Mọi resource tạo ra đều tự gắn các tag này, giúp tìm resource bị sót.
  default_tags {
    tags = {
      Project   = "gymnote"
      Layer     = "persistent"
      ManagedBy = "terraform"
    }
  }
}

locals {
  repositories = ["gymnote-backend", "gymnote-frontend"]
}

resource "aws_ecr_repository" "app" {
  for_each             = toset(local.repositories)
  name                 = each.key
  image_tag_mutability = "MUTABLE"
  force_delete         = true # cho phép xóa repo dù còn image, tiện lúc dọn dẹp cuối dự án

  image_scanning_configuration {
    scan_on_push = true
  }
}

# Chỉ giữ 10 image mới nhất mỗi repo để khỏi tốn tiền lưu.
resource "aws_ecr_lifecycle_policy" "app" {
  for_each   = aws_ecr_repository.app
  repository = each.value.name

  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Keep last 10 images"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = 10
      }
      action = { type = "expire" }
    }]
  })
}

output "ecr_repository_urls" {
  value = { for name, repo in aws_ecr_repository.app : name => repo.repository_url }
}

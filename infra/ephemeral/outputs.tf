output "cluster_name" {
  value = module.eks.cluster_name
}

output "cluster_endpoint" {
  value = module.eks.cluster_endpoint
}

output "vpc_id" {
  value = module.vpc.vpc_id
}

output "public_subnet_ids" {
  value = module.vpc.public_subnets
}

output "private_subnet_ids" {
  value = module.vpc.private_subnets
}

output "configure_kubectl" {
  value = "aws eks update-kubeconfig --region ${var.region} --name ${module.eks.cluster_name} --profile mock"
}

output "db_address" {
  value = aws_db_instance.gymnote.address
}

output "db_password" {
  value     = random_password.db.result
  sensitive = true
}

output "lb_controller_role_arn" {
  value = aws_iam_role.lb_controller.arn
}

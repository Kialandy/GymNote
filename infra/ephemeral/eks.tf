module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 21.0"

  name               = var.cluster_name
  kubernetes_version = var.kubernetes_version

  # Tai khoan khong co quyen kms:TagResource -> khong tao khoa KMS rieng.
  create_kms_key    = false
  encryption_config = null

  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.private_subnets

  # Mac của bạn gọi API server qua endpoint public; quyền vẫn do IAM kiểm soát.
  endpoint_public_access       = true
  endpoint_private_access      = true
  endpoint_public_access_cidrs = var.public_access_cidrs

  # Cho danh tính đang chạy terraform (bạn) quyền admin cluster qua EKS access entry.
  enable_cluster_creator_admin_permissions = true

  addons = {
    coredns = {}
    eks-pod-identity-agent = {
      before_compute = true
    }
    kube-proxy = {}
    vpc-cni = {
      before_compute = true
    }
  }

  eks_managed_node_groups = {
    default = {
      ami_type       = "AL2023_x86_64_STANDARD" # x86_64 để khớp image linux/amd64
      instance_types = [var.node_instance_type]
      capacity_type  = "ON_DEMAND"

      min_size     = var.node_min_size
      max_size     = var.node_max_size
      desired_size = var.node_desired_size
    }
  }
}

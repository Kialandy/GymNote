data "aws_iam_role" "jenkins" {
  name = "gymnote-jenkins"
}

resource "aws_eks_access_entry" "jenkins" {
  cluster_name      = module.eks.cluster_name
  principal_arn     = data.aws_iam_role.jenkins.arn
  kubernetes_groups = ["gymnote-preview-managers"]
}

resource "aws_eks_access_policy_association" "jenkins" {
  cluster_name  = module.eks.cluster_name
  principal_arn = data.aws_iam_role.jenkins.arn
  policy_arn    = "arn:aws:eks::aws:cluster-access-policy/AmazonEKSEditPolicy"

  access_scope {
    type       = "namespace"
    namespaces = ["dev", "staging", "prod", "feature-*", "preview-tools"]
  }

  depends_on = [aws_eks_access_entry.jenkins]
}

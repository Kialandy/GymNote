#!/usr/bin/env bash
# Teardown of the EPHEMERAL layer. Usage: AWS_PROFILE=mock ./scripts/destroy.sh
set -euo pipefail

REGION="${AWS_REGION:-ap-southeast-1}"
CLUSTER="gymnote-eks"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TF_DIR="$ROOT/infra/ephemeral"

if [[ -z "${AWS_PROFILE:-}" ]]; then
  echo "AWS_PROFILE is not set. Run: export AWS_PROFILE=mock" >&2
  exit 1
fi

ACCOUNT="$(aws sts get-caller-identity --query Account --output text)"
echo "Account: $ACCOUNT | Region: $REGION | Profile: $AWS_PROFILE"
read -r -p "Destroy the EPHEMERAL layer (VPC, EKS, ...) in this account? Type 'yes': " ANSWER
[[ "$ANSWER" == "yes" ]] || { echo "Aborted."; exit 1; }

echo "==> Step 1: remove load balancers/volumes created from INSIDE the cluster"
echo "    (they are not in Terraform state and would block VPC deletion)"
if aws eks describe-cluster --name "$CLUSTER" --region "$REGION" >/dev/null 2>&1; then
  aws eks update-kubeconfig --name "$CLUSTER" --region "$REGION" --profile "$AWS_PROFILE" >/dev/null
  if kubectl get ns >/dev/null 2>&1; then
    kubectl delete ingress --all -A --wait=true --timeout=300s || true
    kubectl get svc -A -o jsonpath='{range .items[?(@.spec.type=="LoadBalancer")]}{.metadata.namespace}{" "}{.metadata.name}{"\n"}{end}' \
      | while read -r ns name; do
          [[ -n "$ns" ]] && kubectl delete svc "$name" -n "$ns" --wait=true --timeout=300s || true
        done
    kubectl delete pvc --all -A --wait=true --timeout=300s || true
  else
    echo "    Cluster API not reachable, skipping in-cluster cleanup."
  fi
else
  echo "    Cluster does not exist, skipping in-cluster cleanup."
fi

VPC_ID="$(aws ec2 describe-vpcs --region "$REGION" \
  --filters "Name=tag:Project,Values=gymnote" "Name=tag:Layer,Values=ephemeral" \
  --query 'Vpcs[0].VpcId' --output text)"
if [[ "$VPC_ID" != "None" ]]; then
  for i in $(seq 1 30); do
    COUNT="$(aws elbv2 describe-load-balancers --region "$REGION" \
      --query "length(LoadBalancers[?VpcId=='$VPC_ID'])" --output text)"
    [[ "$COUNT" == "0" ]] && break
    echo "    Waiting for $COUNT load balancer(s) in $VPC_ID to disappear ($i/30)..."
    sleep 10
  done
fi

echo "==> Step 2: terraform destroy (ephemeral layer only)"
terraform -chdir="$TF_DIR" destroy

echo "==> Step 3: leftover check in $REGION"
FAIL=0
check() {
  if [[ "$2" == "0" ]]; then echo "PASS  $1"; else echo "FAIL  $1: $2 left"; FAIL=1; fi
}
check "EKS clusters" \
  "$(aws eks list-clusters --region "$REGION" --query 'length(clusters)' --output text)"
check "EC2 instances of the cluster" \
  "$(aws ec2 describe-instances --region "$REGION" \
     --filters "Name=tag:eks:cluster-name,Values=$CLUSTER" \
               "Name=instance-state-name,Values=pending,running,stopping,stopped" \
     --query 'length(Reservations[].Instances[])' --output text)"
check "NAT gateways (ephemeral)" \
  "$(aws ec2 describe-nat-gateways --region "$REGION" \
     --filter "Name=state,Values=pending,available" "Name=tag:Layer,Values=ephemeral" \
     --query 'length(NatGateways)' --output text)"
check "Load balancers" \
  "$(aws elbv2 describe-load-balancers --region "$REGION" --query 'length(LoadBalancers)' --output text)"
check "RDS instances" \
  "$(aws rds describe-db-instances --region "$REGION" --query 'length(DBInstances)' --output text)"
check "Unassociated Elastic IPs" \
  "$(aws ec2 describe-addresses --region "$REGION" --query 'length(Addresses[?AssociationId==null])' --output text)"
check "Unattached EBS volumes" \
  "$(aws ec2 describe-volumes --region "$REGION" --filters Name=status,Values=available \
     --query 'length(Volumes)' --output text)"
check "VPCs (ephemeral)" \
  "$(aws ec2 describe-vpcs --region "$REGION" --filters Name=tag:Layer,Values=ephemeral \
     --query 'length(Vpcs)' --output text)"

if [[ "$FAIL" -eq 0 ]]; then
  echo "All clean."
else
  echo "Some resources remain. Check the AWS Console (EC2, VPC, EKS, RDS, Load Balancers)." >&2
  exit 1
fi

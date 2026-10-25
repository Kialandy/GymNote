#!/usr/bin/env bash
set -euo pipefail
: "${AWS_PROFILE:?Run: export AWS_PROFILE=mock}"
REGION="${AWS_REGION:-ap-southeast-1}"
CLUSTER="gymnote-eks"
TF_DIR="infra/ephemeral"

echo "Account: $(aws sts get-caller-identity --query Account --output text)  Region: $REGION  Profile: $AWS_PROFILE"
read -r -p "Destroy the ephemeral stack? Type yes: " ANSWER
[ "$ANSWER" = "yes" ] || { echo "Aborted."; exit 1; }

VPC_ID=$(aws ec2 describe-vpcs --region "$REGION" \
  --filters Name=tag:Project,Values=gymnote Name=tag:Layer,Values=ephemeral \
  --query 'Vpcs[0].VpcId' --output text)

echo "==> Step 1: remove in-cluster resources that create AWS resources"
if aws eks describe-cluster --name "$CLUSTER" --region "$REGION" >/dev/null 2>&1; then
  aws eks update-kubeconfig --name "$CLUSTER" --region "$REGION" >/dev/null
  kubectl delete ingress --all -A --ignore-not-found --timeout=180s || true
  kubectl get svc -A --no-headers \
    -o custom-columns=NS:.metadata.namespace,NAME:.metadata.name,TYPE:.spec.type \
    | awk '$3=="LoadBalancer"{print $1" "$2}' \
    | while read -r ns name; do kubectl delete svc "$name" -n "$ns" --timeout=120s || true; done
  kubectl delete pvc --all -A --ignore-not-found --timeout=120s || true
fi
if [ "$VPC_ID" != "None" ]; then
  for i in $(seq 1 30); do
    LBS=$(aws elbv2 describe-load-balancers --region "$REGION" \
      --query "length(LoadBalancers[?VpcId=='$VPC_ID'])" --output text)
    [ "$LBS" = "0" ] && break
    echo "Waiting for load balancers to disappear ($LBS left)..."; sleep 10
  done
fi

echo "==> Step 2: terraform destroy"
terraform -chdir="$TF_DIR" destroy -auto-approve

echo "==> Step 3: leftover check in $REGION"
FAIL=0
check() { # name count
  if [ "$2" = "0" ]; then echo "PASS  $1"; else echo "FAIL  $1: $2 left"; FAIL=1; fi
}
check "EKS cluster $CLUSTER" "$(aws eks list-clusters --region $REGION --query "length(clusters[?@=='$CLUSTER'])" --output text)"
check "EC2 instances of the cluster" "$(aws ec2 describe-instances --region $REGION --filters Name=tag:eks:cluster-name,Values=$CLUSTER Name=instance-state-name,Values=pending,running,stopping,stopped --query 'length(Reservations[].Instances[])' --output text)"
check "NAT gateways (gymnote)" "$(aws ec2 describe-nat-gateways --region $REGION --filter Name=tag:Project,Values=gymnote Name=state,Values=pending,available --query 'length(NatGateways)' --output text)"
if [ "$VPC_ID" != "None" ]; then
  check "Load balancers in old VPC" "$(aws elbv2 describe-load-balancers --region $REGION --query "length(LoadBalancers[?VpcId=='$VPC_ID'])" --output text)"
fi
check "RDS gymnote-db" "$(aws rds describe-db-instances --region $REGION --query "length(DBInstances[?DBInstanceIdentifier=='gymnote-db'])" --output text)"
check "Unassociated Elastic IPs (gymnote)" "$(aws ec2 describe-addresses --region $REGION --filters Name=tag:Project,Values=gymnote --query 'length(Addresses[?AssociationId==`null`])' --output text)"
check "Unattached EBS volumes (cluster)" "$(aws ec2 describe-volumes --region $REGION --filters Name=status,Values=available Name=tag-key,Values=kubernetes.io/cluster/$CLUSTER --query 'length(Volumes)' --output text)"
check "VPCs (ephemeral)" "$(aws ec2 describe-vpcs --region $REGION --filters Name=tag:Project,Values=gymnote Name=tag:Layer,Values=ephemeral --query 'length(Vpcs)' --output text)"
[ "$FAIL" = "0" ] && echo "All clean." || { echo "Some resources remain. Check the AWS Console."; exit 1; }

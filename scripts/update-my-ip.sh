#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
: "${AWS_PROFILE:?Run: export AWS_PROFILE=mock}"

IP=$(curl -s https://checkip.amazonaws.com | tr -d '[:space:]')
echo "IP hiện tại: $IP"

sed -i '' "s|my_ip_cidr.*|my_ip_cidr     = \"$IP/32\"|" infra/persistent/terraform.tfvars
terraform -chdir=infra/persistent apply -auto-approve
echo "Xong. Jenkins mở cho $IP"

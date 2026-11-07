#!/usr/bin/env bash
# Dùng: scripts/preview-down.sh 12345
# Xóa Ingress (để ALB bị gỡ), namespace và database của một feature
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/preview-common.sh

N="$1"; NS="feature-$N"; DB="gymnote_f$N"

# An toàn: chỉ xóa namespace có nhãn preview, không bao giờ đụng dev/staging/prod
if kubectl get ns -l gymnote/preview=true -o name | grep -qx "namespace/$NS"; then
  # Xóa Ingress trước, chờ controller gỡ ALB xong rồi mới xóa namespace
  kubectl delete ingress --all -n "$NS" --wait=true --timeout=300s
  kubectl delete namespace "$NS" --wait=true --timeout=300s
fi

# Xóa database (FORCE ngắt kết nối còn sót)
load_admin
run_sql "DROP DATABASE IF EXISTS $DB WITH (FORCE);"
echo "Đã dọn $NS và $DB"

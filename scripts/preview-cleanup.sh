#!/usr/bin/env bash
# Duyệt các môi trường preview, xóa môi trường nào có branch đã bị xóa trên GitHub
set -euo pipefail
cd "$(dirname "$0")/.."
REPO="Kialandy/GymNote"

for ns in $(kubectl get ns -l gymnote/preview=true -o jsonpath='{.items[*].metadata.name}'); do
  N="${ns#feature-}"
  # Hỏi GitHub branch còn không: 200 = còn, 404 = đã xóa
  CODE=$(curl -s -o /dev/null -w '%{http_code}' "https://api.github.com/repos/$REPO/branches/feature%2F$N")
  case "$CODE" in
    200) echo "$ns: branch còn, giữ lại" ;;
    404) echo "$ns: branch đã xóa, dọn dẹp"; scripts/preview-down.sh "$N" ;;
    *)   echo "$ns: GitHub trả $CODE, bỏ qua lần này" ;;
  esac
done

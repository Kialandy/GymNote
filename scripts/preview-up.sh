#!/usr/bin/env bash
# Dùng: scripts/preview-up.sh feature/12345 <image_tag>
# Tạo namespace + database (sao chép từ dev) + secret + deploy + in địa chỉ ALB cho một branch feature
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/preview-common.sh

BRANCH="$1"; TAG="$2"
MAX_PREVIEWS=3

# Chỉ nhận branch đúng mẫu feature/<số>, lấy số làm hậu tố
[[ "$BRANCH" =~ ^feature/([0-9]+)$ ]] || { echo "Branch '$BRANCH' không đúng mẫu feature/<số>"; exit 1; }
N="${BASH_REMATCH[1]}"; NS="feature-$N"; DB="gymnote_f$N"

# Giới hạn số môi trường preview chạy cùng lúc (cluster chỉ có 2 node nhỏ)
# NEW=1 nghĩa là môi trường mới, chỉ khi đó mới sao chép dữ liệu từ dev
NEW=0
if ! kubectl get ns "$NS" >/dev/null 2>&1; then
  NEW=1
  COUNT=$(kubectl get ns -l gymnote/preview=true --no-headers | wc -l)
  [ "$COUNT" -lt "$MAX_PREVIEWS" ] || { echo "Đã đủ $MAX_PREVIEWS môi trường preview, hãy xóa bớt branch cũ"; exit 1; }
fi

# Tạo namespace (chạy lại nhiều lần vẫn an toàn) và gắn nhãn để script dọn dẹp nhận ra
kubectl create namespace "$NS" --dry-run=client -o yaml | kubectl apply -f -
kubectl label namespace "$NS" gymnote/preview=true --overwrite

# Tạo database gymnote_f<số> nếu chưa có
load_admin
run_sql "SELECT 'CREATE DATABASE $DB' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$DB')\gexec"

# Môi trường mới: sao chép dữ liệu từ database dev (gymnote). Các lần build sau giữ nguyên dữ liệu của feature
if [ "$NEW" = "1" ]; then
  echo "Sao chép dữ liệu từ gymnote sang $DB..."
  clone_db gymnote "$DB"
fi

# Tạo/cập nhật secret chuỗi kết nối (trỏ đúng database của feature này)
kubectl -n "$NS" create secret generic gymnote-db \
  --from-literal=connection-string="Host=$DB_HOST;Port=5432;Database=$DB;Username=$DB_USER;Password=$DB_PASS;SSL Mode=Require;Trust Server Certificate=true" \
  --dry-run=client -o yaml | kubectl apply -f -

# Tạo khóa JWT một lần duy nhất (giữ nguyên ở các lần build sau để không bị đăng xuất)
if ! kubectl -n "$NS" get secret gymnote-app >/dev/null 2>&1; then
  kubectl -n "$NS" create secret generic gymnote-app --from-literal=jwt-secret="$(openssl rand -base64 32)"
fi

# Sinh overlay tạm: dùng lại base, chỉ đổi namespace (xóa khi script kết thúc)
TMP="k8s/overlays/.tmp-feature-$N"
mkdir -p "$TMP"; trap 'rm -rf "$TMP"' EXIT
cat > "$TMP/kustomization.yaml" <<YAML
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
namespace: $NS
resources:
  - ../../base
YAML

# [ĐIỀU CHỈNH] Thay IMAGE_TAG bằng tag thật rồi apply. Nếu deploy.sh của bạn thay khác, làm giống y như vậy
kubectl kustomize "$TMP" | sed "s/IMAGE_TAG/$TAG/g" | kubectl apply -f -

# Chờ các deployment sẵn sàng
kubectl -n "$NS" wait --for=condition=available deployment --all --timeout=300s

# Chờ ALB được cấp địa chỉ (tối đa khoảng 7 phút)
HOST=""
for i in $(seq 1 40); do
  HOST=$(kubectl -n "$NS" get ingress -o jsonpath='{.items[0].status.loadBalancer.ingress[0].hostname}' 2>/dev/null || true)
  [ -n "$HOST" ] && break; sleep 10
done
[ -n "$HOST" ] || { echo "ALB chưa có địa chỉ, kiểm tra controller"; exit 1; }
echo "http://$HOST" > preview-url.txt
echo "PREVIEW URL: http://$HOST"

# Thử truy cập trang chủ (DNS của ALB có thể cần 1-3 phút)
for i in $(seq 1 30); do
  curl -fsS -o /dev/null "http://$HOST/" && { echo "Frontend OK"; exit 0; }; sleep 10
done
echo "Cảnh báo: chưa truy cập được, thử lại sau vài phút"
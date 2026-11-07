#!/usr/bin/env bash
# Hàm dùng chung cho các script preview: đọc tài khoản RDS admin và chạy SQL qua pod tạm
set -euo pipefail
NS_TOOLS="preview-tools"

# Đọc địa chỉ, user, mật khẩu RDS từ secret rds-admin trong cluster
load_admin() {
  DB_HOST=$(kubectl -n "$NS_TOOLS" get secret rds-admin -o jsonpath='{.data.host}' | base64 -d)
  DB_USER=$(kubectl -n "$NS_TOOLS" get secret rds-admin -o jsonpath='{.data.user}' | base64 -d)
  DB_PASS=$(kubectl -n "$NS_TOOLS" get secret rds-admin -o jsonpath='{.data.password}' | base64 -d)
}

# Chạy SQL (đọc từ tham số 1) bằng pod psql tạm trong cluster, xong tự xóa pod
# (RDS ở private subnet nên Jenkins không nối trực tiếp được)
run_sql() {
  echo "$1" | kubectl -n "$NS_TOOLS" run "psql-$RANDOM" --rm -i --restart=Never --quiet \
    --image=postgres:17 --env="PGPASSWORD=$DB_PASS" --command -- \
    psql "host=$DB_HOST user=$DB_USER dbname=postgres sslmode=require" -v ON_ERROR_STOP=1 -q
}

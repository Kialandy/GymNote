#!/usr/bin/env bash
set -euo pipefail
NS="${1:?namespace}"
HOST=""
for i in $(seq 1 30); do
  HOST=$(kubectl -n "$NS" get ingress gymnote -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')
  [ -n "$HOST" ] && break
  echo "waiting for ALB hostname..."; sleep 10
done
[ -n "$HOST" ] || { echo "ALB has no hostname"; exit 1; }
for i in $(seq 1 30); do
  CODE=$(curl -s -o /dev/null -w '%{http_code}' "http://$HOST/" || true)
  echo "attempt $i: HTTP $CODE"
  [ "$CODE" = "200" ] && exit 0
  sleep 10
done
exit 1

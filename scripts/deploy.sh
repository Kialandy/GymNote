#!/usr/bin/env bash
set -euo pipefail
NS="${1:?namespace}"
TAG="${2:?image tag}"
kubectl kustomize "k8s/overlays/$NS" | sed "s/IMAGE_TAG/$TAG/g" | kubectl apply -f -
kubectl -n "$NS" rollout status deployment/backend --timeout=300s
kubectl -n "$NS" rollout status deployment/frontend --timeout=300s

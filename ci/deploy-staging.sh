#!/usr/bin/env sh
set -eu
registry="${1:?ECR registry required}"
tag="${2:?immutable image tag required}"
namespace=smarthotel-staging

kubectl apply -k infra/kubernetes/overlays/staging
for name in frontend gateway identity booking hotel-ops field-ops notifications concierge; do
  kubectl -n "$namespace" set image "deployment/$name" "$name=$registry/smarthotel-$name:$tag"
  kubectl -n "$namespace" annotate deployment "$name" "smarthotel.io/commit=$tag" --overwrite
done
kubectl -n "$namespace" rollout status deployment --timeout=10m

#!/usr/bin/env sh
set -eu
registry="${1:?ECR registry required}"
tag="${2:?immutable image tag required}"
while IFS='|' read -r name _ _; do
  [ -n "$name" ] || continue
  trivy image --exit-code 1 --severity CRITICAL --ignore-unfixed "$registry/smarthotel-$name:$tag"
done < ci/images.txt

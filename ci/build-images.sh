#!/usr/bin/env sh
set -eu
registry="${1:?ECR registry required}"
tag="${2:?immutable image tag required}"
while IFS='|' read -r name dockerfile context; do
  [ -n "$name" ] || continue
  docker build --pull --file "$dockerfile" --tag "$registry/smarthotel-$name:$tag" "$context"
done < ci/images.txt

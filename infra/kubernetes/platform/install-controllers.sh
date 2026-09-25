#!/usr/bin/env sh
set -eu
: "${CALICO_VERSION:?Set a reviewed Calico version, for example v3.x.y}"
: "${CCM_CHART_VERSION:?Set the AWS cloud-controller-manager chart version matching Kubernetes}"
: "${EBS_CSI_CHART_VERSION:?Set a reviewed aws-ebs-csi-driver chart version}"
: "${ALB_CHART_VERSION:?Set a reviewed AWS Load Balancer Controller chart version}"
: "${AWS_REGION:?Set AWS_REGION}"
: "${VPC_ID:?Set VPC_ID}"
cluster_name="${CLUSTER_NAME:-smarthotel-staging}"

kubectl apply -f "https://raw.githubusercontent.com/projectcalico/calico/${CALICO_VERSION}/manifests/calico.yaml"

helm repo add aws-cloud-controller-manager https://kubernetes.github.io/cloud-provider-aws
helm repo add aws-ebs-csi-driver https://kubernetes-sigs.github.io/aws-ebs-csi-driver
helm repo add eks https://aws.github.io/eks-charts
helm repo update

helm upgrade --install aws-cloud-controller-manager aws-cloud-controller-manager/aws-cloud-controller-manager \
  --namespace kube-system --version "$CCM_CHART_VERSION" --set args[0]="--cloud-provider=aws"
helm upgrade --install aws-ebs-csi-driver aws-ebs-csi-driver/aws-ebs-csi-driver \
  --namespace kube-system --version "$EBS_CSI_CHART_VERSION"
helm upgrade --install aws-load-balancer-controller eks/aws-load-balancer-controller \
  --namespace kube-system --version "$ALB_CHART_VERSION" \
  --set clusterName="$cluster_name" --set region="$AWS_REGION" --set vpcId="$VPC_ID"

kubectl rollout status -n kube-system daemonset/aws-cloud-controller-manager --timeout=5m
kubectl rollout status -n kube-system daemonset/ebs-csi-node --timeout=5m
kubectl rollout status -n kube-system deployment/aws-load-balancer-controller --timeout=5m

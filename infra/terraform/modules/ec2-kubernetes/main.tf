data "aws_ssm_parameter" "ubuntu" { name = "/aws/service/canonical/ubuntu/server/24.04/stable/current/amd64/hvm/ebs-gp3/ami-id" }

locals {
  common_bootstrap = <<-EOT
    #!/bin/bash
    set -euxo pipefail
    swapoff -a
    modprobe overlay
    modprobe br_netfilter
    cat >/etc/sysctl.d/99-kubernetes.conf <<'SYSCTL'
    net.bridge.bridge-nf-call-iptables=1
    net.bridge.bridge-nf-call-ip6tables=1
    net.ipv4.ip_forward=1
    SYSCTL
    sysctl --system
    apt-get update
    apt-get install -y ca-certificates curl gpg containerd
    mkdir -p /etc/containerd
    containerd config default >/etc/containerd/config.toml
    sed -i 's/SystemdCgroup = false/SystemdCgroup = true/' /etc/containerd/config.toml
    systemctl restart containerd
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://pkgs.k8s.io/core:/stable:/v${var.kubernetes_minor}/deb/Release.key | gpg --dearmor -o /etc/apt/keyrings/kubernetes-apt-keyring.gpg
    echo 'deb [signed-by=/etc/apt/keyrings/kubernetes-apt-keyring.gpg] https://pkgs.k8s.io/core:/stable:/v${var.kubernetes_minor}/deb/ /' >/etc/apt/sources.list.d/kubernetes.list
    apt-get update && apt-get install -y kubelet kubeadm kubectl awscli
    apt-mark hold kubelet kubeadm kubectl
  EOT
}

resource "aws_instance" "control_plane" {
  ami                    = data.aws_ssm_parameter.ubuntu.value
  instance_type          = var.control_plane_instance_type
  subnet_id              = var.private_subnet_ids[0]
  vpc_security_group_ids = [var.security_group_id]
  iam_instance_profile   = var.instance_profile_name
  user_data              = "${local.common_bootstrap}\nkubeadm init --token ${var.bootstrap_token} --pod-network-cidr=${var.pod_cidr} --apiserver-advertise-address=$(hostname -I | awk '{print $1}')\nmkdir -p /root/.kube && cp /etc/kubernetes/admin.conf /root/.kube/config\naws ssm put-parameter --region ${var.aws_region} --name /smarthotel/${var.name}/join-command --type SecureString --overwrite --value \"$(kubeadm token create --print-join-command)\"\n"
  root_block_device {
    encrypted   = true
    volume_type = "gp3"
    volume_size = 40
  }
  metadata_options {
    http_tokens   = "required"
    http_endpoint = "enabled"
  }
  tags = merge(var.tags, { Name = "${var.name}-control-plane", "kubernetes.io/cluster/${var.name}" = "owned" })
}

resource "aws_instance" "workers" {
  count                  = var.worker_count
  ami                    = data.aws_ssm_parameter.ubuntu.value
  instance_type          = var.worker_instance_type
  subnet_id              = var.private_subnet_ids[count.index % length(var.private_subnet_ids)]
  vpc_security_group_ids = [var.security_group_id]
  iam_instance_profile   = var.instance_profile_name
  user_data              = "${local.common_bootstrap}\nuntil join_cmd=$(aws ssm get-parameter --region ${var.aws_region} --name /smarthotel/${var.name}/join-command --with-decryption --query Parameter.Value --output text 2>/dev/null); do sleep 10; done\n$join_cmd\n"
  root_block_device {
    encrypted   = true
    volume_type = "gp3"
    volume_size = 50
  }
  metadata_options {
    http_tokens   = "required"
    http_endpoint = "enabled"
  }
  tags = merge(var.tags, { Name = "${var.name}-worker-${count.index + 1}", "kubernetes.io/cluster/${var.name}" = "owned" })
}

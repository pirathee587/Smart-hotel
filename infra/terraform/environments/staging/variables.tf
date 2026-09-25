variable "aws_region" {
  type    = string
  default = "ap-south-1"
}
variable "allowed_https_cidrs" {
  type    = list(string)
  default = ["0.0.0.0/0"]
}
variable "control_plane_instance_type" {
  type    = string
  default = "t3.large"
}
variable "worker_instance_type" {
  type    = string
  default = "t3.large"
}
variable "worker_count" {
  type    = number
  default = 2
}
variable "kubernetes_bootstrap_token" {
  type      = string
  sensitive = true
  validation {
    condition     = can(regex("^[a-z0-9]{6}\\.[a-z0-9]{16}$", var.kubernetes_bootstrap_token))
    error_message = "Use a kubeadm token in abcdef.0123456789abcdef format."
  }
}

variable "name" { type = string }
variable "aws_region" { type = string }
variable "private_subnet_ids" { type = list(string) }
variable "security_group_id" { type = string }
variable "instance_profile_name" { type = string }
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
variable "kubernetes_minor" {
  type    = string
  default = "1.35"
}
variable "pod_cidr" {
  type    = string
  default = "192.168.0.0/16"
}
variable "bootstrap_token" {
  type      = string
  sensitive = true
}
variable "tags" {
  type    = map(string)
  default = {}
}

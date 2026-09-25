locals {
  name = "smarthotel-staging"
  tags = { Project = "SmartHotel", Environment = "staging", ManagedBy = "Terraform" }
}

module "network" {
  source          = "../../modules/network"
  name            = local.name
  vpc_cidr        = "10.40.0.0/16"
  public_subnets  = { "ap-south-1a" = "10.40.0.0/24", "ap-south-1b" = "10.40.1.0/24" }
  private_subnets = { "ap-south-1a" = "10.40.10.0/24", "ap-south-1b" = "10.40.11.0/24" }
  tags            = local.tags
}

module "security" {
  source              = "../../modules/security"
  name                = local.name
  vpc_id              = module.network.vpc_id
  vpc_cidr            = "10.40.0.0/16"
  allowed_https_cidrs = var.allowed_https_cidrs
  tags                = local.tags
}

module "iam" {
  source = "../../modules/iam"
  name   = local.name
  tags   = local.tags
}
module "ecr" {
  source       = "../../modules/ecr"
  repositories = ["frontend", "gateway", "identity", "booking", "hotel-ops", "field-ops", "notifications", "concierge"]
  tags         = local.tags
}

module "kubernetes" {
  source                      = "../../modules/ec2-kubernetes"
  name                        = local.name
  aws_region                  = var.aws_region
  private_subnet_ids          = module.network.private_subnet_ids
  security_group_id           = module.security.node_security_group_id
  instance_profile_name       = module.iam.instance_profile_name
  control_plane_instance_type = var.control_plane_instance_type
  worker_instance_type        = var.worker_instance_type
  worker_count                = var.worker_count
  bootstrap_token             = var.kubernetes_bootstrap_token
  tags                        = local.tags
}

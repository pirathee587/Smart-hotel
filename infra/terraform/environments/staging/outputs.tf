output "ecr_repository_urls" { value = module.ecr.repository_urls }
output "control_plane_instance_id" { value = module.kubernetes.control_plane_instance_id }
output "worker_instance_ids" { value = module.kubernetes.worker_instance_ids }

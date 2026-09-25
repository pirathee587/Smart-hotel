output "control_plane_instance_id" { value = aws_instance.control_plane.id }
output "control_plane_private_ip" { value = aws_instance.control_plane.private_ip }
output "worker_instance_ids" { value = aws_instance.workers[*].id }

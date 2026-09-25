output "instance_profile_name" { value = aws_iam_instance_profile.nodes.name }
output "node_role_arn" { value = aws_iam_role.nodes.arn }

output "node_security_group_id" { value = aws_security_group.nodes.id }
output "alb_security_group_id" { value = aws_security_group.alb.id }

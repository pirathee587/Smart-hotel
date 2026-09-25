data "aws_iam_policy_document" "ec2_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "nodes" {
  name               = "${var.name}-nodes"
  assume_role_policy = data.aws_iam_policy_document.ec2_assume.json
  tags               = var.tags
}

resource "aws_iam_role_policy_attachment" "ssm" {
  role       = aws_iam_role.nodes.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}
resource "aws_iam_role_policy_attachment" "ecr" {
  role       = aws_iam_role.nodes.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
}
resource "aws_iam_role_policy_attachment" "ebs_csi" {
  role       = aws_iam_role.nodes.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonEBSCSIDriverPolicy"
}

resource "aws_iam_role_policy" "alb_controller" {
  name = "${var.name}-alb-controller"
  role = aws_iam_role.nodes.id
  policy = jsonencode({ Version = "2012-10-17", Statement = [
    { Effect = "Allow", Action = ["ec2:Describe*", "elasticloadbalancing:Describe*", "acm:ListCertificates", "acm:DescribeCertificate", "iam:CreateServiceLinkedRole"], Resource = "*" },
    { Effect = "Allow", Action = ["ec2:CreateSecurityGroup", "ec2:CreateTags", "ec2:AuthorizeSecurityGroupIngress", "ec2:RevokeSecurityGroupIngress", "ec2:DeleteSecurityGroup", "elasticloadbalancing:CreateLoadBalancer", "elasticloadbalancing:CreateTargetGroup", "elasticloadbalancing:CreateListener", "elasticloadbalancing:CreateRule", "elasticloadbalancing:ModifyLoadBalancerAttributes", "elasticloadbalancing:ModifyTargetGroup", "elasticloadbalancing:ModifyTargetGroupAttributes", "elasticloadbalancing:ModifyListener", "elasticloadbalancing:ModifyRule", "elasticloadbalancing:RegisterTargets", "elasticloadbalancing:DeregisterTargets", "elasticloadbalancing:AddTags", "elasticloadbalancing:RemoveTags", "elasticloadbalancing:DeleteRule", "elasticloadbalancing:DeleteListener", "elasticloadbalancing:DeleteTargetGroup", "elasticloadbalancing:DeleteLoadBalancer", "elasticloadbalancing:SetSecurityGroups", "elasticloadbalancing:SetSubnets"], Resource = "*" }
  ] })
}

data "aws_region" "current" {}

resource "aws_iam_role_policy" "bootstrap_join" {
  name = "${var.name}-bootstrap-join"
  role = aws_iam_role.nodes.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["ssm:GetParameter", "ssm:PutParameter"]
        Resource = "arn:aws:ssm:*:*:parameter/smarthotel/*"
      },
      {
        Effect   = "Allow"
        Action   = ["kms:Decrypt", "kms:Encrypt", "kms:GenerateDataKey"]
        Resource = "*"
        Condition = { StringEquals = { "kms:ViaService" = "ssm.${data.aws_region.current.name}.amazonaws.com" } }
      }
    ]
  })
}

resource "aws_iam_instance_profile" "nodes" {
  name = "${var.name}-nodes"
  role = aws_iam_role.nodes.name
}

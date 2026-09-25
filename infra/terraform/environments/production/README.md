# Production boundary

Production is intentionally not configured for automatic deployment. Create a separately reviewed environment only after staging migration, recovery, security, load-balancer, and browser evidence passes. Never reuse staging state, secrets, CIDRs, certificates, or kubeadm tokens.

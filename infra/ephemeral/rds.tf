variable "db_multi_az" {
  type    = bool
  default = false
}

resource "random_password" "db" {
  length  = 24
  special = false
}

resource "aws_db_subnet_group" "gymnote" {
  name       = "gymnote-db"
  subnet_ids = module.vpc.private_subnets
}

resource "aws_security_group" "db" {
  name        = "gymnote-db"
  description = "PostgreSQL from EKS nodes"
  vpc_id      = module.vpc.vpc_id
}

resource "aws_vpc_security_group_ingress_rule" "db_from_nodes" {
  security_group_id            = aws_security_group.db.id
  referenced_security_group_id = module.eks.node_security_group_id
  ip_protocol                  = "tcp"
  from_port                    = 5432
  to_port                      = 5432
}

resource "aws_db_instance" "gymnote" {
  identifier              = "gymnote-db"
  engine                  = "postgres"
  engine_version          = "17"
  instance_class          = "db.t3.micro"
  allocated_storage       = 20
  db_name                 = "gymnote"
  username                = "gymnote"
  password                = random_password.db.result
  db_subnet_group_name    = aws_db_subnet_group.gymnote.name
  vpc_security_group_ids  = [aws_security_group.db.id]
  multi_az                = var.db_multi_az
  publicly_accessible     = false
  skip_final_snapshot     = true
  deletion_protection     = false
  apply_immediately       = true
  backup_retention_period = 0
}

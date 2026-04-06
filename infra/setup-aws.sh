#!/usr/bin/env bash
# =============================================================================
# FrogBets — Provisionamento de infraestrutura na AWS
# Execute UMA VEZ para criar todos os recursos necessários.
#
# Pré-requisitos:
#   - AWS CLI instalado e configurado (aws configure)
#   - Permissão de AdministratorAccess na conta AWS
#
# Uso:
#   chmod +x infra/setup-aws.sh
#   ./infra/setup-aws.sh
# =============================================================================
set -euo pipefail

# ── Configurações ─────────────────────────────────────────────────────────
AWS_REGION="${AWS_REGION:-us-east-1}"
APP_NAME="frogbets"
DB_NAME="FrogBets"
DB_USER="frogbetsadmin"
DB_PASSWORD="${DB_PASSWORD:-}"
GITHUB_ORG="${GITHUB_ORG:-}"       # ex: victorschlindwein
GITHUB_REPO="${GITHUB_REPO:-FrogBets}"

# ── Validações ────────────────────────────────────────────────────────────
if [ -z "$DB_PASSWORD" ]; then
  read -rsp "Senha do banco de dados (mín. 12 chars): " DB_PASSWORD; echo
fi
if [ ${#DB_PASSWORD} -lt 12 ]; then
  echo "Erro: senha deve ter ao menos 12 caracteres."; exit 1
fi
if [ -z "$GITHUB_ORG" ]; then
  read -rp "Seu usuário/org no GitHub (ex: victorschlindwein): " GITHUB_ORG
fi

ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
echo ""
echo "=== FrogBets — Setup AWS ==="
echo "Account  : $ACCOUNT_ID"
echo "Region   : $AWS_REGION"
echo "GitHub   : $GITHUB_ORG/$GITHUB_REPO"
echo ""

# ── 1. ECR Repositories ───────────────────────────────────────────────────
echo "[1/8] Criando repositórios ECR..."
for repo in frogbets-api frogbets-frontend; do
  aws ecr describe-repositories --repository-names "$repo" --region "$AWS_REGION" \
    --output none 2>/dev/null || \
  aws ecr create-repository \
    --repository-name "$repo" \
    --region "$AWS_REGION" \
    --image-scanning-configuration scanOnPush=true \
    --output none
done
ECR_REGISTRY="${ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
echo "    ECR: $ECR_REGISTRY"

# ── 2. VPC e Subnets (usa a VPC default) ─────────────────────────────────
echo "[2/8] Buscando VPC padrão..."
VPC_ID=$(aws ec2 describe-vpcs \
  --filters "Name=isDefault,Values=true" \
  --query "Vpcs[0].VpcId" --output text --region "$AWS_REGION")
SUBNET_IDS=$(aws ec2 describe-subnets \
  --filters "Name=vpc-id,Values=$VPC_ID" \
  --query "Subnets[*].SubnetId" --output text --region "$AWS_REGION" | tr '\t' ',')
echo "    VPC: $VPC_ID"

# ── 3. Security Groups ────────────────────────────────────────────────────
echo "[3/8] Criando Security Groups..."

# SG para o ALB (público)
ALB_SG=$(aws ec2 create-security-group \
  --group-name "${APP_NAME}-alb-sg" \
  --description "FrogBets ALB" \
  --vpc-id "$VPC_ID" \
  --region "$AWS_REGION" \
  --query GroupId --output text 2>/dev/null || \
  aws ec2 describe-security-groups \
    --filters "Name=group-name,Values=${APP_NAME}-alb-sg" \
    --query "SecurityGroups[0].GroupId" --output text --region "$AWS_REGION")

aws ec2 authorize-security-group-ingress \
  --group-id "$ALB_SG" --protocol tcp --port 80 --cidr 0.0.0.0/0 \
  --region "$AWS_REGION" --output none 2>/dev/null || true
aws ec2 authorize-security-group-ingress \
  --group-id "$ALB_SG" --protocol tcp --port 443 --cidr 0.0.0.0/0 \
  --region "$AWS_REGION" --output none 2>/dev/null || true

# SG para os containers ECS
ECS_SG=$(aws ec2 create-security-group \
  --group-name "${APP_NAME}-ecs-sg" \
  --description "FrogBets ECS Tasks" \
  --vpc-id "$VPC_ID" \
  --region "$AWS_REGION" \
  --query GroupId --output text 2>/dev/null || \
  aws ec2 describe-security-groups \
    --filters "Name=group-name,Values=${APP_NAME}-ecs-sg" \
    --query "SecurityGroups[0].GroupId" --output text --region "$AWS_REGION")

aws ec2 authorize-security-group-ingress \
  --group-id "$ECS_SG" --protocol tcp --port 8080 --source-group "$ALB_SG" \
  --region "$AWS_REGION" --output none 2>/dev/null || true
aws ec2 authorize-security-group-ingress \
  --group-id "$ECS_SG" --protocol tcp --port 80 --source-group "$ALB_SG" \
  --region "$AWS_REGION" --output none 2>/dev/null || true

# SG para o RDS
RDS_SG=$(aws ec2 create-security-group \
  --group-name "${APP_NAME}-rds-sg" \
  --description "FrogBets RDS" \
  --vpc-id "$VPC_ID" \
  --region "$AWS_REGION" \
  --query GroupId --output text 2>/dev/null || \
  aws ec2 describe-security-groups \
    --filters "Name=group-name,Values=${APP_NAME}-rds-sg" \
    --query "SecurityGroups[0].GroupId" --output text --region "$AWS_REGION")

aws ec2 authorize-security-group-ingress \
  --group-id "$RDS_SG" --protocol tcp --port 5432 --source-group "$ECS_SG" \
  --region "$AWS_REGION" --output none 2>/dev/null || true

# ── 4. RDS PostgreSQL ─────────────────────────────────────────────────────
echo "[4/8] Criando RDS PostgreSQL (pode demorar ~5 min)..."
aws rds create-db-instance \
  --db-instance-identifier "${APP_NAME}-db" \
  --db-instance-class db.t3.micro \
  --engine postgres \
  --engine-version "16" \
  --master-username "$DB_USER" \
  --master-user-password "$DB_PASSWORD" \
  --db-name "$DB_NAME" \
  --allocated-storage 20 \
  --storage-type gp2 \
  --vpc-security-group-ids "$RDS_SG" \
  --no-publicly-accessible \
  --backup-retention-period 7 \
  --region "$AWS_REGION" \
  --output none 2>/dev/null || echo "    RDS já existe, pulando..."

echo "    Aguardando RDS ficar disponível..."
aws rds wait db-instance-available \
  --db-instance-identifier "${APP_NAME}-db" \
  --region "$AWS_REGION"

DB_HOST=$(aws rds describe-db-instances \
  --db-instance-identifier "${APP_NAME}-db" \
  --query "DBInstances[0].Endpoint.Address" --output text --region "$AWS_REGION")
DB_CONNECTION_STRING="Host=${DB_HOST};Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};SSL Mode=Require;Trust Server Certificate=true"
echo "    DB Host: $DB_HOST"

# ── 5. ECS Cluster ────────────────────────────────────────────────────────
echo "[5/8] Criando ECS Cluster..."
aws ecs create-cluster \
  --cluster-name "$APP_NAME" \
  --capacity-providers FARGATE FARGATE_SPOT \
  --region "$AWS_REGION" \
  --output none 2>/dev/null || true

# ── 6. IAM Role para ECS Tasks ────────────────────────────────────────────
echo "[6/8] Criando IAM roles..."
# Task Execution Role
cat > /tmp/ecs-trust.json << 'EOF'
{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"ecs-tasks.amazonaws.com"},"Action":"sts:AssumeRole"}]}
EOF

aws iam create-role \
  --role-name "${APP_NAME}-ecs-execution-role" \
  --assume-role-policy-document file:///tmp/ecs-trust.json \
  --output none 2>/dev/null || true

aws iam attach-role-policy \
  --role-name "${APP_NAME}-ecs-execution-role" \
  --policy-arn arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy \
  --output none 2>/dev/null || true

EXECUTION_ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${APP_NAME}-ecs-execution-role"

# ── 7. SSM Parameters (secrets) ───────────────────────────────────────────
echo "[7/8] Armazenando secrets no SSM Parameter Store..."
JWT_KEY=$(openssl rand -base64 48)

aws ssm put-parameter --name "/frogbets/db-connection-string" \
  --value "$DB_CONNECTION_STRING" --type SecureString --overwrite \
  --region "$AWS_REGION" --output none
aws ssm put-parameter --name "/frogbets/jwt-key" \
  --value "$JWT_KEY" --type SecureString --overwrite \
  --region "$AWS_REGION" --output none

# ── 8. OIDC Provider para GitHub Actions ─────────────────────────────────
echo "[8/8] Configurando OIDC para GitHub Actions..."
OIDC_ARN=$(aws iam list-open-id-connect-providers \
  --query "OpenIDConnectProviderList[?ends_with(Arn, 'token.actions.githubusercontent.com')].Arn" \
  --output text 2>/dev/null)

if [ -z "$OIDC_ARN" ]; then
  OIDC_ARN=$(aws iam create-open-id-connect-provider \
    --url https://token.actions.githubusercontent.com \
    --client-id-list sts.amazonaws.com \
    --thumbprint-list 6938fd4d98bab03faadb97b34396831e3780aea1 \
    --query OpenIDConnectProviderArn --output text)
fi

# Role para o GitHub Actions assumir via OIDC
cat > /tmp/github-trust.json << EOF
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": {"Federated": "$OIDC_ARN"},
    "Action": "sts:AssumeRoleWithWebIdentity",
    "Condition": {
      "StringEquals": {"token.actions.githubusercontent.com:aud": "sts.amazonaws.com"},
      "StringLike": {"token.actions.githubusercontent.com:sub": "repo:${GITHUB_ORG}/${GITHUB_REPO}:*"}
    }
  }]
}
EOF

aws iam create-role \
  --role-name "${APP_NAME}-github-actions" \
  --assume-role-policy-document file:///tmp/github-trust.json \
  --output none 2>/dev/null || \
aws iam update-assume-role-policy \
  --role-name "${APP_NAME}-github-actions" \
  --policy-document file:///tmp/github-trust.json \
  --output none

# Permissões necessárias para o GitHub Actions
cat > /tmp/github-policy.json << EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {"Effect":"Allow","Action":["ecr:GetAuthorizationToken"],"Resource":"*"},
    {"Effect":"Allow","Action":["ecr:BatchCheckLayerAvailability","ecr:GetDownloadUrlForLayer","ecr:BatchGetImage","ecr:PutImage","ecr:InitiateLayerUpload","ecr:UploadLayerPart","ecr:CompleteLayerUpload"],"Resource":"arn:aws:ecr:${AWS_REGION}:${ACCOUNT_ID}:repository/frogbets-*"},
    {"Effect":"Allow","Action":["ecs:UpdateService","ecs:DescribeServices","ecs:RegisterTaskDefinition","ecs:DescribeTaskDefinition"],"Resource":"*"},
    {"Effect":"Allow","Action":["iam:PassRole"],"Resource":"$EXECUTION_ROLE_ARN"},
    {"Effect":"Allow","Action":["ssm:GetParameter","ssm:GetParameters"],"Resource":"arn:aws:ssm:${AWS_REGION}:${ACCOUNT_ID}:parameter/frogbets/*"}
  ]
}
EOF

aws iam put-role-policy \
  --role-name "${APP_NAME}-github-actions" \
  --policy-name "${APP_NAME}-deploy-policy" \
  --policy-document file:///tmp/github-policy.json \
  --output none

GITHUB_ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${APP_NAME}-github-actions"

# ── Gera task definitions ─────────────────────────────────────────────────
mkdir -p infra

cat > infra/ecs-task-api.json << EOF
{
  "family": "frogbets-api",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024",
  "executionRoleArn": "$EXECUTION_ROLE_ARN",
  "containerDefinitions": [{
    "name": "frogbets-api",
    "image": "${ECR_REGISTRY}/frogbets-api:latest",
    "portMappings": [{"containerPort": 8080, "protocol": "tcp"}],
    "environment": [
      {"name": "ASPNETCORE_ENVIRONMENT", "value": "Production"},
      {"name": "Jwt__Issuer", "value": "FrogBets"},
      {"name": "Jwt__Audience", "value": "FrogBets"},
      {"name": "Jwt__ExpirationMinutes", "value": "60"}
    ],
    "secrets": [
      {"name": "ConnectionStrings__DefaultConnection", "valueFrom": "arn:aws:ssm:${AWS_REGION}:${ACCOUNT_ID}:parameter/frogbets/db-connection-string"},
      {"name": "Jwt__Key", "valueFrom": "arn:aws:ssm:${AWS_REGION}:${ACCOUNT_ID}:parameter/frogbets/jwt-key"}
    ],
    "logConfiguration": {
      "logDriver": "awslogs",
      "options": {
        "awslogs-group": "/ecs/frogbets-api",
        "awslogs-region": "${AWS_REGION}",
        "awslogs-stream-prefix": "ecs",
        "awslogs-create-group": "true"
      }
    },
    "essential": true
  }]
}
EOF

cat > infra/ecs-task-frontend.json << EOF
{
  "family": "frogbets-frontend",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "256",
  "memory": "512",
  "executionRoleArn": "$EXECUTION_ROLE_ARN",
  "containerDefinitions": [{
    "name": "frogbets-frontend",
    "image": "${ECR_REGISTRY}/frogbets-frontend:latest",
    "portMappings": [{"containerPort": 80, "protocol": "tcp"}],
    "logConfiguration": {
      "logDriver": "awslogs",
      "options": {
        "awslogs-group": "/ecs/frogbets-frontend",
        "awslogs-region": "${AWS_REGION}",
        "awslogs-stream-prefix": "ecs",
        "awslogs-create-group": "true"
      }
    },
    "essential": true
  }]
}
EOF

# ── Resumo ────────────────────────────────────────────────────────────────
echo ""
echo "============================================================"
echo "  SETUP CONCLUÍDO"
echo "  Adicione o secret abaixo no GitHub:"
echo "  Repositório → Settings → Secrets → Actions"
echo "============================================================"
echo ""
echo "AWS_ROLE_ARN:  $GITHUB_ROLE_ARN"
echo ""
echo "Recursos criados:"
echo "  ECR:     $ECR_REGISTRY/frogbets-{api,frontend}"
echo "  RDS:     $DB_HOST"
echo "  Cluster: $APP_NAME"
echo "  Role:    $GITHUB_ROLE_ARN"
echo ""
echo "Próximo passo: rode infra/setup-ecs-services.sh para criar os serviços ECS"
echo "============================================================"

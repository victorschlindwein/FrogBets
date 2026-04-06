#!/usr/bin/env bash
# =============================================================================
# FrogBets — Cria os serviços ECS + ALB
# Execute APÓS o setup-aws.sh ter concluído com sucesso.
# =============================================================================
set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
APP_NAME="frogbets"
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
ECR_REGISTRY="${ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"

echo "=== FrogBets — Setup ECS Services ==="

# ── Busca recursos criados pelo setup-aws.sh ──────────────────────────────
VPC_ID=$(aws ec2 describe-vpcs \
  --filters "Name=isDefault,Values=true" \
  --query "Vpcs[0].VpcId" --output text --region "$AWS_REGION")

SUBNET_IDS_RAW=$(aws ec2 describe-subnets \
  --filters "Name=vpc-id,Values=$VPC_ID" \
  --query "Subnets[*].SubnetId" --output text --region "$AWS_REGION")
SUBNET_ARRAY=($SUBNET_IDS_RAW)
SUBNET_CSV=$(IFS=,; echo "${SUBNET_ARRAY[*]}")

ALB_SG=$(aws ec2 describe-security-groups \
  --filters "Name=group-name,Values=${APP_NAME}-alb-sg" \
  --query "SecurityGroups[0].GroupId" --output text --region "$AWS_REGION")

ECS_SG=$(aws ec2 describe-security-groups \
  --filters "Name=group-name,Values=${APP_NAME}-ecs-sg" \
  --query "SecurityGroups[0].GroupId" --output text --region "$AWS_REGION")

# ── 1. Application Load Balancer ──────────────────────────────────────────
echo "[1/5] Criando Application Load Balancer..."
ALB_ARN=$(aws elbv2 create-load-balancer \
  --name "${APP_NAME}-alb" \
  --subnets ${SUBNET_ARRAY[@]} \
  --security-groups "$ALB_SG" \
  --scheme internet-facing \
  --type application \
  --region "$AWS_REGION" \
  --query "LoadBalancers[0].LoadBalancerArn" --output text 2>/dev/null || \
  aws elbv2 describe-load-balancers \
    --names "${APP_NAME}-alb" \
    --query "LoadBalancers[0].LoadBalancerArn" --output text --region "$AWS_REGION")

ALB_DNS=$(aws elbv2 describe-load-balancers \
  --load-balancer-arns "$ALB_ARN" \
  --query "LoadBalancers[0].DNSName" --output text --region "$AWS_REGION")

echo "    ALB DNS: $ALB_DNS"

# ── 2. Target Groups ──────────────────────────────────────────────────────
echo "[2/5] Criando Target Groups..."

API_TG_ARN=$(aws elbv2 create-target-group \
  --name "${APP_NAME}-api-tg" \
  --protocol HTTP \
  --port 8080 \
  --vpc-id "$VPC_ID" \
  --target-type ip \
  --health-check-path "/api/health" \
  --health-check-interval-seconds 30 \
  --healthy-threshold-count 2 \
  --unhealthy-threshold-count 3 \
  --region "$AWS_REGION" \
  --query "TargetGroups[0].TargetGroupArn" --output text 2>/dev/null || \
  aws elbv2 describe-target-groups \
    --names "${APP_NAME}-api-tg" \
    --query "TargetGroups[0].TargetGroupArn" --output text --region "$AWS_REGION")

FRONTEND_TG_ARN=$(aws elbv2 create-target-group \
  --name "${APP_NAME}-frontend-tg" \
  --protocol HTTP \
  --port 80 \
  --vpc-id "$VPC_ID" \
  --target-type ip \
  --health-check-path "/" \
  --health-check-interval-seconds 30 \
  --healthy-threshold-count 2 \
  --unhealthy-threshold-count 3 \
  --region "$AWS_REGION" \
  --query "TargetGroups[0].TargetGroupArn" --output text 2>/dev/null || \
  aws elbv2 describe-target-groups \
    --names "${APP_NAME}-frontend-tg" \
    --query "TargetGroups[0].TargetGroupArn" --output text --region "$AWS_REGION")

# ── 3. Listener HTTP com regras de roteamento ─────────────────────────────
echo "[3/5] Criando Listener e regras de roteamento..."

LISTENER_ARN=$(aws elbv2 create-listener \
  --load-balancer-arn "$ALB_ARN" \
  --protocol HTTP \
  --port 80 \
  --default-actions "Type=forward,TargetGroupArn=$FRONTEND_TG_ARN" \
  --region "$AWS_REGION" \
  --query "Listeners[0].ListenerArn" --output text 2>/dev/null || \
  aws elbv2 describe-listeners \
    --load-balancer-arn "$ALB_ARN" \
    --query "Listeners[0].ListenerArn" --output text --region "$AWS_REGION")

# Regra: /api/* → API target group
aws elbv2 create-rule \
  --listener-arn "$LISTENER_ARN" \
  --priority 10 \
  --conditions "Field=path-pattern,Values=/api/*" \
  --actions "Type=forward,TargetGroupArn=$API_TG_ARN" \
  --region "$AWS_REGION" \
  --output none 2>/dev/null || true

# ── 4. Registra task definitions ──────────────────────────────────────────
echo "[4/5] Registrando Task Definitions..."
aws ecs register-task-definition \
  --cli-input-json file://infra/ecs-task-api.json \
  --region "$AWS_REGION" --output none

aws ecs register-task-definition \
  --cli-input-json file://infra/ecs-task-frontend.json \
  --region "$AWS_REGION" --output none

# ── 5. Serviços ECS ───────────────────────────────────────────────────────
echo "[5/5] Criando serviços ECS..."

aws ecs create-service \
  --cluster "$APP_NAME" \
  --service-name "${APP_NAME}-api" \
  --task-definition "${APP_NAME}-api" \
  --desired-count 1 \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[$SUBNET_CSV],securityGroups=[$ECS_SG],assignPublicIp=ENABLED}" \
  --load-balancers "targetGroupArn=$API_TG_ARN,containerName=frogbets-api,containerPort=8080" \
  --region "$AWS_REGION" \
  --output none 2>/dev/null || \
aws ecs update-service \
  --cluster "$APP_NAME" \
  --service "${APP_NAME}-api" \
  --desired-count 1 \
  --region "$AWS_REGION" --output none

aws ecs create-service \
  --cluster "$APP_NAME" \
  --service-name "${APP_NAME}-frontend" \
  --task-definition "${APP_NAME}-frontend" \
  --desired-count 1 \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[$SUBNET_CSV],securityGroups=[$ECS_SG],assignPublicIp=ENABLED}" \
  --load-balancers "targetGroupArn=$FRONTEND_TG_ARN,containerName=frogbets-frontend,containerPort=80" \
  --region "$AWS_REGION" \
  --output none 2>/dev/null || \
aws ecs update-service \
  --cluster "$APP_NAME" \
  --service "${APP_NAME}-frontend" \
  --desired-count 1 \
  --region "$AWS_REGION" --output none

# Salva a URL da API no SSM para o build do frontend usar
aws ssm put-parameter \
  --name "/frogbets/api-url" \
  --value "http://${ALB_DNS}" \
  --type String \
  --overwrite \
  --region "$AWS_REGION" --output none

echo ""
echo "============================================================"
echo "  SERVIÇOS ECS CRIADOS"
echo "============================================================"
echo ""
echo "  URL da aplicação: http://$ALB_DNS"
echo "  (Frontend e API acessíveis pelo mesmo domínio)"
echo ""
echo "  Próximos passos:"
echo "  1. Adicione AWS_ROLE_ARN nos secrets do GitHub"
echo "  2. Faça push para main para disparar o primeiro deploy"
echo "  3. Crie o usuário admin conforme DEPLOY.md"
echo "============================================================"

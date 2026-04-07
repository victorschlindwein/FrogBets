$ErrorActionPreference = "Stop"

$AWS_REGION  = if ($env:AWS_REGION)  { $env:AWS_REGION }  else { "sa-east-1" }
$APP_NAME    = "frogbets"
$DB_NAME     = "FrogBets"
$DB_USER     = "frogbetsadmin"
$DB_PASSWORD = if ($env:DB_PASSWORD) { $env:DB_PASSWORD } else { Read-Host "DB Password" }
$GITHUB_ORG  = if ($env:GITHUB_ORG)  { $env:GITHUB_ORG }  else { Read-Host "GitHub org" }
$GITHUB_REPO = if ($env:GITHUB_REPO) { $env:GITHUB_REPO } else { "FrogBets" }

if ($DB_PASSWORD.Length -lt 12) { Write-Error "Password must be at least 12 chars"; exit 1 }

$ACCOUNT_ID = aws sts get-caller-identity --query Account --output text
Write-Host ""
Write-Host "=== FrogBets Setup AWS ==="
Write-Host "Account : $ACCOUNT_ID"
Write-Host "Region  : $AWS_REGION"
Write-Host "GitHub  : $GITHUB_ORG/$GITHUB_REPO"
Write-Host ""

# 1. ECR
Write-Host "[1/8] ECR repositories..."
foreach ($repo in @("frogbets-api", "frogbets-frontend")) {
    $exists = aws ecr describe-repositories --repository-names $repo --region $AWS_REGION --output text 2>$null
    if (-not $exists) {
        aws ecr create-repository --repository-name $repo --region $AWS_REGION --image-scanning-configuration scanOnPush=true --output text | Out-Null
    }
}
$ECR_REGISTRY = "$ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"
Write-Host "    ECR: $ECR_REGISTRY"

# 2. VPC
Write-Host "[2/8] VPC..."
$VPC_ID = aws ec2 describe-vpcs --filters "Name=isDefault,Values=true" --query "Vpcs[0].VpcId" --output text --region $AWS_REGION
$SUBNET_IDS = (aws ec2 describe-subnets --filters "Name=vpc-id,Values=$VPC_ID" --query "Subnets[*].SubnetId" --output text --region $AWS_REGION) -replace "`t", ","
Write-Host "    VPC: $VPC_ID"

# 3. Security Groups
Write-Host "[3/8] Security Groups..."

function Get-OrCreateSG($name, $desc) {
    $id = aws ec2 describe-security-groups --filters "Name=group-name,Values=$name" --query "SecurityGroups[0].GroupId" --output text --region $AWS_REGION 2>$null
    if (-not $id -or $id -eq "None") {
        $id = aws ec2 create-security-group --group-name $name --description $desc --vpc-id $VPC_ID --region $AWS_REGION --query GroupId --output text
    }
    return $id
}

$ALB_SG = Get-OrCreateSG "${APP_NAME}-alb-sg" "FrogBets ALB"
aws ec2 authorize-security-group-ingress --group-id $ALB_SG --protocol tcp --port 80 --cidr 0.0.0.0/0 --region $AWS_REGION --output text 2>$null | Out-Null
aws ec2 authorize-security-group-ingress --group-id $ALB_SG --protocol tcp --port 443 --cidr 0.0.0.0/0 --region $AWS_REGION --output text 2>$null | Out-Null

$ECS_SG = Get-OrCreateSG "${APP_NAME}-ecs-sg" "FrogBets ECS Tasks"
aws ec2 authorize-security-group-ingress --group-id $ECS_SG --protocol tcp --port 8080 --source-group $ALB_SG --region $AWS_REGION --output text 2>$null | Out-Null
aws ec2 authorize-security-group-ingress --group-id $ECS_SG --protocol tcp --port 80 --source-group $ALB_SG --region $AWS_REGION --output text 2>$null | Out-Null

$RDS_SG = Get-OrCreateSG "${APP_NAME}-rds-sg" "FrogBets RDS"
aws ec2 authorize-security-group-ingress --group-id $RDS_SG --protocol tcp --port 5432 --source-group $ECS_SG --region $AWS_REGION --output text 2>$null | Out-Null

# 4. RDS
Write-Host "[4/8] RDS PostgreSQL..."
$RDS_EXISTS = aws rds describe-db-instances --db-instance-identifier "${APP_NAME}-db" --region $AWS_REGION --query "DBInstances[0].DBInstanceIdentifier" --output text 2>$null

if (-not $RDS_EXISTS -or $RDS_EXISTS -eq "None") {
    aws rds create-db-instance `
        --db-instance-identifier "${APP_NAME}-db" `
        --db-instance-class db.t3.micro `
        --engine postgres `
        --engine-version "16" `
        --master-username $DB_USER `
        --master-user-password $DB_PASSWORD `
        --db-name $DB_NAME `
        --allocated-storage 20 `
        --storage-type gp2 `
        --vpc-security-group-ids $RDS_SG `
        --no-publicly-accessible `
        --backup-retention-period 0 `
        --region $AWS_REGION `
        --output text | Out-Null
    Write-Host "    RDS created, waiting to become available..."
} else {
    Write-Host "    RDS already exists, skipping..."
}

aws rds wait db-instance-available --db-instance-identifier "${APP_NAME}-db" --region $AWS_REGION
$DB_HOST = aws rds describe-db-instances --db-instance-identifier "${APP_NAME}-db" --query "DBInstances[0].Endpoint.Address" --output text --region $AWS_REGION

if (-not $DB_HOST -or $DB_HOST -eq "None") { Write-Error "RDS endpoint not found"; exit 1 }
Write-Host "    DB Host: $DB_HOST"

$DB_CONN = "Host=${DB_HOST};Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};SSL Mode=Require;Trust Server Certificate=true"

# 5. ECS Cluster
Write-Host "[5/8] ECS Cluster..."
aws ecs create-cluster --cluster-name $APP_NAME --capacity-providers FARGATE FARGATE_SPOT --region $AWS_REGION --output text 2>$null | Out-Null

# 6. IAM
Write-Host "[6/8] IAM roles..."
$ECS_TRUST = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"ecs-tasks.amazonaws.com"},"Action":"sts:AssumeRole"}]}'
aws iam create-role --role-name "${APP_NAME}-ecs-execution-role" --assume-role-policy-document $ECS_TRUST --output text 2>$null | Out-Null
aws iam attach-role-policy --role-name "${APP_NAME}-ecs-execution-role" --policy-arn arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy 2>$null | Out-Null
$EXECUTION_ROLE_ARN = "arn:aws:iam::${ACCOUNT_ID}:role/${APP_NAME}-ecs-execution-role"

# Permissão para o execution role ler secrets do SSM e escrever logs no CloudWatch
$ECS_EXTRA_POLICY = "{`"Version`":`"2012-10-17`",`"Statement`":[{`"Effect`":`"Allow`",`"Action`":[`"ssm:GetParameter`",`"ssm:GetParameters`"],`"Resource`":`"arn:aws:ssm:${AWS_REGION}:${ACCOUNT_ID}:parameter/${APP_NAME}/*`"},{`"Effect`":`"Allow`",`"Action`":[`"logs:CreateLogGroup`",`"logs:CreateLogStream`",`"logs:PutLogEvents`"],`"Resource`":`"arn:aws:logs:${AWS_REGION}:${ACCOUNT_ID}:log-group:/ecs/${APP_NAME}-*`"}]}"
aws iam put-role-policy --role-name "${APP_NAME}-ecs-execution-role" --policy-name "${APP_NAME}-ecs-extra-policy" --policy-document $ECS_EXTRA_POLICY --output text | Out-Null

# 7. SSM
Write-Host "[7/8] SSM Parameters..."
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$JWT_KEY = [Convert]::ToBase64String($bytes)

aws ssm put-parameter --name "/frogbets/db-connection-string" --value $DB_CONN --type SecureString --overwrite --region $AWS_REGION --output text | Out-Null
aws ssm put-parameter --name "/frogbets/jwt-key" --value $JWT_KEY --type SecureString --overwrite --region $AWS_REGION --output text | Out-Null
Write-Host "    Secrets stored."

# 8. OIDC
Write-Host "[8/8] OIDC for GitHub Actions..."
$OIDC_ARN = aws iam list-open-id-connect-providers --query "OpenIDConnectProviderList[?ends_with(Arn, 'token.actions.githubusercontent.com')].Arn" --output text 2>$null

if (-not $OIDC_ARN) {
    $OIDC_ARN = aws iam create-open-id-connect-provider --url https://token.actions.githubusercontent.com --client-id-list sts.amazonaws.com --thumbprint-list 6938fd4d98bab03faadb97b34396831e3780aea1 --query OpenIDConnectProviderArn --output text
}

$GITHUB_TRUST = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Federated":"OIDC_ARN_PLACEHOLDER"},"Action":"sts:AssumeRoleWithWebIdentity","Condition":{"StringEquals":{"token.actions.githubusercontent.com:aud":"sts.amazonaws.com"},"StringLike":{"token.actions.githubusercontent.com:sub":"repo:ORG_PLACEHOLDER/REPO_PLACEHOLDER:*"}}}]}'
$GITHUB_TRUST = $GITHUB_TRUST -replace "OIDC_ARN_PLACEHOLDER", $OIDC_ARN
$GITHUB_TRUST = $GITHUB_TRUST -replace "ORG_PLACEHOLDER", $GITHUB_ORG
$GITHUB_TRUST = $GITHUB_TRUST -replace "REPO_PLACEHOLDER", $GITHUB_REPO

$role_exists = aws iam get-role --role-name "${APP_NAME}-github-actions" --output text 2>$null
if ($role_exists) {
    aws iam update-assume-role-policy --role-name "${APP_NAME}-github-actions" --policy-document $GITHUB_TRUST --output text | Out-Null
} else {
    aws iam create-role --role-name "${APP_NAME}-github-actions" --assume-role-policy-document $GITHUB_TRUST --output text | Out-Null
}

$GITHUB_POLICY = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Action":["ecr:GetAuthorizationToken"],"Resource":"*"},{"Effect":"Allow","Action":["ecr:BatchCheckLayerAvailability","ecr:GetDownloadUrlForLayer","ecr:BatchGetImage","ecr:PutImage","ecr:InitiateLayerUpload","ecr:UploadLayerPart","ecr:CompleteLayerUpload"],"Resource":"arn:aws:ecr:REGION_PLACEHOLDER:ACCOUNT_PLACEHOLDER:repository/frogbets-*"},{"Effect":"Allow","Action":["ecs:UpdateService","ecs:DescribeServices","ecs:RegisterTaskDefinition","ecs:DescribeTaskDefinition"],"Resource":"*"},{"Effect":"Allow","Action":["iam:PassRole"],"Resource":"ROLE_PLACEHOLDER"},{"Effect":"Allow","Action":["ssm:GetParameter","ssm:GetParameters"],"Resource":"arn:aws:ssm:REGION_PLACEHOLDER:ACCOUNT_PLACEHOLDER:parameter/frogbets/*"}]}'
$GITHUB_POLICY = $GITHUB_POLICY -replace "REGION_PLACEHOLDER", $AWS_REGION
$GITHUB_POLICY = $GITHUB_POLICY -replace "ACCOUNT_PLACEHOLDER", $ACCOUNT_ID
$GITHUB_POLICY = $GITHUB_POLICY -replace "ROLE_PLACEHOLDER", $EXECUTION_ROLE_ARN

aws iam put-role-policy --role-name "${APP_NAME}-github-actions" --policy-name "${APP_NAME}-deploy-policy" --policy-document $GITHUB_POLICY --output text | Out-Null

$GITHUB_ROLE_ARN = "arn:aws:iam::${ACCOUNT_ID}:role/${APP_NAME}-github-actions"

# Task definitions
$api_task = @"
{
  "family": "frogbets-api",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024",
  "executionRoleArn": "$EXECUTION_ROLE_ARN",
  "containerDefinitions": [{
    "name": "frogbets-api",
    "image": "$ECR_REGISTRY/frogbets-api:latest",
    "portMappings": [{"containerPort": 8080, "protocol": "tcp"}],
    "environment": [
      {"name": "ASPNETCORE_ENVIRONMENT", "value": "Production"},
      {"name": "Jwt__Issuer", "value": "FrogBets"},
      {"name": "Jwt__Audience", "value": "FrogBets"},
      {"name": "Jwt__ExpirationMinutes", "value": "60"}
    ],
    "secrets": [
      {"name": "ConnectionStrings__DefaultConnection", "valueFrom": "arn:aws:ssm:$AWS_REGION`:$ACCOUNT_ID`:parameter/frogbets/db-connection-string"},
      {"name": "Jwt__Key", "valueFrom": "arn:aws:ssm:$AWS_REGION`:$ACCOUNT_ID`:parameter/frogbets/jwt-key"}
    ],
    "logConfiguration": {
      "logDriver": "awslogs",
      "options": {
        "awslogs-group": "/ecs/frogbets-api",
        "awslogs-region": "$AWS_REGION",
        "awslogs-stream-prefix": "ecs",
        "awslogs-create-group": "true"
      }
    },
    "essential": true
  }]
}
"@
$api_task | Set-Content infra/ecs-task-api.json

$frontend_task = @"
{
  "family": "frogbets-frontend",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "256",
  "memory": "512",
  "executionRoleArn": "$EXECUTION_ROLE_ARN",
  "containerDefinitions": [{
    "name": "frogbets-frontend",
    "image": "$ECR_REGISTRY/frogbets-frontend:latest",
    "portMappings": [{"containerPort": 80, "protocol": "tcp"}],
    "logConfiguration": {
      "logDriver": "awslogs",
      "options": {
        "awslogs-group": "/ecs/frogbets-frontend",
        "awslogs-region": "$AWS_REGION",
        "awslogs-stream-prefix": "ecs",
        "awslogs-create-group": "true"
      }
    },
    "essential": true
  }]
}
"@
$frontend_task | Set-Content infra/ecs-task-frontend.json

Write-Host ""
Write-Host "=== SETUP COMPLETE ==="
Write-Host "Add to GitHub Secrets (Settings > Secrets > Actions):"
Write-Host "  AWS_ROLE_ARN: $GITHUB_ROLE_ARN"
Write-Host ""
Write-Host "Resources created:"
Write-Host "  ECR:     $ECR_REGISTRY"
Write-Host "  RDS:     $DB_HOST"
Write-Host "  Cluster: $APP_NAME"
Write-Host "  Role:    $GITHUB_ROLE_ARN"

# Deploy FrogBets — AWS ECS Fargate + GitHub Actions

## Visão Geral

```
GitHub (push main)
    └── GitHub Actions
            ├── Roda testes (.NET + Vitest)
            ├── Build & Push imagens → Amazon ECR
            └── Deploy → Amazon ECS Fargate
                            ├── frogbets-api      (ASP.NET Core :8080)
                            └── frogbets-frontend (Nginx + React :80)
                                        ↕
                            Application Load Balancer
                              /api/* → API
                              /*     → Frontend
                                        ↕
                            Amazon RDS PostgreSQL 16
```

---

## Pré-requisitos

- Conta AWS ativa (free tier funciona para começar)
- [AWS CLI v2](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html) instalado
- Credenciais configuradas: `aws configure`

---

## Passo 1 — Configurar AWS CLI

```bash
aws configure
# AWS Access Key ID: <sua chave>
# AWS Secret Access Key: <seu secret>
# Default region name: us-east-1
# Default output format: json
```

---

## Passo 2 — Provisionar infraestrutura base

Execute o script **uma única vez**:

```bash
chmod +x infra/setup-aws.sh
DB_PASSWORD="SuaSenhaSegura123!" \
GITHUB_ORG="victorschlindwein" \
GITHUB_REPO="FrogBets" \
./infra/setup-aws.sh
```

O script cria:
- 2 repositórios ECR (`frogbets-api`, `frogbets-frontend`)
- RDS PostgreSQL 16 (db.t3.micro, ~$15/mês)
- ECS Cluster Fargate
- IAM roles para ECS e GitHub Actions
- SSM Parameters com os secrets (connection string, JWT key)
- OIDC Provider para autenticação sem chaves estáticas no GitHub

Ao final imprime o `AWS_ROLE_ARN` que você precisa adicionar no GitHub.

---

## Passo 3 — Criar serviços ECS e ALB

```bash
chmod +x infra/setup-ecs-services.sh
./infra/setup-ecs-services.sh
```

Cria:
- Application Load Balancer (ponto de entrada único)
- Target Groups para API e Frontend
- Regras de roteamento: `/api/*` → API, `/*` → Frontend
- Serviços ECS Fargate para API e Frontend

Ao final imprime a URL pública da aplicação.

---

## Passo 4 — Adicionar secret no GitHub

Acesse: **Repositório → Settings → Secrets and variables → Actions**

| Secret | Valor |
|--------|-------|
| `AWS_ROLE_ARN` | ARN impresso pelo `setup-aws.sh` (ex: `arn:aws:iam::123456789:role/frogbets-github-actions`) |

> O pipeline usa OIDC — sem chaves de acesso estáticas no GitHub. Mais seguro.

---

## Passo 5 — Criar environment de produção no GitHub

1. **Settings → Environments → New environment** → nome: `production`
2. Opcional: adicionar **Required reviewers** para aprovação manual antes do deploy

---

## Passo 6 — Primeiro Deploy

Faça push para `main` ou dispare manualmente:

```
GitHub → Actions → Build & Deploy to AWS → Run workflow
```

O pipeline executa em ~6-10 minutos:
1. Testes .NET e Vitest
2. Build das imagens Docker e push para ECR
3. Deploy nos serviços ECS (rolling update)

---

## Passo 7 — Criar o primeiro usuário admin

Conecte ao RDS via AWS Systems Manager Session Manager ou um bastion host.

Gere o hash da senha:
```bash
cd tools/HashGen
dotnet run -- "SuaSenhaAdmin123!"
```

Conecte ao banco e insira o admin:
```sql
INSERT INTO "Users" (
  "Id", "Username", "PasswordHash", "IsAdmin",
  "VirtualBalance", "ReservedBalance", "WinsCount", "LossesCount",
  "CreatedAt", "IsTeamLeader"
) VALUES (
  gen_random_uuid(), 'admin', '<hash_gerado>', true,
  10000, 0, 0, 0, now(), false
);
```

---

## Fluxo de Deploy Contínuo

A partir do setup, **todo push para `main`** dispara automaticamente:

```
push → test → build → push ECR → deploy ECS (rolling update)
```

O ECS faz rolling update: sobe a nova versão antes de derrubar a antiga, sem downtime.

---

## Custos Estimados (AWS, us-east-1)

| Recurso | Tier | Custo/mês estimado |
|---------|------|-------------------|
| ECS Fargate (API) | 0.5 vCPU / 1GB | ~$15 |
| ECS Fargate (Frontend) | 0.25 vCPU / 0.5GB | ~$7 |
| RDS PostgreSQL | db.t3.micro, 20GB | ~$15 |
| ECR | 2 repositórios | ~$1 |
| ALB | 1 load balancer | ~$16 |
| **Total estimado** | | **~$54/mês** |

> Para reduzir custos em ambiente de baixo uso, defina `desired-count: 0`
> nos serviços ECS quando não estiver em uso.

---

## Monitoramento — Logs em tempo real

```bash
# Logs da API
aws logs tail /ecs/frogbets-api --follow --region us-east-1

# Logs do Frontend
aws logs tail /ecs/frogbets-frontend --follow --region us-east-1
```

---

## Atualizar variáveis de ambiente sem novo deploy

```bash
# Atualiza um parâmetro no SSM
aws ssm put-parameter \
  --name "/frogbets/jwt-key" \
  --value "nova-chave-aqui" \
  --type SecureString \
  --overwrite

# Força restart do serviço para pegar o novo valor
aws ecs update-service \
  --cluster frogbets \
  --service frogbets-api \
  --force-new-deployment
```

---

## Teardown (remover tudo)

```bash
# Para os serviços
aws ecs update-service --cluster frogbets --service frogbets-api --desired-count 0
aws ecs update-service --cluster frogbets --service frogbets-frontend --desired-count 0

# Remove o RDS (CUIDADO: apaga os dados)
aws rds delete-db-instance --db-instance-identifier frogbets-db --skip-final-snapshot

# Remove o ALB
ALB_ARN=$(aws elbv2 describe-load-balancers --names frogbets-alb --query "LoadBalancers[0].LoadBalancerArn" --output text)
aws elbv2 delete-load-balancer --load-balancer-arn $ALB_ARN

# Remove os repositórios ECR
aws ecr delete-repository --repository-name frogbets-api --force
aws ecr delete-repository --repository-name frogbets-frontend --force
```

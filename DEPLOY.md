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
                            Application Load Balancer (HTTPS :443)
                              /api/* → API
                              /*     → Frontend
                              HTTP :80 → redirect HTTPS
                                        ↕
                            Amazon RDS PostgreSQL 16
```

---

## Pré-requisitos

- Conta AWS ativa
- [AWS CLI v2](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html) instalado e configurado
- PowerShell (Windows) ou bash (Linux/macOS)
- Domínio próprio (necessário para HTTPS)

---

## Passo 1 — Configurar AWS CLI

```powershell
aws configure
# AWS Access Key ID: <sua chave>
# AWS Secret Access Key: <seu secret>
# Default region name: sa-east-1
# Default output format: json
```

---

## Passo 2 — Provisionar infraestrutura base

Execute o script **uma única vez** no PowerShell:

```powershell
$env:DB_PASSWORD = "SuaSenhaSegura123!"
$env:GITHUB_ORG  = "seu-org"
$env:GITHUB_REPO = "FrogBets"
.\infra\setup-aws.ps1
```

O script cria:
- 2 repositórios ECR (`frogbets-api`, `frogbets-frontend`)
- RDS PostgreSQL 16 (db.t3.micro)
- ECS Cluster Fargate
- IAM roles para ECS e GitHub Actions (com permissões de SSM e CloudWatch)
- SSM Parameters: connection string, JWT key
- OIDC Provider para autenticação sem chaves estáticas no GitHub

Ao final imprime o `AWS_ROLE_ARN` necessário para o próximo passo.

---

## Passo 3 — Criar serviços ECS e ALB

```powershell
# Requer bash/WSL — ou execute os comandos equivalentes manualmente
bash infra/setup-ecs-services.sh
```

Cria:
- Application Load Balancer (ponto de entrada único)
- Target Groups para API e Frontend
- Regras de roteamento: `/api/*` → API, `/*` → Frontend
- Serviços ECS Fargate para API e Frontend
- Salva `/frogbets/api-url` no SSM

---

## Passo 4 — Configurar domínio e HTTPS

### 4.1 — Solicitar certificado no ACM

```powershell
$CERT_ARN = aws acm request-certificate `
  --domain-name "seudominio.com.br" `
  --subject-alternative-names "www.seudominio.com.br" `
  --validation-method DNS `
  --region sa-east-1 `
  --query "CertificateArn" --output text
```

### 4.2 — Validar domínio via DNS

```powershell
aws acm describe-certificate `
  --certificate-arn $CERT_ARN `
  --region sa-east-1 `
  --query "Certificate.DomainValidationOptions[*].{Domain:DomainName,Name:ResourceRecord.Name,Value:ResourceRecord.Value}"
```

Adicione os registros CNAME retornados no painel do seu provedor de domínio. Aguarde o status virar `ISSUED`:

```powershell
aws acm describe-certificate --certificate-arn $CERT_ARN --region sa-east-1 --query "Certificate.Status"
```

### 4.3 — Configurar HTTPS no ALB

```powershell
$ALB_ARN = aws elbv2 describe-load-balancers --names frogbets-alb --region sa-east-1 --query "LoadBalancers[0].LoadBalancerArn" --output text
$HTTP_LISTENER_ARN = aws elbv2 describe-listeners --load-balancer-arn $ALB_ARN --region sa-east-1 --query "Listeners[0].ListenerArn" --output text
$FRONTEND_TG_ARN = aws elbv2 describe-target-groups --names frogbets-frontend-tg --region sa-east-1 --query "TargetGroups[0].TargetGroupArn" --output text
$API_TG_ARN = aws elbv2 describe-target-groups --names frogbets-api-tg --region sa-east-1 --query "TargetGroups[0].TargetGroupArn" --output text

# Cria listener HTTPS
$HTTPS_LISTENER_ARN = aws elbv2 create-listener `
  --load-balancer-arn $ALB_ARN `
  --protocol HTTPS --port 443 `
  --certificates CertificateArn=$CERT_ARN `
  --default-actions "Type=forward,TargetGroupArn=$FRONTEND_TG_ARN" `
  --region sa-east-1 `
  --query "Listeners[0].ListenerArn" --output text

# Regra /api/* → API
aws elbv2 create-rule `
  --listener-arn $HTTPS_LISTENER_ARN --priority 10 `
  --conditions "Field=path-pattern,Values=/api/*" `
  --actions "Type=forward,TargetGroupArn=$API_TG_ARN" `
  --region sa-east-1 | Out-Null

# Redireciona HTTP → HTTPS
aws elbv2 modify-listener `
  --listener-arn $HTTP_LISTENER_ARN `
  --default-actions "Type=redirect,RedirectConfig={Protocol=HTTPS,Port=443,StatusCode=HTTP_301}" `
  --region sa-east-1 | Out-Null
```

### 4.4 — Atualizar SSM com URLs HTTPS

```powershell
aws ssm put-parameter --name "/frogbets/api-url" --value "https://www.seudominio.com.br/api" --type String --overwrite --region sa-east-1
aws ssm put-parameter --name "/frogbets/allowed-origins" --value "https://www.seudominio.com.br" --type String --overwrite --region sa-east-1
```

### 4.5 — Apontar domínio para o ALB

No painel do seu provedor de domínio, adicione:
- Tipo: `CNAME`, Nome: `www`, Valor: `<dns-do-alb>.sa-east-1.elb.amazonaws.com`

### 4.6 — Redirecionar domínio raiz (sem www) para www

O problema de `frogbets.com.br` não funcionar sem `www` é um problema de DNS, não de código. O ALB só aceita conexões via CNAME, e registros CNAME não são permitidos no apex do domínio (regra do DNS).

**Solução: usar ALIAS/ANAME no provedor de domínio**

Alguns provedores (Cloudflare, Route 53, Registro.br com suporte a ALIAS) permitem apontar o apex diretamente para o ALB:

- **Cloudflare:** Adicione um registro `A` com nome `@` e valor `<dns-do-alb>` — o Cloudflare resolve automaticamente via CNAME Flattening.
- **AWS Route 53:** Use um registro `A` do tipo **Alias** apontando para o ALB.
- **Outros provedores:** Verifique se suportam `ALIAS` ou `ANAME` no apex.

**Alternativa: redirecionar via regra no ALB**

Adicione uma regra no listener HTTPS que redireciona `frogbets.com.br` → `www.frogbets.com.br`:

```powershell
# Adiciona o certificado ao listener HTTPS (se ainda não incluiu o apex)
aws elbv2 add-listener-certificates --listener-arn $HTTPS_LISTENER_ARN --certificates CertificateArn=$CERT_ARN --region sa-east-1 | Out-Null

# Regra de redirect: host sem www → com www
aws elbv2 create-rule --listener-arn $HTTPS_LISTENER_ARN --priority 5 --conditions "Field=host-header,Values=frogbets.com.br" --actions "Type=redirect,RedirectConfig={Host=www.frogbets.com.br,Path=/#{path},Query=#{query},StatusCode=HTTP_301}" --region sa-east-1 | Out-Null
```

O certificado ACM já inclui `www.frogbets.com.br` como SAN (passo 4.1), então cobre os dois domínios.

---

## Passo 5 — Adicionar secret no GitHub

**Repositório → Settings → Secrets and variables → Actions**

| Secret | Valor |
|--------|-------|
| `AWS_ROLE_ARN` | ARN impresso pelo `setup-aws.ps1` |

---

## Passo 6 — Criar environment de produção no GitHub

1. **Settings → Environments → New environment** → nome: `production`
2. Opcional: adicionar **Required reviewers** para aprovação manual

---

## Passo 7 — Primeiro Deploy

Faça push para `main` ou dispare manualmente:

```
GitHub → Actions → Build & Deploy to AWS → Run workflow
```

O pipeline executa em ~6-10 minutos:
1. Testes .NET e Vitest
2. Build das imagens Docker e push para ECR
3. Deploy nos serviços ECS (rolling update)

---

## Passo 8 — Criar o primeiro usuário admin

Conecte ao RDS e execute:

```sql
INSERT INTO "Users" (
  "Id", "Username", "PasswordHash", "IsAdmin",
  "VirtualBalance", "ReservedBalance", "WinsCount", "LossesCount",
  "CreatedAt", "IsTeamLeader"
) VALUES (
  gen_random_uuid(), 'admin', '<bcrypt_hash>', true,
  10000, 0, 0, 0, now(), false
);
```

---

## SSM Parameters

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `/frogbets/db-connection-string` | SecureString | Connection string do PostgreSQL |
| `/frogbets/jwt-key` | SecureString | Chave de assinatura JWT |
| `/frogbets/api-url` | String | URL pública da API (usada no build do frontend) |
| `/frogbets/allowed-origins` | String | Origem permitida no CORS da API |

---

## Fluxo de Deploy Contínuo

Todo push para `main` dispara automaticamente:

```
push → test → build → push ECR → deploy ECS (rolling update)
```

O ECS faz rolling update sem downtime.

---

## Monitoramento — Logs em tempo real

```powershell
# Logs da API
aws logs tail /ecs/frogbets-api --follow --region sa-east-1

# Logs do Frontend
aws logs tail /ecs/frogbets-frontend --follow --region sa-east-1
```

---

## Atualizar variáveis de ambiente sem novo deploy

```powershell
aws ssm put-parameter --name "/frogbets/jwt-key" --value "nova-chave" --type SecureString --overwrite --region sa-east-1
aws ecs update-service --cluster frogbets --service frogbets-api --force-new-deployment --region sa-east-1 | Out-Null
```

---

## Custos Estimados (AWS, sa-east-1)

| Recurso | Tier | Custo/mês estimado |
|---------|------|-------------------|
| ECS Fargate (API) | 0.5 vCPU / 1GB | ~$15 |
| ECS Fargate (Frontend) | 0.25 vCPU / 0.5GB | ~$7 |
| RDS PostgreSQL | db.t3.micro, 20GB | ~$15 |
| ECR | 2 repositórios | ~$1 |
| ALB | 1 load balancer | ~$16 |
| ACM | Certificado SSL | Gratuito |
| **Total estimado** | | **~$54/mês** |

---

## Teardown (remover tudo)

```powershell
aws ecs update-service --cluster frogbets --service frogbets-api --desired-count 0 --region sa-east-1 | Out-Null
aws ecs update-service --cluster frogbets --service frogbets-frontend --desired-count 0 --region sa-east-1 | Out-Null
aws rds delete-db-instance --db-instance-identifier frogbets-db --skip-final-snapshot --region sa-east-1 | Out-Null
$ALB_ARN = aws elbv2 describe-load-balancers --names frogbets-alb --region sa-east-1 --query "LoadBalancers[0].LoadBalancerArn" --output text
aws elbv2 delete-load-balancer --load-balancer-arn $ALB_ARN --region sa-east-1 | Out-Null
aws ecr delete-repository --repository-name frogbets-api --force --region sa-east-1 | Out-Null
aws ecr delete-repository --repository-name frogbets-frontend --force --region sa-east-1 | Out-Null
```

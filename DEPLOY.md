# Deploy FrogBets — Azure Container Apps + GitHub Actions

## Visão Geral

```
GitHub (push main)
    └── GitHub Actions
            ├── Roda testes (.NET + Vitest)
            ├── Build & Push imagens → Azure Container Registry
            └── Deploy → Azure Container Apps
                            ├── frogbets-api   (ASP.NET Core)
                            └── frogbets-frontend (Nginx + React)
                                        ↕
                            Azure Database for PostgreSQL
```

---

## Pré-requisitos

- Conta Azure com uma subscription ativa
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) instalado
- Acesso de admin ao repositório GitHub

---

## Passo 1 — Login na Azure

```bash
az login
az account set --subscription "<nome ou ID da sua subscription>"
```

---

## Passo 2 — Provisionar a infraestrutura

Execute o script de setup **uma única vez**:

```bash
chmod +x infra/setup-azure.sh
DB_ADMIN_PASSWORD="SuaSenhaSegura123!" ./infra/setup-azure.sh
```

O script cria:
- Resource Group `frogbets-rg` na região `brazilsouth`
- Azure Container Registry (ACR) para armazenar as imagens Docker
- PostgreSQL Flexible Server (tier Burstable B1ms — ~$15/mês)
- Container Apps Environment
- Container App `frogbets-api`
- Container App `frogbets-frontend`
- Service Principal com permissão Contributor no Resource Group

Ao final, o script imprime **todos os valores** que você precisa adicionar como secrets no GitHub.

> Guarde a saída do script em local seguro — ela contém credenciais sensíveis.

---

## Passo 3 — Configurar Secrets no GitHub

Acesse: **Repositório → Settings → Secrets and variables → Actions → New repository secret**

| Secret | Descrição | Onde obter |
|--------|-----------|------------|
| `AZURE_CREDENTIALS` | JSON do Service Principal | Saída do script (bloco JSON) |
| `AZURE_RESOURCE_GROUP` | Nome do resource group | `frogbets-rg` |
| `ACR_LOGIN_SERVER` | URL do registry | Ex: `frogbetsacr.azurecr.io` |
| `ACR_USERNAME` | Usuário do ACR | Saída do script |
| `ACR_PASSWORD` | Senha do ACR | Saída do script |
| `DB_CONNECTION_STRING` | Connection string PostgreSQL | Saída do script |
| `JWT_KEY` | Chave JWT (64+ chars) | Saída do script (gerada automaticamente) |
| `ALLOWED_ORIGINS` | URL pública do frontend | Ex: `https://frogbets-frontend.xxx.azurecontainerapps.io` |

---

## Passo 4 — Configurar Environment de produção no GitHub

1. Acesse **Settings → Environments → New environment**
2. Nomeie como `production`
3. Opcionalmente adicione **Required reviewers** para aprovar deploys manualmente

---

## Passo 5 — Primeiro Deploy

Faça um push para `main` ou dispare manualmente:

```
GitHub → Actions → Build & Deploy to Azure → Run workflow
```

O pipeline executa em ~5-8 minutos:
1. Testes .NET e Vitest
2. Build das imagens Docker e push para o ACR
3. Deploy das Container Apps

---

## Passo 6 — Criar o primeiro usuário admin

Após o primeiro deploy, conecte ao banco e insira o admin:

```bash
# Conectar ao PostgreSQL via Azure CLI
az postgres flexible-server connect \
  --name frogbets-db \
  --admin-user frogbetsadmin \
  --admin-password "SuaSenhaSegura123!" \
  --database-name FrogBets
```

Ou use qualquer cliente PostgreSQL (DBeaver, psql, etc.) com o host:
`frogbets-db.postgres.database.azure.com`

Gere o hash da senha com o utilitário incluso:
```bash
cd tools/HashGen
dotnet run -- "SuaSenhaAdmin123!"
```

Insira o admin:
```sql
INSERT INTO "Users" (
  "Id", "Username", "PasswordHash", "IsAdmin",
  "VirtualBalance", "ReservedBalance", "WinsCount", "LossesCount",
  "CreatedAt", "IsTeamLeader"
) VALUES (
  gen_random_uuid(),
  'admin',
  '<hash_gerado_acima>',
  true, 10000, 0, 0, 0, now(), false
);
```

---

## Fluxo de Deploy Contínuo

A partir do setup, **todo push para `main`** dispara automaticamente:

```
push → test → build → push ACR → deploy Container Apps
```

Para deploys de feature branches sem afetar produção, crie um ambiente `staging`
e adicione uma condição `if: github.ref == 'refs/heads/main'` no job de deploy.

---

## Custos Estimados (Azure, região Brazil South)

| Recurso | Tier | Custo/mês estimado |
|---------|------|-------------------|
| Container Apps (API) | 0.5 vCPU / 1GB, min 0 réplicas | ~$5–15 |
| Container Apps (Frontend) | 0.25 vCPU / 0.5GB, min 0 réplicas | ~$2–5 |
| PostgreSQL Flexible Server | Burstable B1ms, 32GB | ~$15 |
| Container Registry | Basic | ~$5 |
| **Total estimado** | | **~$27–40/mês** |

> Com `min-replicas: 0`, as Container Apps escalam para zero quando não há tráfego,
> reduzindo custos em ambientes de baixo uso.

---

## Variáveis de Ambiente em Produção

Todas as variáveis sensíveis são injetadas pelo GitHub Actions via secrets.
**Nunca commite** valores reais de produção no repositório.

Para atualizar uma variável sem novo deploy de código:

```bash
az containerapp update \
  --name frogbets-api \
  --resource-group frogbets-rg \
  --set-env-vars "Jwt__ExpirationMinutes=120"
```

---

## Monitoramento

Ver logs em tempo real:

```bash
# API
az containerapp logs show --name frogbets-api --resource-group frogbets-rg --follow

# Frontend
az containerapp logs show --name frogbets-frontend --resource-group frogbets-rg --follow
```

---

## Teardown (remover tudo)

```bash
az group delete --name frogbets-rg --yes --no-wait
```

#!/usr/bin/env bash
# =============================================================================
# FrogBets — Provisionamento de infraestrutura na Azure
# Execute UMA VEZ para criar todos os recursos necessários.
#
# Pré-requisitos:
#   - Azure CLI instalado e logado (az login)
#   - Permissão de Contributor na subscription
#
# Uso:
#   chmod +x infra/setup-azure.sh
#   ./infra/setup-azure.sh
# =============================================================================
set -euo pipefail

# ── Configurações — ajuste conforme necessário ────────────────────────────
RESOURCE_GROUP="frogbets-rg"
LOCATION="brazilsouth"          # região mais próxima do Brasil
ACR_NAME="frogbetsacr"          # deve ser globalmente único, só letras/números
ENVIRONMENT_NAME="frogbets-env" # Container Apps Environment
DB_SERVER_NAME="frogbets-db"    # deve ser globalmente único
DB_NAME="FrogBets"
DB_ADMIN_USER="frogbetsadmin"
DB_ADMIN_PASSWORD="${DB_ADMIN_PASSWORD:-}"  # passe via env ou será solicitado

# ── Validações ────────────────────────────────────────────────────────────
if [ -z "$DB_ADMIN_PASSWORD" ]; then
  read -rsp "Digite a senha do banco de dados PostgreSQL: " DB_ADMIN_PASSWORD
  echo
fi

if [ ${#DB_ADMIN_PASSWORD} -lt 12 ]; then
  echo "Erro: a senha deve ter ao menos 12 caracteres."
  exit 1
fi

echo ""
echo "=== FrogBets — Setup Azure ==="
echo "Resource Group : $RESOURCE_GROUP"
echo "Location       : $LOCATION"
echo "ACR            : $ACR_NAME"
echo "DB Server      : $DB_SERVER_NAME"
echo ""

# ── 1. Resource Group ─────────────────────────────────────────────────────
echo "[1/7] Criando Resource Group..."
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --output none

# ── 2. Azure Container Registry ───────────────────────────────────────────
echo "[2/7] Criando Azure Container Registry..."
az acr create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACR_NAME" \
  --sku Basic \
  --admin-enabled true \
  --output none

ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --query loginServer -o tsv)
ACR_USERNAME=$(az acr credential show --name "$ACR_NAME" --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name "$ACR_NAME" --query "passwords[0].value" -o tsv)

echo "    ACR: $ACR_LOGIN_SERVER"

# ── 3. PostgreSQL Flexible Server ─────────────────────────────────────────
echo "[3/7] Criando PostgreSQL Flexible Server (pode demorar ~5 min)..."
az postgres flexible-server create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DB_SERVER_NAME" \
  --location "$LOCATION" \
  --admin-user "$DB_ADMIN_USER" \
  --admin-password "$DB_ADMIN_PASSWORD" \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --storage-size 32 \
  --version 16 \
  --public-access 0.0.0.0 \
  --output none

echo "[3/7] Criando banco de dados $DB_NAME..."
az postgres flexible-server db create \
  --resource-group "$RESOURCE_GROUP" \
  --server-name "$DB_SERVER_NAME" \
  --database-name "$DB_NAME" \
  --output none

DB_HOST="${DB_SERVER_NAME}.postgres.database.azure.com"
DB_CONNECTION_STRING="Host=${DB_HOST};Port=5432;Database=${DB_NAME};Username=${DB_ADMIN_USER};Password=${DB_ADMIN_PASSWORD};SSL Mode=Require;Trust Server Certificate=true"

# ── 4. Container Apps Environment ─────────────────────────────────────────
echo "[4/7] Criando Container Apps Environment..."
az containerapp env create \
  --name "$ENVIRONMENT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --output none

# ── 5. Container App — API ────────────────────────────────────────────────
echo "[5/7] Criando Container App da API..."

# Gera JWT_KEY seguro
JWT_KEY=$(openssl rand -base64 48)

az containerapp create \
  --name "frogbets-api" \
  --resource-group "$RESOURCE_GROUP" \
  --environment "$ENVIRONMENT_NAME" \
  --image "mcr.microsoft.com/dotnet/aspnet:8.0" \
  --target-port 8080 \
  --ingress external \
  --registry-server "$ACR_LOGIN_SERVER" \
  --registry-username "$ACR_USERNAME" \
  --registry-password "$ACR_PASSWORD" \
  --min-replicas 0 \
  --max-replicas 3 \
  --cpu 0.5 \
  --memory 1.0Gi \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__DefaultConnection=${DB_CONNECTION_STRING}" \
    "Jwt__Key=${JWT_KEY}" \
    "Jwt__Issuer=FrogBets" \
    "Jwt__Audience=FrogBets" \
    "Jwt__ExpirationMinutes=60" \
  --output none

API_URL=$(az containerapp show \
  --name "frogbets-api" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.configuration.ingress.fqdn" -o tsv)

echo "    API URL: https://$API_URL"

# ── 6. Container App — Frontend ───────────────────────────────────────────
echo "[6/7] Criando Container App do Frontend..."
az containerapp create \
  --name "frogbets-frontend" \
  --resource-group "$RESOURCE_GROUP" \
  --environment "$ENVIRONMENT_NAME" \
  --image "nginx:alpine" \
  --target-port 80 \
  --ingress external \
  --registry-server "$ACR_LOGIN_SERVER" \
  --registry-username "$ACR_USERNAME" \
  --registry-password "$ACR_PASSWORD" \
  --min-replicas 0 \
  --max-replicas 2 \
  --cpu 0.25 \
  --memory 0.5Gi \
  --output none

FRONTEND_URL=$(az containerapp show \
  --name "frogbets-frontend" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.configuration.ingress.fqdn" -o tsv)

echo "    Frontend URL: https://$FRONTEND_URL"

# Atualiza AllowedOrigins na API com a URL real do frontend
az containerapp update \
  --name "frogbets-api" \
  --resource-group "$RESOURCE_GROUP" \
  --set-env-vars "AllowedOrigins=https://${FRONTEND_URL}" \
  --output none

# ── 7. Service Principal para GitHub Actions ──────────────────────────────
echo "[7/7] Criando Service Principal para GitHub Actions..."
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

SP_JSON=$(az ad sp create-for-rbac \
  --name "frogbets-github-actions" \
  --role Contributor \
  --scopes "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}" \
  --sdk-auth)

# ── Resumo final ──────────────────────────────────────────────────────────
echo ""
echo "============================================================"
echo "  SETUP CONCLUÍDO — Adicione os secrets abaixo no GitHub"
echo "  Repositório → Settings → Secrets → Actions"
echo "============================================================"
echo ""
echo "AZURE_CREDENTIALS:"
echo "$SP_JSON"
echo ""
echo "AZURE_RESOURCE_GROUP:  $RESOURCE_GROUP"
echo "ACR_LOGIN_SERVER:      $ACR_LOGIN_SERVER"
echo "ACR_USERNAME:          $ACR_USERNAME"
echo "ACR_PASSWORD:          $ACR_PASSWORD"
echo "DB_CONNECTION_STRING:  $DB_CONNECTION_STRING"
echo "JWT_KEY:               $JWT_KEY"
echo "ALLOWED_ORIGINS:       https://$FRONTEND_URL"
echo ""
echo "URLs da aplicação:"
echo "  Frontend: https://$FRONTEND_URL"
echo "  API:      https://$API_URL"
echo "============================================================"

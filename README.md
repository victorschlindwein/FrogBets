# 🐸 FrogBets

Plataforma de apostas virtuais para partidas de CS2 entre amigos. Os usuários apostam saldo virtual uns contra os outros em mercados criados por administradores, acompanham o placar no leaderboard e disputam o ranking de jogadores com base em performance real nas partidas.

---

## O Problema que Resolve

Grupos de amigos que jogam CS2 juntos não têm uma forma simples de:
- Apostar saldo fictício entre si em partidas reais
- Acompanhar quem está ganhando mais apostas ao longo do tempo
- Ter um ranking de performance individual baseado em estatísticas reais de jogo (estilo HLTV Rating 2.0)
- Controlar o acesso à plataforma via convites

O FrogBets resolve tudo isso em uma aplicação web self-hosted, sem dinheiro real envolvido.

---

## Funcionalidades

- Registro via convite (sistema de invite codes)
- Apostas P2P: usuários criam e cobrem apostas em mercados de partidas
- Mercados de apostas criados por admins (ex: "Quem vence o mapa 1?")
- Liquidação automática de apostas após resultado ser registrado
- Leaderboard com saldo virtual, vitórias e derrotas
- Ranking de jogadores por performance (Rating 2.0 adaptado do HLTV)
- Painel administrativo para gerenciar jogos, times, jogadores e resultados
- Notificações in-app

---

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | ASP.NET Core 8, C# |
| Banco de dados | PostgreSQL 16 |
| ORM | Entity Framework Core 8 |
| Autenticação | JWT Bearer |
| Frontend | React 18, TypeScript, Vite |
| HTTP Client | Axios |
| Testes Frontend | Vitest + Testing Library |
| Containerização | Docker + Docker Compose |
| Proxy reverso | Nginx |

---

## Pré-requisitos

- [Docker](https://docs.docker.com/get-docker/) e [Docker Compose](https://docs.docker.com/compose/install/)
- Git

Para desenvolvimento local sem Docker:
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- PostgreSQL 16 rodando localmente

---

## Como Executar

### Com Docker (recomendado)

1. Clone o repositório:
   ```bash
   git clone https://github.com/seu-usuario/frogbets.git
   cd frogbets
   ```

2. Copie o arquivo de variáveis de ambiente:
   ```bash
   cp .env.example .env
   ```

3. Edite o `.env` e preencha os valores:
   ```env
   POSTGRES_PASSWORD=sua_senha_segura
   JWT_KEY=gere_uma_chave_com_minimo_32_chars
   ```
   Para gerar uma chave JWT segura:
   ```bash
   openssl rand -base64 32
   ```

4. Suba os containers:
   ```bash
   docker compose up -d
   ```

5. Acesse:
   - Frontend: http://localhost:3000
   - API: http://localhost:8080

As migrações do banco de dados são aplicadas automaticamente na inicialização da API.

---

### Desenvolvimento Local (sem Docker)

**Backend:**
```bash
# Configure a connection string no appsettings.Development.json ou via variável de ambiente
cd src/FrogBets.Api
dotnet run
```

**Frontend:**
```bash
cd frontend
npm install
npm run dev
```

---

## Primeiro Acesso

A plataforma usa um sistema de convites. Para criar o primeiro usuário administrador, insira um registro diretamente no banco:

```sql
INSERT INTO "Users" ("Id", "Username", "PasswordHash", "IsAdmin", "VirtualBalance", "ReservedBalance", "WinsCount", "LossesCount", "CreatedAt")
VALUES (gen_random_uuid(), 'admin', '<bcrypt_hash>', true, 10000, 0, 0, 0, now());
```

Após isso, use o painel admin para gerar convites para os demais usuários.

---

## Testes

**Frontend:**
```bash
cd frontend
npm test
```

**Backend:**
```bash
dotnet test
```

---

## Estrutura do Projeto

```
frogbets/
├── src/
│   ├── FrogBets.Api/          # Controllers, Services, configuração ASP.NET
│   ├── FrogBets.Domain/       # Entidades e enums de domínio
│   └── FrogBets.Infrastructure/ # DbContext, migrações EF Core
├── frontend/
│   └── src/
│       ├── api/               # Cliente HTTP (Axios)
│       ├── components/        # Componentes reutilizáveis
│       └── pages/             # Páginas da aplicação
├── docker-compose.yml
├── Dockerfile.api
├── Dockerfile.frontend
└── nginx.conf
```

---

## Como Contribuir

Contribuições são bem-vindas. Siga o fluxo abaixo:

1. Faça um fork do repositório
2. Crie uma branch para sua feature ou correção:
   ```bash
   git checkout -b feat/minha-feature
   # ou
   git checkout -b fix/meu-bugfix
   ```
3. Faça suas alterações com commits descritivos
4. Abra um Pull Request descrevendo o que foi feito e por quê

### Convenções

- Commits em português ou inglês, mas seja consistente no PR
- Código C# segue as convenções padrão do .NET (PascalCase para membros públicos)
- Código TypeScript/React segue o estilo existente (componentes funcionais, hooks)
- Adicione testes para novas funcionalidades sempre que possível

---

## Registrar uma Issue

Encontrou um bug ou tem uma sugestão? Abra uma issue no GitHub:

1. Acesse a aba **Issues** do repositório
2. Clique em **New Issue**
3. Para bugs, inclua:
   - Descrição do comportamento esperado vs. o que aconteceu
   - Passos para reproduzir
   - Versão do sistema operacional e navegador
   - Logs relevantes (console do browser ou logs da API)
4. Para sugestões, descreva o problema que a feature resolveria

---

## Licença

Este projeto é de uso pessoal/privado entre amigos. Sem licença de distribuição definida.

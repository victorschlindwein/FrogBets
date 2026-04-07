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

### Apostas P2P
- Qualquer usuário autenticado pode criar uma aposta em um mercado aberto, escolhendo uma opção e um valor
- Outros usuários cobrem apostas pendentes — a opção do cobrador é sempre o oposto da do criador (atribuída automaticamente)
- Ao criar ou cobrir uma aposta, o valor é movido de `VirtualBalance` para `ReservedBalance` — o saldo total nunca muda
- Apostas pendentes podem ser canceladas pelo criador antes de serem cobertas
- Marketplace: lista todas as apostas pendentes de outros usuários disponíveis para cobertura

### Jogos e Mercados
- Admins criam jogos (séries de CS2) informando os times, data e número de mapas
- Ao criar um jogo com N mapas, o sistema gera automaticamente todos os mercados: N × {Vencedor do Mapa, Top Kills, Mais Mortes, Maior Dano por Utilitários} + 1 × Vencedor da Série
- Admin inicia o jogo (mercados ficam fechados para novas apostas) e registra os resultados por mercado
- Ao registrar um resultado, todas as apostas ativas do mercado são liquidadas automaticamente: vencedor recebe 2× o valor apostado, perdedor perde o valor reservado

### Leaderboard
- Ranking de apostadores por saldo virtual acumulado
- Exibe saldo disponível, saldo reservado, vitórias e derrotas de cada usuário

### Sistema de Rating de Jogadores (CS2)
- Admins cadastram times e jogadores de CS2
- Para cada partida, o admin registra o `MapResult` (mapa + número de rounds) e depois as estatísticas individuais de cada jogador naquele mapa (kills, deaths, assists, dano total, KAST%)
- O rating é calculado por mapa usando a fórmula HLTV Rating 2.0 adaptada e acumulado no `PlayerScore` do jogador
- Ranking público de jogadores por performance

### Times e Marketplace de Trocas
- Cada usuário pode pertencer a um time, com papel de líder de time
- Líderes podem marcar membros como disponíveis para troca e criar/aceitar ofertas entre times
- Admins podem realizar trocas diretas sem necessidade de oferta formal

### Acesso e Segurança
- Plataforma fechada por convite — o único caminho para criar conta é via token de convite gerado por admin
- JWT com expiração de 60 minutos e logout real (token adicionado à blocklist)
- Rate limiting nos endpoints de autenticação (5 tentativas por 15 minutos por IP)
- Auditoria automática de todas as operações de escrita

### Painel Administrativo
- Interface web completa para gerenciar jogos, times, jogadores, convites, resultados e estatísticas
- Dropdowns com dados reais (sem necessidade de copiar/colar UUIDs)
- Gestão de líderes de time e trocas diretas entre membros

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
| Testes Backend | xUnit + FsCheck (property-based testing) |
| Testes Frontend | Vitest + Testing Library |
| Testes E2E | Cypress |
| Containerização | Docker + Docker Compose |
| Proxy reverso | Nginx |
| Deploy | AWS ECS Fargate + ALB |

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
INSERT INTO "Users" ("Id", "Username", "PasswordHash", "IsAdmin", "VirtualBalance", "ReservedBalance", "WinsCount", "LossesCount", "CreatedAt", "IsTeamLeader")
VALUES (gen_random_uuid(), 'admin', '<bcrypt_hash>', true, 10000, 0, 0, 0, now(), false);
```

Para gerar o hash BCrypt da senha, use o utilitário incluso:
```bash
# Crie um projeto temporário ou use o dotnet-script
# O hash deve ser gerado com BCrypt.Net-Next, work factor 11
```

Após isso, use o painel admin para gerar convites para os demais usuários.

---

## Testes

### Backend (xUnit + FsCheck)

```bash
dotnet test --configuration Release --verbosity quiet
```

272 testes no total, incluindo property-based tests com FsCheck.

### Frontend (Vitest)

```bash
cd frontend
npm run test -- --run
```

### E2E com Cypress

Veja a seção completa em [docs/TECHNICAL.md — Testes E2E com Cypress](docs/TECHNICAL.md#testes-e2e-com-cypress).

Resumo rápido:
```bash
# Com a aplicação rodando localmente (frontend em :5173, API em :8080)
cd frontend
npx cypress open    # interface interativa
npx cypress run     # headless (CI)
```

---

## Estrutura do Projeto

```
frogbets/
├── src/
│   ├── FrogBets.Api/          # Controllers, Services, configuração ASP.NET
│   │   ├── Controllers/       # AuthController, BetsController, GamesController, ...
│   │   └── Services/          # AuthService, BetService, SettlementService, ...
│   ├── FrogBets.Domain/       # Entidades e enums de domínio
│   │   ├── Entities/          # User, Game, Market, Bet, CS2Player, CS2Team, ...
│   │   └── Enums/             # BetStatus, GameStatus, MarketType, ...
│   └── FrogBets.Infrastructure/ # DbContext, migrações EF Core
│       ├── Data/              # FrogBetsDbContext
│       └── Migrations/        # Migrações EF Core
├── frontend/
│   └── src/
│       ├── api/               # Cliente HTTP (Axios) + endpoints de players
│       ├── components/        # Navbar, ProtectedRoute
│       └── pages/             # Login, Register, Dashboard, Games, Bets, ...
├── tests/
│   └── FrogBets.Tests/        # Testes unitários, integração e property-based
│       └── Integration/       # Testes de integração com WebApplicationFactory
├── infra/                     # Scripts de infraestrutura AWS
├── docs/
│   ├── TECHNICAL.md           # Documentação técnica detalhada
│   ├── C4.md                  # Modelo C4 de arquitetura (Context, Containers, Components, Code)
│   └── ADR.md                 # Architecture Decision Records
├── docker-compose.yml
├── Dockerfile.api
├── Dockerfile.frontend
├── DEPLOY.md                  # Guia de deploy AWS ECS Fargate
└── nginx.conf
```

---

## Deploy

A aplicação roda em produção na AWS ECS Fargate com Application Load Balancer. Todo push para `main` dispara o pipeline de CI/CD via GitHub Actions:

1. Roda os testes (.NET + Vitest)
2. Build e push das imagens Docker para ECR
3. Deploy rolling update nos serviços ECS

Veja [DEPLOY.md](DEPLOY.md) para instruções completas de setup da infraestrutura.

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
4. **Rode os testes antes de commitar** — zero falhas é obrigatório:
   ```bash
   dotnet test --configuration Release --verbosity quiet
   cd frontend && npm run test -- --run
   ```
5. Abra um Pull Request descrevendo o que foi feito e por quê

### Convenções

- Commits em português ou inglês, mas seja consistente no PR
- Código C# segue as convenções padrão do .NET (PascalCase para membros públicos)
- Código TypeScript/React segue o estilo existente (componentes funcionais, hooks)
- Adicione testes para novas funcionalidades sempre que possível

---

## Licença

Este projeto é de uso pessoal/privado entre amigos. Sem licença de distribuição definida.

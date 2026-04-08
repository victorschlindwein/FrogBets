# 🐸 FrogBets

Plataforma de apostas virtuais P2P para partidas de CS2 entre amigos. Saldo fictício, sem dinheiro real — serve como métrica de desempenho e diversão.

---

## Índice

- [O Problema que Resolve](#o-problema-que-resolve)
- [Visão Geral da Arquitetura](#visão-geral-da-arquitetura)
- [Funcionalidades](#funcionalidades)
- [Stack](#stack)
- [Como Executar](#como-executar)
- [Primeiro Acesso](#primeiro-acesso)
- [Testes](#testes)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Deploy](#deploy)
- [Como Contribuir](#como-contribuir)
- [Documentação](#documentação)

---

## O Problema que Resolve

Grupos de amigos que jogam CS2 juntos não têm uma forma simples de:
- Apostar saldo fictício entre si em partidas reais
- Acompanhar quem está ganhando mais apostas ao longo do tempo
- Ter um ranking de performance individual baseado em estatísticas reais (estilo HLTV Rating 2.0)
- Controlar o acesso à plataforma via convites

O FrogBets resolve tudo isso em uma aplicação web self-hosted.

---

## Visão Geral da Arquitetura

O sistema é composto por três containers principais:

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                              FrogBets                                        │
│                                                                              │
│  ┌─────────────────────┐   HTTP/JSON    ┌──────────────────────────────────┐ │
│  │                     │   + JWT Bearer │                                  │ │
│  │   React SPA         │ ─────────────► │   ASP.NET Core 8 Web API         │ │
│  │                     │               │                                  │ │
│  │  React 18           │               │  REST API · JWT Auth             │ │
│  │  TypeScript + Vite  │               │  EF Core 8 · Porta 8080          │ │
│  │  Axios              │               │                                  │ │
│  │  Porta 8080 (nginx) │               └──────────────────────────────────┘ │
│  └─────────────────────┘                              │                      │
│                                                       │ EF Core              │
│                                                       ▼                      │
│                                      ┌──────────────────────────────────┐    │
│                                      │        PostgreSQL 16             │    │
│                                      │        Porta 5432                │    │
│                                      └──────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────────────┘
```

Em produção (AWS ECS Fargate):

```
Internet
    │
    ▼
AWS ALB (porta 80/443)
    ├── /api/*  ──► ECS Task: frogbets-api      (porta 8080)
    └── /*      ──► ECS Task: frogbets-frontend  (porta 8080, nginx-unprivileged)
                                                        │
                                                AWS RDS PostgreSQL
```

> Modelo C4 completo (Context, Containers, Components, Code) em [docs/C4.md](docs/C4.md).

---

## Funcionalidades

### Apostas P2P
- Usuários criam apostas em mercados abertos escolhendo uma opção e um valor
- Outros usuários cobrem apostas pendentes — a opção do cobrador é sempre o oposto (atribuída automaticamente)
- Ao criar ou cobrir, o valor sai de `VirtualBalance` e vai para `ReservedBalance` — o total nunca muda
- Apostas pendentes podem ser canceladas pelo criador antes de serem cobertas
- Marketplace lista todas as apostas pendentes disponíveis para cobertura

### Jogos e Mercados
- Admins criam jogos (séries de CS2) com times, data e número de mapas
- Ao criar um jogo com N mapas, o sistema gera automaticamente:
  - N × {Vencedor do Mapa, Top Kills, Mais Mortes, Maior Dano por Utilitários}
  - 1 × Vencedor da Série
- Admins podem editar jogos agendados (times, data, número de mapas) — ao alterar o número de mapas, os mercados são regenerados automaticamente
- Admins podem excluir jogos agendados — todas as apostas pendentes e ativas são canceladas e os saldos devolvidos; jogos em andamento ou finalizados não podem ser excluídos
- Admin inicia o jogo (mercados fecham para novas apostas) e registra resultados por mercado
- Ao registrar um resultado, todas as apostas ativas são liquidadas: vencedor recebe 2× o valor, perdedor perde o reservado

### Leaderboard
- Ranking de apostadores por saldo virtual acumulado
- Exibe saldo disponível, reservado, vitórias e derrotas

### Rating de Jogadores (CS2)
- Admins cadastram times e jogadores de CS2
- Para cada partida, o admin registra o `MapResult` e as estatísticas individuais (kills, deaths, assists, dano, KAST%)
- Rating calculado por mapa usando a fórmula HLTV Rating 2.0 adaptada e acumulado no `PlayerScore`
- Ranking público de jogadores por performance

### Times e Marketplace de Trocas
- Cada usuário pode pertencer a um time, com papel de líder
- Líderes marcam membros como disponíveis para troca e criam/aceitam ofertas entre times
- Admins realizam trocas diretas sem necessidade de oferta formal

### Acesso e Segurança
- Plataforma fechada por convite — único caminho para criar conta é via token gerado por admin
- Todas as rotas da API exigem autenticação JWT (exceto login, registro e health check)
- JWT com expiração de 60 minutos e logout real (token adicionado à blocklist)
- Rate limiting nos endpoints de autenticação (5 tentativas por 15 minutos por IP)
- Auditoria automática de todas as operações de escrita

### Painel Administrativo
- Interface web completa para gerenciar jogos, times, jogadores, convites, resultados e estatísticas
- Edição e exclusão de jogos agendados diretamente pelo painel
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

## Como Executar

### Com Docker (recomendado)

1. Clone o repositório:
   ```bash
   git clone https://github.com/seu-usuario/frogbets.git
   cd frogbets
   ```

2. Copie e preencha as variáveis de ambiente:
   ```bash
   cp .env.example .env
   ```
   ```env
   POSTGRES_PASSWORD=sua_senha_segura
   JWT_KEY=gere_uma_chave_com_minimo_32_chars
   ```
   Para gerar uma chave JWT segura:
   ```bash
   openssl rand -base64 32
   ```

3. Suba os containers:
   ```bash
   docker compose up -d
   ```

4. Acesse:
   - Frontend: http://localhost:3000
   - API: http://localhost:8080

As migrações são aplicadas automaticamente na inicialização da API.

---

### Desenvolvimento Local (sem Docker)

**Backend:**
```bash
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

A plataforma usa um sistema de convites. Para criar o primeiro usuário administrador, insira diretamente no banco:

```sql
INSERT INTO "Users" ("Id", "Username", "PasswordHash", "IsAdmin", "VirtualBalance", "ReservedBalance", "WinsCount", "LossesCount", "CreatedAt", "IsTeamLeader")
VALUES (gen_random_uuid(), 'admin', '<bcrypt_hash>', true, 10000, 0, 0, 0, now(), false);
```

O hash BCrypt deve ser gerado com `BCrypt.Net-Next`, work factor 11. Após isso, use o painel admin para gerar convites para os demais usuários.

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

```bash
# Com a aplicação rodando localmente (frontend em :5173, API em :8080)
cd frontend
npx cypress open    # interface interativa
npx cypress run     # headless (CI)
```

> Instruções completas em [docs/TECHNICAL.md](docs/TECHNICAL.md#testes-e2e-com-cypress).

---

## Estrutura do Projeto

```
frogbets/
├── src/
│   ├── FrogBets.Api/            # Controllers, Services, configuração ASP.NET
│   ├── FrogBets.Domain/         # Entidades e enums de domínio
│   └── FrogBets.Infrastructure/ # DbContext, migrações EF Core
├── frontend/src/
│   ├── api/                     # Cliente HTTP (Axios) + endpoints
│   ├── components/              # Navbar, ProtectedRoute
│   └── pages/                   # Login, Dashboard, Games, Bets, ...
├── tests/FrogBets.Tests/        # Testes unitários, integração e property-based
├── infra/                       # Scripts de infraestrutura AWS
├── docs/
│   ├── TECHNICAL.md             # Documentação técnica detalhada
│   ├── C4.md                    # Modelo C4 de arquitetura
│   └── ADR.md                   # Architecture Decision Records
├── docker-compose.yml
├── Dockerfile.api
├── Dockerfile.frontend
├── DEPLOY.md                    # Guia de deploy AWS ECS Fargate
└── nginx.conf
```

---

## Deploy

A aplicação roda em produção na AWS ECS Fargate com Application Load Balancer. Todo push para `main` dispara o pipeline de CI/CD via GitHub Actions:

1. Roda os testes (.NET + Vitest)
2. Build e push das imagens Docker para ECR
3. Deploy rolling update nos serviços ECS

> Instruções completas de setup da infraestrutura em [DEPLOY.md](DEPLOY.md).

---

## Como Contribuir

1. Faça um fork e crie uma branch:
   ```bash
   git checkout -b feat/minha-feature
   ```

2. Faça suas alterações com commits descritivos.

3. Rode os testes antes de commitar — zero falhas é obrigatório:
   ```bash
   dotnet test --configuration Release --verbosity quiet
   cd frontend && npm run test -- --run
   ```

4. Abra um Pull Request descrevendo o que foi feito e por quê.

### Convenções
- Código C# segue as convenções padrão do .NET (PascalCase para membros públicos)
- Código TypeScript/React segue o estilo existente (componentes funcionais, hooks)
- Adicione testes para novas funcionalidades sempre que possível

---

## Documentação

| Documento | Conteúdo |
|---|---|
| [docs/TECHNICAL.md](docs/TECHNICAL.md) | Endpoints, serviços, entidades, migrações, testes E2E |
| [docs/C4.md](docs/C4.md) | Modelo C4 de arquitetura (Context, Containers, Components, Code) |
| [docs/ADR.md](docs/ADR.md) | Architecture Decision Records |
| [DEPLOY.md](DEPLOY.md) | Setup completo de infraestrutura AWS ECS Fargate |

---

## Licença

Uso pessoal/privado entre amigos. Sem licença de distribuição definida.

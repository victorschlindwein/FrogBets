# FrogBets — Documentação Técnica

Guia de referência para desenvolvedores que queiram entender, contribuir ou estender a plataforma FrogBets.

---

## Arquitetura

```
Frontend (React SPA) — React 18 + TypeScript + Vite + Axios
         |
         | HTTP/JSON + JWT Bearer
         v
Backend (ASP.NET Core 8 Web API) — Porta 8080
  Controllers → Services → FrogBetsDbContext (EF Core 8)
         |
         v
PostgreSQL 16
```

Em produção, o tráfego passa por um Application Load Balancer (AWS ALB):

```
Internet → ALB (porta 80)
              ├── /api/* → ECS frogbets-api (porta 8080)
              └── /*     → ECS frogbets-frontend (porta 8080, nginx-unprivileged)
```

### Fluxo de autenticação

1. `POST /api/auth/login` → retorna JWT com claims: `sub`, `unique_name`, `jti`, `isAdmin`
2. Frontend armazena o token em `sessionStorage` (não localStorage)
3. Cada request inclui `Authorization: Bearer <token>`
4. Logout chama `POST /api/auth/logout` que adiciona o `jti` à blocklist persistida no banco
5. `TokenBlocklist` mantém cache em memória (carregado do banco no primeiro uso)

### Fluxo de aposta P2P

```
Usuário A cria aposta → saldo reservado (VirtualBalance -= X, ReservedBalance += X)
Usuário B cobre aposta → saldo reservado (VirtualBalance -= X, ReservedBalance += X)
Admin registra resultado → SettlementService liquida todas as apostas ativas do mercado
  → Vencedor: VirtualBalance += 2X, ReservedBalance -= X
  → Perdedor: ReservedBalance -= X (consumido pelo vencedor)
  → Anulada: ambos recebem de volta (VirtualBalance += X, ReservedBalance -= X)
```

---

## Entidades de Domínio

### User
| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | Guid | PK |
| `Username` | string | Único, 3-100 chars |
| `PasswordHash` | string | BCrypt hash |
| `IsAdmin` | bool | Permissão de administrador |
| `VirtualBalance` | decimal | Saldo disponível para apostas |
| `ReservedBalance` | decimal | Saldo reservado em apostas ativas/pendentes |
| `WinsCount` | int | Total de apostas ganhas |
| `LossesCount` | int | Total de apostas perdidas |
| `TeamId` | Guid? | FK para CS2Team (nullable) |
| `IsTeamLeader` | bool | Papel de líder de time |

> Invariante: `VirtualBalance + ReservedBalance` = saldo total do usuário (nunca muda exceto em liquidações)

### Game
| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | Guid | PK |
| `TeamA` / `TeamB` | string | Nomes dos times |
| `ScheduledAt` | DateTime | Data/hora do jogo |
| `NumberOfMaps` | int | Formato da série (1, 3 ou 5) |
| `Status` | GameStatus | `Scheduled` → `InProgress` → `Finished` |

Ao criar um jogo com N mapas, o sistema gera automaticamente N × 4 mercados de mapa (MapWinner, TopKills, MostDeaths, MostUtilityDamage) + 1 mercado de série (SeriesWinner).

### Market
| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | Guid | PK |
| `GameId` | Guid | FK para Game |
| `Type` | MarketType | Tipo do mercado |
| `MapNumber` | int? | Número do mapa (null para SeriesWinner) |
| `Status` | MarketStatus | `Open` → `Closed` → `Settled`/`Voided` |
| `WinningOption` | string? | Preenchido na liquidação |

**MarketType:** `MapWinner`, `SeriesWinner`, `TopKills`, `MostDeaths`, `MostUtilityDamage`

### Bet
| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | Guid | PK |
| `MarketId` | Guid | FK para Market |
| `CreatorId` | Guid | FK para User (criador) |
| `CoveredById` | Guid? | FK para User (cobrador) |
| `CreatorOption` | string | Opção escolhida pelo criador |
| `CovererOption` | string? | Opção oposta, atribuída automaticamente |
| `Amount` | decimal | Valor apostado por cada lado |
| `Status` | BetStatus | `Pending` → `Active` → `Settled`/`Cancelled`/`Voided` |
| `Result` | BetResult? | `CreatorWon`, `CovererWon`, `Voided` |

**Opções para mercados de time:** `"TeamA"` ou `"TeamB"`
**Opções para mercados de jogador:** `"<nickname>"` ou `"NOT_<nickname>"`

### AuditLog
| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | Guid | PK |
| `ActorId` | Guid? | FK para User (nullable — null para requisições anônimas) |
| `ActorUsername` | string | Username do actor (máx 100; "anonymous" para anônimos) |
| `Action` | string | Identificador semântico da ação (ex: `bets.create`, `games.start`) |
| `ResourceType` | string? | Tipo do recurso afetado (ex: `bet`, `game`, `user`) |
| `ResourceId` | string? | ID do recurso afetado (extraído dos route values) |
| `HttpMethod` | string | Método HTTP (POST, PATCH, PUT, DELETE) |
| `Route` | string | Template da rota (ex: `api/bets/{id}/cover`) |
| `StatusCode` | int | HTTP status code da resposta |
| `IpAddress` | string? | Endereço IP do cliente (suporta IPv6, máx 45 chars) |
| `OccurredAt` | DateTime | Timestamp UTC da ação |
| `Details` | string? | Informações adicionais (máx 1000 chars, truncado automaticamente) |

### MapResult
| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | Guid | PK |
| `GameId` | Guid | FK para Game |
| `MapNumber` | int | Número ordinal do mapa na série (≥ 1) |
| `Rounds` | int | Total de rounds jogados neste mapa (> 0) |
| `CreatedAt` | DateTime | Timestamp UTC de criação |

Constraint de unicidade: `(GameId, MapNumber)` — não é possível registrar o mesmo mapa duas vezes na mesma série.

### CS2Player / CS2Team / MatchStats

Entidades para o sistema de rating de jogadores. O `PlayerScore` é acumulado usando a fórmula HLTV Rating 2.0 adaptada:

```
Rating = 0.0073 × KAST + 0.3591 × KPR + (−0.5329) × DPR + 0.2372 × Impact + 0.0032 × ADR + 0.1587
```

Onde: KPR = kills/rounds, DPR = deaths/rounds, ADR = damage/rounds, Impact = KPR + (assists/rounds × 0.4)

`MatchStats` referencia `MapResultId` (em vez de `GameId` + `Rounds`). O campo `Rounds` foi removido de `MatchStats` e passa a ser obtido do `MapResult` associado. A constraint de unicidade mudou de `(PlayerId, GameId)` para `(PlayerId, MapResultId)`.

| Campo `MatchStats` | Tipo | Descrição |
|---|---|---|
| `Id` | Guid | PK |
| `PlayerId` | Guid | FK para CS2Player |
| `MapResultId` | Guid | FK para MapResult (substitui GameId; Rounds obtido via MapResult) |
| `Kills` | int | Abates no mapa |
| `Deaths` | int | Mortes no mapa |
| `Assists` | int | Assistências no mapa |
| `TotalDamage` | double | Dano total causado |
| `KastPercent` | double | % de rounds com Kill/Assist/Survive/Trade (0–100) |
| `Rating` | double | Rating calculado pela fórmula HLTV 2.0 adaptada |
| `CreatedAt` | DateTime | Timestamp UTC de criação |

---

## API Endpoints

### Autenticação
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/api/auth/login` | Público | Login, retorna JWT |
| POST | `/api/auth/logout` | JWT | Invalida o token atual |
| POST | `/api/auth/register` | Público | Cadastro com token de convite |

### Usuários
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/users` | Admin | Listar todos os usuários (id, username, isAdmin, isTeamLeader, teamId) |
| GET | `/api/users/me` | JWT | Perfil do usuário autenticado (inclui `isTeamLeader` e `teamId`) |
| GET | `/api/users/me/balance` | JWT | Saldo virtual e reservado |
| PATCH | `/api/users/{id}/team` | JWT (líder/admin) | Mover usuário de time |
| POST | `/api/users/{id}/promote` | Admin | Conceder papel de admin a um usuário |
| POST | `/api/users/{id}/demote` | Admin | Revogar papel de admin (não funciona no master admin) |

### Jogos
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/games` | Público | Listar todos os jogos |
| GET | `/api/games/{id}` | Público | Detalhes de um jogo específico |
| POST | `/api/games` | Admin | Criar jogo (gera mercados automaticamente) |
| PATCH | `/api/games/{id}/start` | Admin | Iniciar jogo (fecha mercados) |
| POST | `/api/games/{id}/results` | Admin | Registrar resultado de mercado |

### Apostas
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/bets` | JWT | Apostas do usuário autenticado |
| POST | `/api/bets` | JWT | Criar aposta |
| POST | `/api/bets/{id}/cover` | JWT | Cobrir aposta pendente |
| DELETE | `/api/bets/{id}` | JWT | Cancelar aposta pendente |
| GET | `/api/marketplace` | JWT | Apostas pendentes de outros usuários |

### Leaderboard
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/leaderboard` | JWT | Ranking de apostadores por saldo |

### Convites
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/api/invites` | Admin | Gerar 1–50 tokens de convite (validade fixa de 24h) |
| GET | `/api/invites` | Admin | Listar todos os convites |
| DELETE | `/api/invites/{id}` | Admin | Revogar convite pendente |

**POST /api/invites — request:**
```json
{ "quantity": 3, "description": "opcional, só aceito quando quantity = 1" }
```

**POST /api/invites — response (201):**
```json
{ "tokens": ["aabbcc...", "ddeeff...", "112233..."] }
```

**Códigos de erro:**
| Código | HTTP | Descrição |
|---|---|---|
| `INVALID_QUANTITY` | 400 | Quantidade menor que 1 |
| `QUANTITY_LIMIT_EXCEEDED` | 400 | Quantidade maior que 50 |

### Times e Jogadores
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/teams` | Público | Listar times |
| POST | `/api/teams` | Admin | Criar time |
| POST | `/api/teams/{id}/leader/{uid}` | Admin | Designar líder |
| DELETE | `/api/teams/{id}/leader` | Admin | Remover líder |
| POST | `/api/players` | Admin | Criar jogador |
| GET | `/api/players` | Admin | Listar jogadores |
| GET | `/api/players/ranking` | Público | Ranking de jogadores |
| POST | `/api/players/{id}/stats` | Admin | Registrar estatísticas de partida (body: `mapResultId`, kills, deaths, assists, totalDamage, kastPercent) |
| GET | `/api/players/{id}/stats` | Público | Retornar estatísticas de um jogador por mapa |

### MapResults
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/api/map-results` | Admin | Criar MapResult para um jogo (body: `gameId`, `mapNumber`, `rounds`) |
| GET | `/api/map-results?gameId={id}` | Admin | Listar MapResults de um jogo, ordenados por MapNumber |

### Logs de Auditoria
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/audit-logs` | Admin | Consultar logs de auditoria com filtros opcionais |

**Query params:** `actorId` (Guid?), `action` (string?), `from` (DateTime?), `to` (DateTime?), `page` (int, padrão 1), `pageSize` (int, padrão 20, máx 100)

### Marketplace de Trocas
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/trades/listings` | JWT | Listar membros disponíveis para troca |
| POST | `/api/trades/listings` | Líder/Admin | Marcar membro como disponível |
| DELETE | `/api/trades/listings/{uid}` | Líder/Admin | Remover disponibilidade |
| GET | `/api/trades/offers` | Líder | Ofertas recebidas |
| POST | `/api/trades/offers` | Líder/Admin | Criar oferta de troca |
| PATCH | `/api/trades/offers/{id}/accept` | Líder | Aceitar oferta |
| PATCH | `/api/trades/offers/{id}/reject` | Líder | Recusar oferta |
| POST | `/api/trades/direct` | Admin | Troca direta entre membros |

### Formato de erro padrão
```json
{
  "error": {
    "code": "CODIGO_DO_ERRO",
    "message": "Mensagem legível para o usuário."
  }
}
```

---

## Serviços de Domínio

| Serviço | Responsabilidade |
|---|---|
| `AuthService` | Login, logout, registro, geração/validação de JWT |
| `BalanceService` | Reserva, liberação e crédito de saldo virtual (transações Serializable) |
| `BetService` | Criar, cobrir, cancelar apostas; listagem de apostas e marketplace |
| `SettlementService` | Liquidar apostas ao registrar resultado de mercado |
| `GameService` | Criar jogos, iniciar, registrar resultados, buscar por ID |
| `InviteService` | Gerar, validar, revogar tokens de convite (RandomNumberGenerator) |
| `TeamMembershipService` | Vincular usuários a times, gerenciar líderes |
| `TradeService` | Marketplace de trocas: listings, ofertas, aceitação, troca direta |
| `PlayerService` | CRUD de jogadores CS2, ranking |
| `MatchStatsService` | Registrar estatísticas de partida, calcular e acumular rating |
| `RatingCalculator` | Cálculo estático do HLTV Rating 2.0 adaptado |
| `TokenBlocklist` | Cache em memória + persistência no banco de JTIs revogados |
| `AuditLogService` | Persistir, consultar e limpar logs de auditoria |
| `AuditLogCleanupService` | IHostedService: limpeza diária de logs expirados |
| `AuditMiddleware` | Middleware ASP.NET Core: intercepta escritas e persiste logs automaticamente |

---

## Segurança

- **Autenticação:** JWT Bearer com expiração configurável (padrão 60 min)
- **Autorização de admin:** verificada via claim `isAdmin` no JWT
- **Autorização de líder:** verificada consultando o banco (não via claim, para evitar tokens desatualizados)
- **Rate limiting:** 5 tentativas por 15 minutos nos endpoints de auth (desabilitado no ambiente `Testing`)
- **Logout:** token adicionado à blocklist (JTI persistido no banco, cache em memória)
- **Senhas:** BCrypt com salt automático (BCrypt.Net-Next)
- **Tokens de convite:** gerados com `RandomNumberGenerator.GetBytes(16)` — criptograficamente seguros
- **CORS:** apenas a origem configurada em `ALLOWED_ORIGINS`
- **Body limit:** 1 MB máximo por request
- **Registro:** apenas via token de convite válido

---

## Banco de Dados

O projeto usa **Entity Framework Core 8** com PostgreSQL 16. As migrações são aplicadas automaticamente no startup da API via `db.Database.Migrate()` (apenas quando o provider for relacional — ignorado em testes com InMemory).

### Migrações existentes
| Migração | Descrição |
|---|---|
| `InitialCreate` | Tabelas base: Users, Games, Markets, Bets, GameResults, Notifications |
| `AddInvites` | Tabela Invites para sistema de convites |
| `AddPlayerRatingSystem` | Tabelas CS2Teams, CS2Players, MatchStats |
| `AddTeamMembership` | Campos TeamId/IsTeamLeader em Users, tabelas TradeListings e TradeOffers |
| `AddRevokedTokens` | Tabela RevokedTokens para blocklist de JWT |
| `AddAuditLogs` | Tabela AuditLogs com índices em ActorId, Action, OccurredAt |
| `AddMapResultAndRefactorMatchStats` | Nova tabela MapResults; MatchStats perde `Rounds` e `GameId`, ganha `MapResultId`; constraint única muda de `(PlayerId, GameId)` para `(PlayerId, MapResultId)` |

### Criar nova migração
```bash
cd src/FrogBets.Infrastructure
dotnet ef migrations add NomeDaMigracao --startup-project ../FrogBets.Api
```

---

## Variáveis de Ambiente

| Variável | Descrição |
|---|---|
| `POSTGRES_PASSWORD` | Senha do PostgreSQL |
| `JWT_KEY` | Chave secreta JWT (mín. 32 chars) — gere com `openssl rand -base64 32` |
| `ALLOWED_ORIGINS` | Origens CORS permitidas (ex: `https://frogbets.example.com`) |
| `MasterAdminUsername` | Username do admin master — protegido contra revogação de direitos via API |
| `AUDIT_LOG_RETENTION_DAYS` | Dias de retenção dos logs de auditoria (padrão: 90) |

Em produção, os valores são armazenados no AWS SSM Parameter Store e injetados nas tasks ECS. Nunca commite o arquivo `.env`.

---

## Testes

### Backend — xUnit + FsCheck

```bash
dotnet test --configuration Release --verbosity quiet
```

264 testes no total. Zero falhas é obrigatório antes de qualquer commit.

#### Arquivos de teste
| Arquivo | Cobertura |
|---|---|
| `AuthServiceTests.cs` | Login, logout, blocklist |
| `BalanceServiceTests.cs` | Reserva, liberação, crédito de saldo |
| `BetServiceCreateTests.cs` | Criação de apostas |
| `BetServiceCoverTests.cs` | Cobertura de apostas |
| `BetServiceCancelTests.cs` | Cancelamento de apostas |
| `BetsListingTests.cs` | Listagem de apostas e marketplace |
| `GameServiceTests.cs` | Criação de jogos, início, registro de resultados |
| `SettlementServiceTests.cs` | Liquidação de apostas |
| `UsersControllerTests.cs` | Endpoints de perfil e saldo |
| `FrogBetsPropertyTests.cs` | Properties 1-19 da spec frog-bets (FsCheck) |
| `PlayerRatingSystemTests.cs` | Properties da spec player-rating-system (FsCheck) |
| `TeamMembershipTests.cs` | Properties da spec team-membership (FsCheck) |
| `Integration/AuthIntegrationTests.cs` | Testes de integração de autenticação |
| `Integration/GamesIntegrationTests.cs` | Testes de integração de jogos |
| `Integration/UsersIntegrationTests.cs` | Testes de integração de usuários |
| `Integration/PlayersIntegrationTests.cs` | Testes de integração de jogadores |
| `Integration/BetsIntegrationTests.cs` | Testes de integração de apostas |
| `Integration/InvitesIntegrationTests.cs` | Testes de integração de convites |
| `Integration/TeamsIntegrationTests.cs` | Testes de integração de times |
| `Integration/LeaderboardIntegrationTests.cs` | Testes de integração do leaderboard |
| `Integration/MarketplaceIntegrationTests.cs` | Testes de integração do marketplace |
| `AuditLogServiceTests.cs` | Testes unitários do AuditLogService |
| `AuditLogPropertyTests.cs` | Properties 1-10 da spec audit-logs (FsCheck) |

#### Configuração dos testes de integração

Os testes de integração usam `WebApplicationFactory<Program>` com banco InMemory. A `IntegrationTestFactory` configura:
- Banco InMemory isolado por instância (Guid único)
- Ambiente `Testing` — desabilita o rate limiter e usa `appsettings.Testing.json`
- JWT com chave fixa de teste (`super-secret-key-that-is-at-least-32-chars!!`)

O arquivo `src/FrogBets.Api/appsettings.Testing.json` contém a configuração JWT para os testes e é versionado no repositório (não contém secrets reais).

### Frontend — Vitest

```bash
cd frontend
npm run test -- --run
```

### Testes E2E com Cypress

Os testes E2E validam fluxos completos da aplicação no browser, com frontend e API rodando localmente.

#### Pré-requisitos

- Frontend rodando em `http://localhost:5173` (Vite dev server)
- API rodando em `http://localhost:8080`
- Banco de dados com dados de teste disponíveis

#### Iniciando o ambiente local

**Terminal 1 — API:**
```bash
cd src/FrogBets.Api
dotnet run
```

**Terminal 2 — Frontend:**
```bash
cd frontend
npm install
npm run dev
```

Aguarde ambos estarem prontos antes de abrir o Cypress.

#### Rodando os testes

**Modo interativo (recomendado para desenvolvimento):**
```bash
cd frontend
npx cypress open
```

Isso abre a interface do Cypress onde você pode:
- Selecionar o browser (Chrome, Firefox, Edge)
- Ver os testes disponíveis em `cypress/e2e/`
- Executar testes individualmente e ver o resultado em tempo real
- Inspecionar cada passo com o time-travel debugger

**Modo headless (CI / linha de comando):**
```bash
cd frontend
npx cypress run
```

Executa todos os testes sem interface gráfica. Screenshots de falhas são salvas em `frontend/cypress/screenshots/`.

**Rodar um arquivo específico:**
```bash
cd frontend
npx cypress run --spec "cypress/e2e/auth.cy.ts"
```

**Rodar em um browser específico:**
```bash
cd frontend
npx cypress run --browser chrome
```

#### Estrutura dos testes E2E

```
frontend/
├── cypress/
│   ├── e2e/           # Arquivos de teste (*.cy.ts)
│   ├── fixtures/      # Dados de teste estáticos (JSON)
│   └── support/
│       └── e2e.ts     # Configuração global e comandos customizados
└── cypress.config.ts  # Configuração: baseUrl, apiUrl, specPattern
```

#### Configuração (`cypress.config.ts`)

```typescript
{
  e2e: {
    baseUrl: 'http://localhost:5173',   // URL do frontend
    specPattern: 'cypress/e2e/**/*.cy.ts',
    env: {
      apiUrl: 'http://localhost:8080',  // URL da API
    }
  }
}
```

#### Dicas

- Os testes E2E não fazem parte do pipeline de CI (apenas Vitest e xUnit rodam no GitHub Actions)
- Use `cy.intercept()` para mockar chamadas de API quando necessário
- Evite depender de dados persistidos entre testes — cada teste deve ser independente
- Para testes que precisam de usuário autenticado, use `cy.session()` para reutilizar a sessão

---

## Deploy

Veja [DEPLOY.md](../DEPLOY.md) para instruções de deploy em produção (AWS ECS Fargate).

### Pipeline CI/CD

Todo push para `main` dispara automaticamente via GitHub Actions:

1. Roda testes .NET (`dotnet test`)
2. Roda testes frontend (`npm run test`)
3. Build das imagens Docker e push para ECR
4. Deploy rolling update nos serviços ECS (sem downtime)

A autenticação com a AWS usa OIDC (sem chaves estáticas no GitHub).

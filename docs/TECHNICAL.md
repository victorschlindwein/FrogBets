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

### CS2Player / CS2Team / MatchStats

Entidades para o sistema de rating de jogadores. O `PlayerScore` é acumulado usando a fórmula HLTV Rating 2.0 adaptada:

```
Rating = 0.0073 × KAST + 0.3591 × KPR + (−0.5329) × DPR + 0.2372 × Impact + 0.0032 × ADR + 0.1587
```

Onde: KPR = kills/rounds, DPR = deaths/rounds, ADR = damage/rounds, Impact = KPR + (assists/rounds × 0.4)

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
| GET | `/api/users/me` | JWT | Perfil do usuário autenticado |
| GET | `/api/users/me/balance` | JWT | Saldo virtual e reservado |
| PATCH | `/api/users/{id}/team` | JWT (líder/admin) | Mover usuário de time |

### Jogos
| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/games` | Público | Listar todos os jogos |
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
| POST | `/api/invites` | Admin | Gerar token de convite |
| GET | `/api/invites` | Admin | Listar todos os convites |
| DELETE | `/api/invites/{id}` | Admin | Revogar convite pendente |

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
| POST | `/api/players/{id}/stats` | Admin | Registrar estatísticas de partida |

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
| `GameService` | Criar jogos, iniciar, registrar resultados |
| `InviteService` | Gerar, validar, revogar tokens de convite |
| `TeamMembershipService` | Vincular usuários a times, gerenciar líderes |
| `TradeService` | Marketplace de trocas: listings, ofertas, aceitação, troca direta |
| `PlayerService` | CRUD de jogadores CS2, ranking |
| `MatchStatsService` | Registrar estatísticas de partida, calcular e acumular rating |
| `RatingCalculator` | Cálculo estático do HLTV Rating 2.0 adaptado |
| `TokenBlocklist` | Cache em memória + persistência no banco de JTIs revogados |

---

## Segurança

- **Autenticação:** JWT Bearer com expiração configurável (padrão 60 min)
- **Autorização de admin:** verificada via claim `isAdmin` no JWT
- **Autorização de líder:** verificada consultando o banco (não via claim, para evitar tokens desatualizados)
- **Rate limiting:** 5 tentativas por 15 minutos nos endpoints de auth
- **Logout:** token adicionado à blocklist (JTI persistido no banco, cache em memória)
- **Senhas:** BCrypt com salt automático
- **CORS:** apenas a origem configurada em `ALLOWED_ORIGINS`
- **Body limit:** 1 MB máximo por request
- **Registro:** apenas via token de convite válido

---

## Banco de Dados

O projeto usa **Entity Framework Core 8** com PostgreSQL 16. As migrações são aplicadas automaticamente no startup da API.

### Migrações existentes
| Migração | Descrição |
|---|---|
| `InitialCreate` | Tabelas base: Users, Games, Markets, Bets, GameResults, Notifications |
| `AddInvites` | Tabela Invites para sistema de convites |
| `AddPlayerRatingSystem` | Tabelas CS2Teams, CS2Players, MatchStats |
| `AddTeamMembership` | Campos TeamId/IsTeamLeader em Users, tabelas TradeListings e TradeOffers |
| `AddRevokedTokens` | Tabela RevokedTokens para blocklist de JWT |

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

Em produção, configure essas variáveis diretamente no servidor ou CI/CD. Nunca commite o arquivo `.env`.

---

## Testes

A suíte de testes usa **xUnit** + **FsCheck** (property-based testing).

```bash
dotnet test
```

### Arquivos de teste
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

---

## Deploy

Veja [DEPLOY.md](../DEPLOY.md) para instruções de deploy em produção (AWS ECS Fargate).

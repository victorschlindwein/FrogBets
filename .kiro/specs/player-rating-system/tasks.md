# Plano de Implementação: Player Rating System

## Overview

Implementação incremental do sistema de rating de jogadores de CS2, seguindo a ordem: entidades de domínio → migração EF Core → lógica pura de cálculo → serviços e interfaces → controllers → registro no DI → testes de propriedade → frontend (client API, seções no AdminPage, PlayersRankingPage).

## Tasks

- [x] 1. Criar entidades de domínio CS2Team, CS2Player e MatchStats
  - Criar `src/FrogBets.Domain/Entities/CS2Team.cs` com propriedades `Id`, `Name`, `LogoUrl`, `CreatedAt` e navegação `Players`
  - Criar `src/FrogBets.Domain/Entities/CS2Player.cs` com propriedades `Id`, `TeamId`, `Nickname`, `RealName`, `PhotoUrl`, `PlayerScore`, `MatchesCount`, `CreatedAt` e navegações `Team` e `Stats`
  - Criar `src/FrogBets.Domain/Entities/MatchStats.cs` com propriedades `Id`, `PlayerId`, `GameId`, `Kills`, `Deaths`, `Assists`, `TotalDamage`, `Rounds`, `KastPercent`, `Rating`, `CreatedAt` e navegações `Player` e `Game`
  - _Requirements: 1.2, 2.2, 3.2_

- [x] 2. Configurar EF Core e gerar migração
  - Adicionar `DbSet<CS2Team>`, `DbSet<CS2Player>` e `DbSet<MatchStats>` ao `FrogBetsDbContext`
  - Configurar `OnModelCreating`: unique index em `CS2Team.Name`, unique index em `CS2Player.Nickname`, unique index composto em `MatchStats(PlayerId, GameId)`, FK de `CS2Player → CS2Team` (Restrict), FK de `MatchStats → CS2Player` (Restrict), FK de `MatchStats → Game` (Restrict), default 0.0 para `CS2Player.PlayerScore`
  - Gerar migração EF Core: `dotnet ef migrations add AddPlayerRatingSystem --project src/FrogBets.Infrastructure --startup-project src/FrogBets.Api`
  - _Requirements: 1.2, 2.2, 3.2_

- [x] 3. Implementar RatingCalculator
  - Criar `src/FrogBets.Api/Services/RatingCalculator.cs` como classe estática pura
  - Implementar método `Calculate(int kills, int deaths, int assists, double totalDamage, int rounds, double kastPercent)` com a fórmula: `0.0073 * kast + 0.3591 * kpr + (-0.5329) * dpr + 0.2372 * impact + 0.0032 * adr + 0.1587`
  - _Requirements: 3.3, 4.1, 4.2, 4.3, 4.4, 4.5, 4.7_

  - [ ]* 3.1 Escrever property test — Property 1: Determinismo do RatingCalculator
    - Verificar que chamadas múltiplas com os mesmos inputs retornam o mesmo valor
    - **Property 1: Determinismo do RatingCalculator**
    - **Validates: Requirements 4.6**

  - [ ]* 3.2 Escrever property test — Property 2: Corretude da fórmula de rating
    - Verificar que o resultado é numericamente igual (tolerância 1e-9) à fórmula de referência calculada inline
    - **Property 2: Corretude da fórmula de rating**
    - **Validates: Requirements 3.3, 4.1, 4.2, 4.3, 4.4, 4.5**

- [x] 4. Implementar ITeamService e TeamService
  - Criar `src/FrogBets.Api/Services/ITeamService.cs` com interface, records `CreateTeamRequest` e `CS2TeamDto` conforme design
  - Criar `src/FrogBets.Api/Services/TeamService.cs` implementando `CreateTeamAsync` (valida nome, verifica duplicata → 409, persiste) e `GetTeamsAsync` (retorna lista ordenada por nome)
  - _Requirements: 1.2, 1.3, 1.4, 1.5_

- [x] 5. Implementar IPlayerService e PlayerService
  - Criar `src/FrogBets.Api/Services/IPlayerService.cs` com interface, records `CreatePlayerRequest`, `CS2PlayerDto` e `PlayerRankingItemDto` conforme design
  - Criar `src/FrogBets.Api/Services/PlayerService.cs` implementando `CreatePlayerAsync` (valida nickname e teamId, verifica duplicatas, persiste), `GetPlayersAsync` e `GetRankingAsync` (ordenado por `PlayerScore` desc, com posição calculada)
  - _Requirements: 2.2, 2.3, 2.4, 2.5, 5.1, 5.2, 5.4_

- [x] 6. Implementar IMatchStatsService e MatchStatsService
  - Criar `src/FrogBets.Api/Services/IMatchStatsService.cs` com interface e records `RegisterStatsRequest` e `MatchStatsDto` conforme design
  - Criar `src/FrogBets.Api/Services/MatchStatsService.cs` implementando `RegisterStatsAsync`: validar `rounds > 0` (→ 400), validar `kastPercent ∈ [0,100]` (→ 400), verificar duplicata `(PlayerId, GameId)` (→ 409), verificar existência de player e game (→ 404), chamar `RatingCalculator.Calculate()`, persistir `MatchStats`, incrementar `CS2Player.PlayerScore` e `CS2Player.MatchesCount`
  - _Requirements: 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

  - [ ]* 6.1 Escrever property test — Property 3: Acumulação do PlayerScore
    - Para N stats registradas para o mesmo jogador, `PlayerScore` final deve ser igual à soma dos N ratings
    - **Property 3: Acumulação do PlayerScore**
    - **Validates: Requirements 3.4**

  - [ ]* 6.2 Escrever property test — Property 4: Rejeição de rounds inválidos
    - Para qualquer `rounds ≤ 0`, o serviço deve rejeitar com `INVALID_ROUNDS_COUNT` e `PlayerScore` permanece inalterado
    - **Property 4: Rejeição de rounds inválidos**
    - **Validates: Requirements 3.6**

  - [ ]* 6.3 Escrever property test — Property 5: Rejeição de KAST fora do intervalo
    - Para qualquer `kastPercent < 0` ou `kastPercent > 100`, o serviço deve rejeitar com `INVALID_KAST_VALUE` e `PlayerScore` permanece inalterado
    - **Property 5: Rejeição de KAST fora do intervalo**
    - **Validates: Requirements 3.7**

- [x] 7. Implementar TeamsController e PlayersController
  - Criar `src/FrogBets.Api/Controllers/TeamsController.cs` com rotas `POST /api/teams` (Admin) e `GET /api/teams` (Admin), seguindo o padrão de `GamesController` para autenticação e tratamento de erros
  - Criar `src/FrogBets.Api/Controllers/PlayersController.cs` com rotas `POST /api/players` (Admin), `GET /api/players` (Admin), `GET /api/players/ranking` (AllowAnonymous) e `POST /api/players/{id}/stats` (Admin)
  - Mapear exceções de domínio para os códigos HTTP definidos no design (400, 404, 409)
  - _Requirements: 1.2, 1.3, 1.4, 2.2, 2.3, 2.4, 2.5, 3.2, 3.5, 3.6, 3.7, 3.8, 5.1, 6.1, 6.2, 6.3_

- [x] 8. Registrar serviços no Program.cs
  - Adicionar `builder.Services.AddScoped<ITeamService, TeamService>()`, `AddScoped<IPlayerService, PlayerService>()` e `AddScoped<IMatchStatsService, MatchStatsService>()` em `src/FrogBets.Api/Program.cs`
  - _Requirements: 1.2, 2.2, 3.2_

- [x] 9. Checkpoint — Verificar compilação e testes
  - Garantir que o projeto compila sem erros e todos os testes passam. Perguntar ao usuário se há dúvidas antes de prosseguir para o frontend.

- [x] 10. Implementar funções de client API no frontend
  - Adicionar em `frontend/src/api/client.ts` (ou arquivo separado `players.ts`) as funções: `getTeams()`, `createTeam(data)`, `getPlayers()`, `createPlayer(data)`, `getPlayersRanking()`, `registerMatchStats(playerId, data)` usando o `apiClient` Axios existente
  - _Requirements: 1.1, 1.5, 2.1, 2.6, 3.1, 5.5_

- [x] 11. Implementar TeamsSection no AdminPage
  - Criar componente `TeamsSection` em `frontend/src/pages/AdminPage.tsx` com formulário de criação (campos: nome, logoUrl opcional) e listagem de times cadastrados
  - Adicionar `<TeamsSection />` ao retorno do componente `AdminPage`
  - _Requirements: 1.1, 1.5_

- [x] 12. Implementar PlayersSection no AdminPage
  - Criar componente `PlayersSection` em `frontend/src/pages/AdminPage.tsx` com formulário de criação (campos: nickname, nome real opcional, select de time, photoUrl opcional) e listagem de jogadores com nickname, time e score atual
  - Adicionar `<PlayersSection />` ao retorno do componente `AdminPage`
  - _Requirements: 2.1, 2.6_

- [x] 13. Implementar MatchStatsSection no AdminPage
  - Criar componente `MatchStatsSection` em `frontend/src/pages/AdminPage.tsx` com formulário de registro de stats: select de game, select de player, campos kills, deaths, assists, totalDamage, rounds e kastPercent
  - Adicionar `<MatchStatsSection />` ao retorno do componente `AdminPage`
  - _Requirements: 3.1_

- [x] 14. Implementar PlayersRankingPage e rota no App.tsx
  - Criar `frontend/src/pages/PlayersRankingPage.tsx` exibindo tabela com posição, nickname, time, score acumulado e número de partidas, consumindo `GET /api/players/ranking`
  - Adicionar rota `/players/ranking` dentro do bloco `<ProtectedRoute>` em `frontend/src/App.tsx`
  - Adicionar link de navegação para `/players/ranking` no `Navbar.tsx`
  - _Requirements: 5.2, 5.5_

  - [ ]* 14.1 Escrever property test — Property 6: Ordenação do ranking
    - Para N jogadores com scores distintos, o endpoint deve retornar lista onde `ranking[i].playerScore ≥ ranking[i+1].playerScore`
    - **Property 6: Ordenação do ranking**
    - **Validates: Requirements 5.1, 5.3**

  - [ ]* 14.2 Escrever property test — Property 7: Completude dos campos do ranking
    - Para qualquer jogador com stats registradas, cada item do ranking deve conter todos os campos obrigatórios não-nulos
    - **Property 7: Completude dos campos do ranking**
    - **Validates: Requirements 5.2**

- [x] 15. Checkpoint final — Garantir que todos os testes passam
  - Garantir que todos os testes passam. Perguntar ao usuário se há dúvidas antes de concluir.

## Notes

- Tasks marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- Os testes de propriedade usam **FsCheck** (`FsCheck.Xunit`) com mínimo de 100 iterações por propriedade
- As propriedades 3–7 testam os serviços com DbContext em memória (`UseInMemoryDatabase`)
- Cada teste de propriedade deve ter o comentário: `// Feature: player-rating-system, Property N: <texto>`
- O `RatingCalculator` é estático e puro — não requer DI
- Seguir o padrão de tratamento de erros de `GamesController` (try/catch com objeto `{ error: { code, message } }`)

# Plano de Implementação: match-stats-per-map

## Visão Geral

Introduz a entidade `MapResult` (mapa específico dentro de uma série) e refatora `MatchStats` para referenciar `MapResultId` em vez de `GameId` + `Rounds`. O campo `Rounds` sai de `MatchStats` e passa a viver em `MapResult`. O rating continua sendo calculado pela mesma fórmula, usando os `Rounds` do `MapResult` correspondente.

Como o banco está em ambiente de teste sem dados reais de `MatchStats`, a migração pode ser destrutiva (drop e recriação da tabela).

## Tarefas

- [x] 1. Criar entidade `MapResult` no domínio e configurar o DbContext
  - Criar `src/FrogBets.Domain/Entities/MapResult.cs` com propriedades `Id`, `GameId`, `MapNumber`, `Rounds`, `CreatedAt` e navegações `Game` e `Stats`
  - Adicionar `DbSet<MapResult> MapResults` em `FrogBetsDbContext`
  - Configurar `OnModelCreating` para `MapResult`: chave primária, índice único `(GameId, MapNumber)`, FK para `Game` com `Restrict`, relação `HasMany(Stats)`
  - _Requirements: 1.1, 1.2, 5.1_

- [x] 2. Refatorar entidade `MatchStats` para referenciar `MapResult`
  - Em `src/FrogBets.Domain/Entities/MatchStats.cs`: substituir `GameId` por `MapResultId`, remover `Rounds`, adicionar navegação `MapResult`
  - Atualizar configuração EF em `OnModelCreating` para `MatchStats`: trocar índice único de `(PlayerId, GameId)` para `(PlayerId, MapResultId)`, remover FK para `Games`, a FK para `MapResults` já é configurada via `HasMany/WithOne` no bloco de `MapResult`
  - _Requirements: 2.1, 2.2, 2.3, 5.2, 5.3_

- [x] 3. Criar migração EF Core destrutiva para `MatchStats` e nova tabela `MapResults`
  - Criar migração `AddMapResultAndRefactorMatchStats` via `dotnet ef migrations add AddMapResultAndRefactorMatchStats --project src/FrogBets.Infrastructure --startup-project src/FrogBets.Api`
  - Como o ambiente é de teste sem dados reais de `MatchStats`, a migração gerada automaticamente (drop + recreate) é suficiente — não é necessário script de migração de dados
  - Verificar que a migração gerada cria `MapResults` com as colunas corretas e recria `MatchStats` com `MapResultId` (NOT NULL) e sem `Rounds`
  - _Requirements: 5.1, 5.2, 5.3_

- [x] 4. Implementar `IMapResultService` e `MapResultService`
  - Criar `src/FrogBets.Api/Services/IMapResultService.cs` com records `CreateMapResultRequest(Guid GameId, int MapNumber, int Rounds)`, `MapResultDto(Guid Id, Guid GameId, int MapNumber, int Rounds, DateTime CreatedAt)` e interface com `CreateMapResultAsync` e `GetByGameAsync`
  - Criar `src/FrogBets.Api/Services/MapResultService.cs` implementando as validações: `INVALID_MAP_NUMBER` (MapNumber < 1), `INVALID_ROUNDS_COUNT` (Rounds <= 0), `MAP_GAME_NOT_FOUND` (GameId inexistente), `MAP_ALREADY_REGISTERED` (duplicata de GameId+MapNumber)
  - Registrar `IMapResultService` / `MapResultService` no DI em `Program.cs`
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [ ]* 4.1 Escrever testes unitários para `MapResultService`
    - Cobrir: criação válida, `MAP_GAME_NOT_FOUND`, `INVALID_MAP_NUMBER`, `INVALID_ROUNDS_COUNT`, `MAP_ALREADY_REGISTERED`
    - _Requirements: 1.1, 1.3, 1.4, 1.5, 1.6_

  - [ ]* 4.2 Escrever property test — Property 1: Unicidade de MapResult por (GameId, MapNumber)
    - **Property 1: Unicidade de MapResult por (GameId, MapNumber)**
    - Gerar pares `(GameId, MapNumber)` aleatórios, tentar criar dois `MapResult` com o mesmo par, verificar que o segundo falha com `MAP_ALREADY_REGISTERED`
    - **Validates: Requirements 1.2, 1.6**

- [x] 5. Refatorar `IMatchStatsService` e `MatchStatsService`
  - Atualizar `RegisterStatsRequest` em `IMatchStatsService.cs`: substituir `GameId` e `Rounds` por `MapResultId`
  - Atualizar `MatchStatsDto`: substituir `GameId` e `Rounds` por `MapResultId`, `MapNumber` e `Rounds` (vindos do `MapResult`)
  - Adicionar `GetStatsByPlayerAsync(Guid playerId)` à interface
  - Reescrever `MatchStatsService.RegisterStatsAsync`: buscar `MapResult` pelo `MapResultId` (erro `MAP_RESULT_NOT_FOUND` se não existir), usar `mapResult.Rounds` no `RatingCalculator.Calculate`, verificar duplicata por `(PlayerId, MapResultId)` (erro `STATS_ALREADY_REGISTERED`), manter acumulação de `PlayerScore` e `MatchesCount`
  - Implementar `GetStatsByPlayerAsync`: retornar `MatchStatsDto[]` com join em `MapResult` para incluir `MapNumber` e `Rounds`
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3_

  - [ ]* 5.1 Escrever testes unitários para `MatchStatsService` refatorado
    - Cobrir: criação válida com `MapResultId`, `MAP_RESULT_NOT_FOUND`, `STATS_ALREADY_REGISTERED`, `INVALID_KAST_VALUE`, acumulação de `PlayerScore`/`MatchesCount`, `GetStatsByPlayerAsync` retorna lista vazia e lista com dados corretos
    - _Requirements: 2.4, 2.5, 3.3, 3.4, 4.2, 4.3_

  - [ ]* 5.2 Escrever property test — Property 2: Unicidade de MatchStats por (PlayerId, MapResultId)
    - **Property 2: Unicidade de MatchStats por (PlayerId, MapResultId)**
    - Gerar `(PlayerId, MapResultId)` aleatórios, registrar stats duas vezes, verificar que o segundo falha com `STATS_ALREADY_REGISTERED` e que `PlayerScore` não muda
    - **Validates: Requirements 2.3, 2.5**

  - [ ]* 5.3 Escrever property test — Property 3: Rating calculado com Rounds do MapResult
    - **Property 3: Rating calculado com Rounds do MapResult**
    - Gerar stats aleatórias e `MapResult` com `Rounds` aleatório, verificar que o rating armazenado é igual a `RatingCalculator.Calculate(kills, deaths, assists, damage, mapResult.Rounds, kastPercent)`
    - **Validates: Requirements 3.1, 3.2**

  - [ ]* 5.4 Escrever property test — Property 4: Acumulação de PlayerScore e MatchesCount
    - **Property 4: Acumulação de PlayerScore e MatchesCount**
    - Gerar jogador com `PlayerScore = S` e `MatchesCount = N` iniciais aleatórios, registrar stats com rating `R`, verificar `PlayerScore = S + R` e `MatchesCount = N + 1`
    - **Validates: Requirements 3.4**

  - [ ]* 5.5 Escrever property test — Property 5: Consulta retorna stats com dados do MapResult
    - **Property 5: Consulta retorna stats com dados do MapResult**
    - Gerar N stats para um jogador, consultar via `GetStatsByPlayerAsync`, verificar que retorna exatamente N itens cada um com `MapNumber` e `Rounds` corretos do `MapResult` correspondente
    - **Validates: Requirements 4.1, 4.2**

- [x] 6. Checkpoint — garantir que todos os testes passam
  - Rodar `dotnet test --configuration Release --verbosity quiet` e confirmar zero falhas antes de prosseguir para o frontend
  - Perguntar ao usuário se há dúvidas antes de continuar.

- [x] 7. Criar `MapResultsController`
  - Criar `src/FrogBets.Api/Controllers/MapResultsController.cs` com:
    - `POST /api/map-results` (admin): recebe `{ gameId, mapNumber, rounds }`, retorna 201 com `MapResultDto`; trata `MAP_GAME_NOT_FOUND` (404), `INVALID_MAP_NUMBER` (400), `INVALID_ROUNDS_COUNT` (400), `MAP_ALREADY_REGISTERED` (409)
    - `GET /api/map-results?gameId=` (admin): retorna lista de `MapResultDto` do game; usa `[AllowAnonymous]` não — manter como admin para popular dropdown
  - Seguir o padrão de verificação de admin via `IsAdminFromDb()` já usado em `PlayersController`
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [x] 8. Refatorar `PlayersController` para o novo contrato de stats
  - Atualizar `RegisterStatsBody`: remover `GameId` e `Rounds`, adicionar `MapResultId`
  - Atualizar chamada a `_matchStatsService.RegisterStatsAsync` para usar o novo `RegisterStatsRequest`
  - Adicionar endpoint `GET /api/players/{id}/stats` que chama `_matchStatsService.GetStatsByPlayerAsync(id)` e retorna 200 com `MatchStatsDto[]`; marcar com `[AllowAnonymous]` (consulta pública conforme Requirement 4)
  - Atualizar tratamento de erros: substituir `INVALID_ROUNDS_COUNT` por `MAP_RESULT_NOT_FOUND` (404)
  - _Requirements: 2.1, 2.4, 2.5, 4.1, 4.2, 4.3_

- [x] 9. Atualizar `frontend/src/api/players.ts`
  - Adicionar interface `MapResult { id: string; gameId: string; mapNumber: number; rounds: number; createdAt: string }`
  - Adicionar `createMapResult(data: { gameId: string; mapNumber: number; rounds: number }): Promise<MapResult>`
  - Adicionar `getMapResultsByGame(gameId: string): Promise<MapResult[]>`
  - Atualizar `RegisterStatsPayload`: remover `gameId` e `rounds`, adicionar `mapResultId: string`
  - Atualizar `MatchStatsDto` (novo tipo): `{ id, playerId, mapResultId, mapNumber, rounds, kills, deaths, assists, totalDamage, kastPercent, rating, createdAt }`
  - Adicionar `getPlayerStats(playerId: string): Promise<MatchStatsDto[]>`
  - _Requirements: 2.1, 4.1, 4.2_

- [x] 10. Refatorar `MatchStatsSection` em `AdminPage.tsx`
  - Adicionar sub-seção "📍 Registrar Mapa" antes de "📊 Estatísticas de Partida":
    - Seleciona Game (InProgress ou Finished) via dropdown
    - Campos `MapNumber` (number, min 1) e `Rounds` (number, min 1)
    - Chama `createMapResult` e exibe feedback de sucesso/erro
  - Refatorar o formulário de registro de stats para fluxo de 3 etapas:
    1. Seleciona Game → carrega MapResults via `getMapResultsByGame`
    2. Seleciona MapResult do dropdown (exibe "Mapa N — X rounds")
    3. Seleciona Jogador e informa kills/deaths/assists/totalDamage/kastPercent (sem campo rounds)
  - Atualizar chamada a `registerMatchStats` para usar `mapResultId` em vez de `gameId` + `rounds`
  - Adicionar entrada `{ id: 'sec-map-results', label: '📍 Mapas' }` no `NAV_ITEMS_BASE`
  - _Requirements: 1.1, 2.1, 4.1_

- [x] 11. Atualizar documentação
  - Atualizar `docs/TECHNICAL.md`: novos endpoints (`POST /api/map-results`, `GET /api/map-results?gameId=`, `GET /api/players/{id}/stats`), entidade `MapResult`, mudança em `MatchStats` (remoção de `Rounds`, adição de `MapResultId`)
  - _Requirements: 5.1, 5.2_

- [x] 12. Checkpoint final — garantir que todos os testes passam
  - Rodar `dotnet test --configuration Release --verbosity quiet` e `npm run test -- --run` no frontend
  - Confirmar zero falhas. Perguntar ao usuário se há dúvidas antes de encerrar.

## Notas

- Tarefas marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- A migração é destrutiva para `MatchStats` (sem dados reais a preservar)
- Cada property test deve incluir o comentário `// Feature: match-stats-per-map, Property N: <texto>` seguindo o padrão do projeto
- O `RatingCalculator` não precisa de alterações — apenas o chamador muda (passa `mapResult.Rounds` em vez de `request.Rounds`)

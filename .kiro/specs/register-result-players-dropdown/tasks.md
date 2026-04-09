# Implementation Plan: register-result-players-dropdown

## Overview

Substituir os campos de texto livre de Player_Markets no `RegisterResultSection` por dropdowns SELECT populados com `Users` reais dos times do jogo. Envolve: (1) reescrever a query do `GamesController.GetGamePlayers` para usar `Users` em vez de `CS2Players`, (2) atualizar o tipo `GamePlayer` no frontend, (3) adicionar estado e lógica de carregamento de jogadores no `RegisterResultSection`, (4) trocar `<input>` por `<select>` nos Player_Markets, e (5) reordenar a exibição dos mercados.

## Tasks

- [x] 1. Atualizar `GamesController.GetGamePlayers` para consultar `Users`
  - Reescrever a query em `src/FrogBets.Api/Controllers/GamesController.cs` no método `GetGamePlayers`
  - Substituir a consulta a `_db.CS2Players` por consulta a `_db.Users` com `Include(u => u.Team)`
  - Filtrar por `u.TeamId.HasValue && teamIds.Contains(u.TeamId.Value)`
  - Ordenar por `u.Team!.Name` e depois por `u.Username`
  - Projetar para `new { id = u.Id, username = u.Username, teamName = u.Team!.Name }`
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [ ] 2. Atualizar o tipo `GamePlayer` e adicionar testes de integração do endpoint
  - [x] 2.1 Atualizar interface `GamePlayer` em `frontend/src/api/players.ts`
    - Substituir campo `nickname: string` por `username: string`
    - _Requirements: 4.1, 4.2_

  - [ ]* 2.2 Escrever testes de integração para `GET /api/games/{id}/players`
    - Criar `tests/FrogBets.IntegrationTests/Controllers/GamesControllerPlayersTests.cs`
    - Testar: retorna 404 com `GAME_NOT_FOUND` para jogo inexistente (Requirement 3.3)
    - Testar: retorna 200 com `[]` quando nenhum usuário pertence aos times (Requirement 3.4)
    - Testar: retorna 401 sem token de autenticação (Requirement 3.5)
    - Testar: retorna 200 com token de usuário comum (Requirement 3.5)
    - Testar: retorna `id`, `username`, `teamName` corretos para usuários dos times (Requirements 3.1, 3.2)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [ ]* 2.3 Escrever property test para `GetGamePlayers` (Property 4)
    - Criar `tests/FrogBets.Tests/GamePlayersEndpointTests.cs`
    - **Property 4: Endpoint returns correct users with correct fields**
    - **Validates: Requirements 3.1, 3.2**

- [ ] 3. Checkpoint — Garantir que todos os testes do backend passam
  - Garantir que todos os testes passam, perguntar ao usuário se houver dúvidas.

- [x] 4. Atualizar `RegisterResultSection` — estado e carregamento de jogadores
  - Modificar `RegisterResultSection` em `frontend/src/pages/AdminPage.tsx`
  - Adicionar estados: `gamePlayers`, `loadingPlayers`, `playersError`
  - No `useEffect` existente, carregar jogadores em paralelo com os mercados via `getGamePlayers(selectedGameId)`
  - Tratar erro de carregamento setando `playersError`
  - Limpar `gamePlayers` e `playersError` ao trocar de jogo (quando `selectedGameId` estiver vazio)
  - _Requirements: 1.1, 1.4, 1.5_

- [ ] 5. Implementar ordenação de mercados e renderização de dropdowns de jogador
  - [x] 5.1 Adicionar função `sortMarkets` e constante `MARKET_ORDER` em `AdminPage.tsx`
    - Definir `MARKET_ORDER`: `MapWinner=0, SeriesWinner=1, TopKills=2, MostDeaths=3, MostUtilityDamage=4`
    - Implementar `sortMarkets` que ordena por tipo e depois por `mapNumber` (null tratado como 0)
    - Aplicar `sortMarkets` na lista de mercados antes de renderizar
    - _Requirements: 2.1, 2.2_

  - [x] 5.2 Substituir `<input type="text">` por `<select>` nos Player_Markets
    - Definir `PLAYER_MARKETS = ['TopKills', 'MostDeaths', 'MostUtilityDamage']`
    - Para Player_Markets: renderizar `<select>` com opção `"— pular —"` como padrão e uma `<option>` por jogador com `value={p.username}` e label `"{p.username} ({p.teamName})"`
    - Desabilitar o `<select>` quando `gamePlayers.length === 0`
    - Exibir mensagem "Nenhum jogador encontrado para este jogo" quando lista estiver vazia
    - Exibir mensagem de erro quando `playersError` estiver setado
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 4.1, 4.2, 4.3_

  - [ ]* 5.3 Escrever testes unitários para `RegisterResultSection`
    - Criar `frontend/src/pages/AdminPage.registerresult.test.tsx`
    - Testar: renderiza `<select>` para Player_Markets quando jogadores são carregados
    - Testar: renderiza opção "— pular —" como padrão
    - Testar: exibe mensagem de erro quando endpoint de jogadores falha
    - Testar: exibe dropdowns desabilitados quando lista de jogadores está vazia
    - Testar: submete `winningOption` com `username` (não `id`)
    - Testar: Team_Markets continuam usando `<select>` de times
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 4.1, 4.2, 4.3_

  - [ ]* 5.4 Escrever property test para dropdown options (Property 1)
    - Criar `frontend/src/pages/AdminPage.registerresult.property.test.tsx`
    - **Property 1: Dropdown options reflect GamePlayer data**
    - **Validates: Requirements 1.2, 4.1, 4.3**

  - [ ]* 5.5 Escrever property test para winningOption submetido (Property 2)
    - Arquivo: `frontend/src/pages/AdminPage.registerresult.property.test.tsx`
    - **Property 2: Submitted winningOption is the selected username**
    - **Validates: Requirements 1.3, 4.2**

  - [ ]* 5.6 Escrever property test para ordenação de mercados (Property 3)
    - Arquivo: `frontend/src/pages/AdminPage.registerresult.property.test.tsx`
    - **Property 3: Market ordering groups Team_Markets before Player_Markets**
    - **Validates: Requirements 2.1, 2.2**

- [ ] 6. Checkpoint final — Garantir que todos os testes passam
  - Garantir que todos os testes passam (`dotnet test` + `npm run test`), perguntar ao usuário se houver dúvidas.

## Notes

- Tasks marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- Cada task referencia os requisitos específicos para rastreabilidade
- O backend não requer nova migração — apenas a query do `GetGamePlayers` muda
- O campo `winningOption` enviado ao `POST /api/games/{id}/results` continua sendo `username` (string), sem alteração no contrato do endpoint de resultado
- Property tests usam `fast-check` (já disponível via `frontend/src/test/fc-helpers.ts`) com mínimo 100 iterações

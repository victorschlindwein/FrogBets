# Implementation Plan

- [x] 1. Write bug condition exploration tests
  - **Property 1: Bug Condition** - Marketplace BetDto Flat Fields + Missing CS2Players
  - **CRITICAL**: These tests MUST FAIL on unfixed code — failure confirms the bugs exist
  - **DO NOT attempt to fix the tests or the code when they fail**
  - **NOTE**: These tests encode the expected behavior — they will validate the fix when they pass after implementation
  - **GOAL**: Surface counterexamples that demonstrate both bugs exist
  - **Bug 1 — Frontend (Vitest + fast-check):**
    - Scope: any `BetDto[]` response where `response.length > 0 AND response[0].market === undefined AND response[0].marketType !== undefined`
    - Mock `GET /api/marketplace` returning `[{ id, marketId, marketType: "MapWinner", mapNumber: 1, gameId, creatorOption, amount, creatorId }]` (campos planos, sem `market` aninhado)
    - Render `<MarketplacePage />` and assert it does NOT throw and renders the bet row
    - Use fast-check: `fc.array(fc.record({ marketType: fc.constantFrom("MapWinner","SeriesWinner","TopKills"), mapNumber: fc.option(fc.integer({min:1,max:5})), gameId: fc.uuid(), ... }), {minLength:1})` → assert no TypeError thrown
    - Run on UNFIXED code — **EXPECTED OUTCOME**: Test FAILS with `TypeError: Cannot read properties of undefined (reading 'type')`
    - Document counterexample: `marketLabel(undefined)` throws TypeError
  - **Bug 2 — Backend (xUnit + FsCheck):**
    - Scope: any game where `CS2Players.Count > Users.Where(TeamId in teamIds).Count` OR any CS2Player with `UserId == null`
    - Seed: game with TeamA + TeamB, 2 CS2Players in TeamA (one with UserId=null), 2 CS2Players in TeamB (one with UserId=null)
    - Call `GET /api/games/{id}/players` and assert response contains all 4 CS2Players
    - Run on UNFIXED code — **EXPECTED OUTCOME**: Test FAILS returning only users with TeamId (misses CS2Players without UserId)
    - Document counterexample: response has N < 4 when some CS2Players have no linked User
  - Mark task complete when tests are written, run, and failures are documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Marketplace Empty State + 404 for Missing Game
  - **IMPORTANT**: Follow observation-first methodology — observe UNFIXED code behavior for non-buggy inputs
  - **Bug 1 — Preservation (Vitest):**
    - Observe: `GET /api/marketplace` returning `[]` → component renders "Nenhuma aposta disponível para cobertura" on unfixed code
    - Write test: mock empty response → assert message "Nenhuma aposta disponível para cobertura" is visible
    - Observe: cover bet flow → `POST /bets/{id}/cover` success → bet removed from list on unfixed code
    - Write test: mock marketplace with 1 bet + mock cover success → assert bet disappears after cover
    - Verify both tests PASS on UNFIXED code
  - **Bug 2 — Preservation (xUnit + FsCheck):**
    - Observe: `GET /api/games/{id}/players` with non-existent gameId → 404 with `GAME_NOT_FOUND` on unfixed code
    - Write property-based test: for any random Guid not in the database, endpoint returns 404 with `GAME_NOT_FOUND`
    - Use FsCheck: `Prop.ForAll(Arb.Default.Guid(), async guid => { var res = await client.GetAsync($"/api/games/{guid}/players"); return res.StatusCode == HttpStatusCode.NotFound; })`
    - Verify test PASSES on UNFIXED code
  - **EXPECTED OUTCOME**: All preservation tests PASS (confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.5_

- [x] 3. Fix for marketplace tela branca + players faltando nas apostas

  - [x] 3.1 Bug 1 — Frontend: adicionar interface `BetDtoResponse` e mapear para `MarketplaceBet`
    - Em `frontend/src/pages/MarketplacePage.tsx`, adicionar interface `BetDtoResponse` com campos planos: `id`, `marketId`, `marketType`, `mapNumber`, `gameId`, `creatorOption`, `amount`, `creatorId`
    - Adicionar função `mapDtoToMarketplaceBet(dto: BetDtoResponse): MarketplaceBet` que retorna `{ ...dto, market: { type: dto.marketType, mapNumber: dto.mapNumber, gameId: dto.gameId } }`
    - No `useEffect`, substituir `apiClient.get<MarketplaceBet[]>('/marketplace').then(res => setBets(res.data))` por `apiClient.get<BetDtoResponse[]>('/marketplace').then(res => setBets(res.data.map(mapDtoToMarketplaceBet)))`
    - A interface `MarketplaceBet` existente permanece inalterada
    - _Bug_Condition: `response.length > 0 AND response[0].market === undefined AND response[0].marketType !== undefined`_
    - _Expected_Behavior: `bet.market.type === dto.marketType AND bet.market.mapNumber === dto.mapNumber AND bet.market.gameId === dto.gameId AND noExceptionThrown`_
    - _Preservation: marketplace vazio continua exibindo mensagem; cobertura de aposta continua removendo da lista_
    - _Requirements: 2.1, 2.2, 3.1, 3.2_

  - [x] 3.2 Bug 2 — Backend: substituir query `_db.Users` por `_db.CS2Players` em `GetGamePlayers`
    - Em `src/FrogBets.Api/Controllers/GamesController.cs`, na action `GetGamePlayers`, substituir a query em `_db.Users` por `_db.CS2Players`
    - Trocar `Include(u => u.Team)` por `Include(p => p.Team)`
    - Trocar `Where(u => u.TeamId.HasValue && teamIds.Contains(u.TeamId.Value))` por `Where(p => p.TeamId.HasValue && teamIds.Contains(p.TeamId.Value))`
    - Trocar `OrderBy(u => u.Team!.Name).ThenBy(u => u.Username)` por `OrderBy(p => p.Team!.Name).ThenBy(p => p.Nickname)`
    - Trocar `Select(u => new { id = u.Id, nickname = u.Username, teamName = u.Team!.Name })` por `Select(p => new { id = p.Id, nickname = p.Nickname, teamName = p.Team!.Name })`
    - O contrato de resposta (`id`, `nickname`, `teamName`) permanece idêntico — sem breaking change
    - _Bug_Condition: `CS2Players.Count > Users.Where(TeamId in teamIds).Count OR EXISTS p WHERE p.UserId IS NULL`_
    - _Expected_Behavior: `result.Count === CS2Players.Count(p => teamIds.Contains(p.TeamId)) AND ALL p: p.nickname === CS2Player.Nickname`_
    - _Preservation: jogo inexistente continua retornando 404 com GAME_NOT_FOUND_
    - _Requirements: 2.3, 2.4, 3.5_

  - [x] 3.3 Verify bug condition exploration tests now pass
    - **Property 1: Expected Behavior** - Marketplace renders BetDto + All CS2Players returned
    - **IMPORTANT**: Re-run the SAME tests from task 1 — do NOT write new tests
    - Run Bug 1 exploration test: assert `<MarketplacePage />` renders without TypeError when API returns flat BetDto fields
    - Run Bug 2 exploration test: assert `GET /api/games/{id}/players` returns all 4 CS2Players including those with `UserId == null`
    - **EXPECTED OUTCOME**: Both tests PASS (confirms both bugs are fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.4 Verify preservation tests still pass
    - **Property 2: Preservation** - Empty marketplace + 404 for missing game
    - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
    - Run marketplace empty state test: assert "Nenhuma aposta disponível para cobertura" still renders
    - Run cover bet test: assert bet is removed from list after successful cover
    - Run 404 property test: assert any random Guid still returns 404 with `GAME_NOT_FOUND`
    - **EXPECTED OUTCOME**: All preservation tests PASS (confirms no regressions)

- [x] 4. Checkpoint — Ensure all tests pass
  - Run `cd frontend && npm run test` — all Vitest tests must pass
  - Run `dotnet test --configuration Release --verbosity quiet` — all xUnit tests must pass
  - Run `npx tsc --noEmit` in frontend — zero TypeScript errors
  - Ensure all tests pass; ask the user if questions arise

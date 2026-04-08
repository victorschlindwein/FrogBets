# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Mercados de Jogador Renderizam Input de Texto
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to the three concrete failing market types: TopKills, MostDeaths, MostUtilityDamage
  - Create `frontend/src/pages/GameDetailPage.test.tsx`
  - Render `BetForm` with `market.type = 'TopKills'` and assert that `<select>` is present (not `<input type="text">`)
  - Repeat for `MostDeaths` and `MostUtilityDamage`
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (this is correct - it proves the bug exists)
  - Document counterexamples found: e.g., "BetForm with TopKills renders `<input type='text'>` instead of `<select>`"
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Mercados de Time Continuam Renderizando Dropdown Correto
  - **IMPORTANT**: Follow observation-first methodology
  - Observe behavior on UNFIXED code for non-buggy inputs (MapWinner, SeriesWinner)
  - Observe: `BetForm` with `MapWinner` renders `<select>` with `game.teamA` and `game.teamB` options
  - Observe: `BetForm` with `SeriesWinner` renders `<select>` with `game.teamA` and `game.teamB` options
  - Write property-based tests: for all team market types, `<select>` with teamA and teamB is rendered
  - Use FsCheck-style approach in Vitest: generate arbitrary teamA/teamB names and verify dropdown options match
  - Verify tests PASS on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.3_

- [x] 3. Fix: substituir input de texto por dropdown de jogadores em mercados de jogador

  - [x] 3.1 Adicionar endpoint GET /api/games/{id}/players em GamesController.cs
    - Adicionar action `GetGamePlayers(Guid id)` com `[HttpGet("{id:guid}/players")]` e `[Authorize]`
    - Buscar o jogo pelo id; retornar 404 com `GAME_NOT_FOUND` se não existir
    - Consultar `CS2Players` com `Include(p => p.Team)` filtrando por `p.Team.Name == game.TeamA || p.Team.Name == game.TeamB`
    - Ordenar por `Team.Name` e `Nickname`; projetar em `new { nickname = p.Nickname, teamName = p.Team.Name }`
    - _Bug_Condition: isBugCondition(market) where market.type IN ['TopKills', 'MostDeaths', 'MostUtilityDamage']_
    - _Expected_Behavior: GET /api/games/{id}/players retorna lista de { nickname, teamName } dos jogadores dos dois times_
    - _Preservation: endpoint é [Authorize] (não admin-only), não altera nenhum endpoint existente_
    - _Requirements: 2.1, 2.2_

  - [x] 3.2 Adicionar getGamePlayers em frontend/src/api/players.ts
    - Adicionar interface `GamePlayer { nickname: string; teamName: string }`
    - Adicionar função `getGamePlayers(gameId: string): Promise<GamePlayer[]>` chamando `GET /games/{gameId}/players`
    - _Requirements: 2.1_

  - [x] 3.3 Atualizar GameDetailPage.tsx para buscar jogadores e passar para BetForm
    - Adicionar constante `PLAYER_MARKETS = ['TopKills', 'MostDeaths', 'MostUtilityDamage']`
    - Adicionar estado `const [players, setPlayers] = useState<GamePlayer[]>([])`
    - Adicionar `useEffect` que chama `getGamePlayers(id)` após carregar o jogo; em caso de erro, manter `[]`
    - Adicionar prop `players: GamePlayer[]` ao `BetForm`
    - Substituir o branch `else` no `BetForm`: quando `isPlayerMarket` (PLAYER_MARKETS.includes(market.type)), renderizar `<select>` com os jogadores no formato `"Nickname - Nome do Time"`; manter `<input type="text">` apenas como fallback para tipos futuros
    - Passar `players={players}` ao renderizar `<BetForm>` em `GameDetailPage`
    - _Bug_Condition: isBugCondition(market) where market.type IN PLAYER_MARKETS AND campo renderizado é `<input type="text">`_
    - _Expected_Behavior: `<select>` com options value=nickname label="nickname - teamName" para cada jogador_
    - _Preservation: branch isTeamMarket permanece intacto; creatorOption continua sendo enviado no POST /api/bets_
    - _Requirements: 2.1, 2.2, 3.1, 3.2, 3.3_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Mercados de Jogador Renderizam Dropdown
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - Run `GameDetailPage.test.tsx` bug condition tests
    - **EXPECTED OUTCOME**: Tests PASS (confirms bug is fixed — BetForm renders `<select>` for TopKills, MostDeaths, MostUtilityDamage)
    - _Requirements: 2.1, 2.2_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - Mercados de Time Não São Afetados
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions — MapWinner and SeriesWinner still render team dropdown)
    - _Requirements: 3.1, 3.3_

- [x] 4. Checkpoint - Ensure all tests pass
  - Run `dotnet test --configuration Release --verbosity quiet` — all .NET tests must pass
  - Run `cd frontend && npm run test -- --run` — all frontend tests must pass
  - Ensure all tests pass, ask the user if questions arise.

# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Fluxo E2E Completo Funciona Corretamente
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bugs exist in the E2E flow
  - **Scoped E2E Approach**: Execute the complete flow step-by-step to identify exact failure points
  - Create Cypress E2E test in `frontend/cypress/e2e/complete-flow.cy.ts` that executes:
    - Register 4 users (user1, user2, user3, user4) with valid invite tokens
    - Admin creates TeamA and TeamB
    - Admin adds user1, user2 to TeamA and user3, user4 to TeamB as CS2Players
    - Admin creates game TeamA vs TeamB with 3 maps
    - Verify player dropdowns in TopKills/MostDeaths/MostUtilityDamage show exactly 4 players
    - user1 creates bet on MapWinner (Map 1) choosing TeamA
    - user2 covers user1's bet
    - Verify bet is removed from marketplace after coverage
    - Admin starts game (status → InProgress)
    - Admin finishes game (status → Finished) and marks MapWinner (Map 1) result as TeamA
    - Verify settlement: user1 received 2× amount, user2 lost amount, WinsCount/LossesCount updated
    - Verify balance invariant: `VirtualBalance + ReservedBalance` is correct for all 4 users
  - Test assertions should match the Expected Behavior Properties from design (Requirements 2.1-2.6)
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (this is correct - it proves the bugs exist)
  - Document counterexamples found to understand root causes:
    - Which step fails first?
    - Are player dropdowns empty or showing wrong players?
    - Does bet coverage fail or not update marketplace?
    - Does settlement fail to update WinsCount/LossesCount?
    - Is balance invariant violated?
  - Mark task complete when test is written, run, and failures are documented
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Comportamento Existente Não Muda
  - **IMPORTANT**: Follow observation-first methodology
  - Observe behavior on UNFIXED code for non-E2E-flow operations
  - Run all existing E2E tests on UNFIXED code and document their current behavior:
    - `frontend/cypress/e2e/auth.cy.ts` - authentication flow
    - `frontend/cypress/e2e/bets.cy.ts` - isolated bet operations
    - `frontend/cypress/e2e/games.cy.ts` - game listing and bet creation
    - `frontend/cypress/e2e/marketplace.cy.ts` - marketplace operations
    - `frontend/cypress/e2e/teams.cy.ts` - team management
    - `frontend/cypress/e2e/leaderboard.cy.ts` - leaderboard display
    - `frontend/cypress/e2e/players-ranking.cy.ts` - player ranking
    - `frontend/cypress/e2e/navbar.cy.ts` - navigation
    - `frontend/cypress/e2e/dashboard.cy.ts` - dashboard display
  - Verify all existing tests PASS on UNFIXED code (baseline behavior)
  - Document any business rule validations that should be preserved:
    - CANNOT_COVER_OWN_BET
    - DUPLICATE_BET_ON_MARKET
    - INSUFFICIENT_BALANCE
    - CANNOT_CANCEL_ACTIVE_BET
    - Balance invariant in isolated operations
    - Route protection (redirect to /login when not authenticated)
    - Access control (403 for non-admins on admin endpoints)
  - **EXPECTED OUTCOME**: All existing tests PASS (confirms baseline behavior to preserve)
  - Mark task complete when all tests are run and passing behavior is documented
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

- [x] 3. Fix for Fluxo E2E Completo da Plataforma FrogBets

  - [x] 3.1 Analyze exploration test failures and confirm root causes
    - Review documented counterexamples from task 1
    - Identify which of the 6 hypothesized root causes are confirmed:
      1. Endpoint GET /api/games/{id}/players not returning correct players
      2. Frontend not populating dropdowns correctly
      3. Bet coverage not removing from marketplace
      4. Settlement not updating WinsCount/LossesCount
      5. Race condition in bet coverage
      6. User registration not creating CS2Player automatically
    - If hypotheses are refuted, re-hypothesize based on actual failures
    - Document confirmed root causes before proceeding to implementation
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 3.2 Implement fix for player dropdowns (if confirmed as root cause)
    - **File**: `frontend/src/pages/GameDetailPage.tsx`
    - Add fetch of players via `GET /api/games/{id}/players` on page load
    - Store player list in local state
    - Format player market options as `"<nickname>"` and `"NOT_<nickname>"`
    - Populate TopKills/MostDeaths/MostUtilityDamage dropdowns with player options
    - Ensure only players from the two teams of the game are displayed
    - **File**: `src/FrogBets.Api/Controllers/GamesController.cs` (if endpoint needs fixes)
    - Verify `GetGamePlayers` filters only players from the two specific teams
    - Add filter `!p.Team.IsDeleted` to exclude deleted teams
    - Order players consistently (by team name, then nickname)
    - _Bug_Condition: isBugCondition(input) where input includes "verify_player_dropdowns" step_
    - _Expected_Behavior: allPlayersVisibleInDropdowns(result) = true for all 4 players_
    - _Preservation: All existing game and marketplace tests continue passing_
    - _Requirements: 1.2, 2.2, 3.1_

  - [x] 3.3 Implement fix for bet coverage marketplace update (if confirmed as root cause)
    - **File**: `frontend/src/pages/MarketplacePage.tsx`
    - Update `handleCover` function to remove covered bet from local state
    - Filter covered bet: `bets.filter(b => b.id !== betId)` after successful coverage
    - Or re-fetch marketplace list after coverage
    - Ensure UI updates immediately after coverage
    - _Bug_Condition: isBugCondition(input) where input includes "cover_bets_by_other_users" step_
    - _Expected_Behavior: betCoverageSuccessful(result) = true, bet removed from marketplace_
    - _Preservation: All existing marketplace tests continue passing_
    - _Requirements: 1.3, 2.3, 3.1_

  - [x] 3.4 Implement fix for settlement WinsCount/LossesCount (if confirmed as root cause)
    - **File**: `src/FrogBets.Api/Services/SettlementService.cs`
    - Update `SettleMarketAsync` to increment `user.WinsCount` for winner
    - Increment `user.LossesCount` for loser
    - Save changes to database after updating counters
    - Verify logic for determining winner vs loser is correct:
      - For team markets: compare creatorOption/covererOption with winningOption
      - For player markets: handle `NOT_` prefix correctly
    - _Bug_Condition: isBugCondition(input) where input includes "verify_settlement" step_
    - _Expected_Behavior: settlementCorrect(result) = true, counters updated correctly_
    - _Preservation: All existing bet and settlement tests continue passing_
    - _Requirements: 1.4, 2.4, 3.1_

  - [x] 3.5 Implement fix for CS2Player creation on registration (if confirmed as root cause)
    - **File**: `src/FrogBets.Api/Services/AuthService.cs`
    - Verify `RegisterAsync` creates CS2Player when `teamId.HasValue`
    - Ensure player creation doesn't fail silently
    - Add robust error handling and logging
    - Verify transaction is not committed if player creation fails
    - Test registration flow with teamId to confirm player is created
    - _Bug_Condition: isBugCondition(input) where input includes "add_2_players_per_team" step_
    - _Expected_Behavior: Players are created and visible in game dropdowns_
    - _Preservation: All existing auth tests continue passing_
    - _Requirements: 1.1, 2.1, 3.1_

  - [x] 3.6 Verify race condition protection (if confirmed as issue)
    - **File**: `src/FrogBets.Api/Services/BetService.cs`
    - Verify `CoverBetAsync` uses `IsolationLevel.Serializable` transaction
    - Ensure status check and update are atomic
    - Verify `SELECT FOR UPDATE` pattern is used correctly
    - Add integration test for concurrent coverage attempts
    - _Bug_Condition: isBugCondition(input) where input includes concurrent operations_
    - _Expected_Behavior: Only one coverage succeeds, no double-coverage_
    - _Preservation: All existing bet tests continue passing_
    - _Requirements: 1.6, 2.6, 3.1_

  - [x] 3.7 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Fluxo E2E Completo Funciona Corretamente
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms the expected behavior is satisfied
    - Run complete E2E flow test from `frontend/cypress/e2e/complete-flow.cy.ts`
    - Verify all steps complete successfully:
      - 4 users registered
      - 2 teams created
      - 4 players added (2 per team)
      - Game created with 3 maps
      - Player dropdowns show exactly 4 correct players
      - Bet created and covered successfully
      - Bet removed from marketplace after coverage
      - Game started and finished
      - Settlement correct: balances and counters updated
      - Balance invariant preserved for all users
    - **EXPECTED OUTCOME**: Test PASSES (confirms bugs are fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 3.8 Verify preservation tests still pass
    - **Property 2: Preservation** - Comportamento Existente Não Muda
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run all existing E2E tests and verify they still pass:
      - `npm run test:e2e` (runs all Cypress tests)
    - Verify specific test suites:
      - Auth flow (login, register, logout, route protection)
      - Bets flow (list, cancel, details)
      - Games flow (list, create bet, insufficient balance error)
      - Marketplace flow (list, cover bet, errors)
      - Teams flow (list, create)
      - Leaderboard, players-ranking, navbar, dashboard
    - Verify business rules still enforced:
      - CANNOT_COVER_OWN_BET returns error
      - DUPLICATE_BET_ON_MARKET returns error
      - INSUFFICIENT_BALANCE returns error
      - CANNOT_CANCEL_ACTIVE_BET returns error
    - Verify access control:
      - Non-authenticated users redirected to /login
      - Non-admins receive 403 on admin endpoints
    - **EXPECTED OUTCOME**: All tests PASS (confirms no regressions)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

- [x] 4. Checkpoint - Ensure all tests pass
  - Run complete test suite: `dotnet test --configuration Release --verbosity quiet`
  - Run frontend unit tests: `cd frontend && npm run test`
  - Run E2E tests: `cd frontend && npm run test:e2e`
  - Verify TypeScript compilation: `cd frontend && npx tsc --noEmit`
  - Ensure all tests pass with zero failures
  - If any issues arise, ask the user for guidance before proceeding
  - Document any remaining concerns or edge cases discovered

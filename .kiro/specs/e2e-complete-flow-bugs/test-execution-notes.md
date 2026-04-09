# E2E Bug Condition Exploration Test - Execution Notes

## Test Location
`frontend/cypress/e2e/complete-flow.cy.ts`

## Test Purpose
This test executes the complete E2E flow to identify bugs in the FrogBets platform. It is EXPECTED TO FAIL on unfixed code - failures confirm the bugs exist.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**

## Prerequisites

### 1. Backend API Running
The backend API must be running on `http://localhost:8080`:

```powershell
# From project root
dotnet run --project src/FrogBets.Api
```

### 2. Frontend Dev Server Running
The frontend must be running on `http://localhost:5173`:

```powershell
# From frontend directory
cd frontend
npm run dev
```

### 3. Database Setup
Ensure PostgreSQL is running and migrations are applied:

```powershell
# Apply migrations (if not auto-applied)
dotnet ef database update --project src/FrogBets.Infrastructure --startup-project src/FrogBets.Api
```

### 4. Admin User Exists
The test uses admin credentials to create invites and manage the game. Ensure an admin user exists:

- Username: `admin` (or set `CYPRESS_adminUsername` env var)
- Password: `admin123456` (or set `CYPRESS_adminPassword` env var)

## Running the Test

### Headless Mode (CI/Automated)
```powershell
cd frontend
npm run test:e2e -- --spec "cypress/e2e/complete-flow.cy.ts"
```

### Interactive Mode (Development)
```powershell
cd frontend
npm run test:e2e:open
# Then select complete-flow.cy.ts from the Cypress UI
```

## Expected Failures (Unfixed Code)

Based on the root cause hypotheses in the design document, the test is expected to fail at one or more of these points:

### Failure Point 1: Player Dropdowns (Step 9)
**Expected Error:** Endpoint `/api/games/{id}/players` returns 404 or incorrect players

**Symptoms:**
- Endpoint not found (404)
- Returns empty array
- Returns players from wrong teams
- Returns players from deleted teams

**Root Cause Hypothesis:**
- Endpoint `GET /api/games/{id}/players` not implemented
- Or endpoint filters players incorrectly

**Log Output:**
```
❌ EXPECTED FAILURE: Endpoint /api/games/{id}/players not found (404)
   Root cause hypothesis: Endpoint not implemented
```

### Failure Point 2: Marketplace After Coverage (Step 14)
**Expected Error:** Bet still appears in marketplace after being covered

**Symptoms:**
- Bet with status "Active" still in marketplace response
- Frontend doesn't remove bet from local state

**Root Cause Hypothesis:**
- Frontend not updating state after coverage
- Backend `/api/marketplace` endpoint not filtering Active bets

**Log Output:**
```
❌ EXPECTED FAILURE: Bet still appears in marketplace after coverage
   Root cause hypothesis: Frontend not updating state or backend not filtering correctly
```

### Failure Point 3: WinsCount/LossesCount (Step 20)
**Expected Error:** WinsCount and LossesCount not incremented after settlement

**Symptoms:**
- Winner's `winsCount` remains 0
- Loser's `lossesCount` remains 0
- Balances updated correctly but counters not

**Root Cause Hypothesis:**
- `SettlementService.SettleMarketAsync` not updating counters
- Only updates balances, forgets to increment wins/losses

**Log Output:**
```
❌ EXPECTED FAILURE: User1 WinsCount not incremented after winning bet
   Root cause hypothesis: SettlementService not updating WinsCount/LossesCount

❌ EXPECTED FAILURE: User2 LossesCount not incremented after losing bet
   Root cause hypothesis: SettlementService not updating WinsCount/LossesCount
```

### Failure Point 4: Balance Invariant (Step 21)
**Expected Error:** Balance invariant violated after settlement

**Symptoms:**
- `VirtualBalance + ReservedBalance` doesn't match expected total
- Money "disappears" or is "created" incorrectly

**Root Cause Hypothesis:**
- Settlement logic has arithmetic error
- Race condition in balance updates

## Test Flow Summary

The test executes these steps in sequence:

1. ✅ Admin logs in
2. ✅ Admin creates 4 invite tokens
3. ✅ Register 4 users (user1, user2, user3, user4)
4. ✅ Admin creates TeamA
5. ✅ Admin creates TeamB
6. ✅ Admin adds user1, user2 to TeamA as CS2Players
7. ✅ Admin adds user3, user4 to TeamB as CS2Players
8. ✅ Admin creates game TeamA vs TeamB with 3 maps
9. ❌ **Verify player dropdowns show exactly 4 players** (EXPECTED FAILURE)
10. ✅ Capture initial balances
11. ✅ user1 creates bet on MapWinner (Map 1) choosing TeamA
12. ✅ Verify bet appears in marketplace
13. ✅ user2 covers user1's bet
14. ❌ **Verify bet removed from marketplace** (EXPECTED FAILURE)
15. ✅ Verify balances after coverage
16. ✅ Admin starts game
17. ✅ Admin finishes game
18. ✅ Admin settles MapWinner (Map 1) with result TeamA
19. ✅ Verify settlement balances
20. ❌ **Verify WinsCount/LossesCount updated** (EXPECTED FAILURE)
21. ✅ Verify balance invariant

## Interpreting Results

### If Test Passes Unexpectedly
This is a **CRITICAL ISSUE** - it means the test doesn't properly detect the bugs. Possible reasons:
- Code already has fixes applied
- Root cause hypotheses are incorrect
- Test logic has errors

**Action:** Report to orchestrator for re-investigation.

### If Test Fails as Expected
This is the **SUCCESS CASE** for exploration tests - it confirms the bugs exist.

**Action:** Document the counterexamples found and proceed to implementation tasks.

## Counterexample Documentation

After running the test, document the actual failures observed:

### Counterexample 1: [Description]
- **Step:** [Step number and name]
- **Expected:** [What should happen]
- **Actual:** [What actually happened]
- **Error Message:** [Exact error from test]
- **Root Cause Confirmed:** [Yes/No/Partial]

### Counterexample 2: [Description]
[Same format as above]

## Next Steps After Test Execution

1. Review test output and screenshots (if any)
2. Document all counterexamples found
3. Confirm or refute root cause hypotheses
4. If hypotheses confirmed: proceed to Task 2 (implementation)
5. If hypotheses refuted: re-investigate and update design document

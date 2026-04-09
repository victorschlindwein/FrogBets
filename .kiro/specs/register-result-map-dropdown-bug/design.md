# Register Result Map Dropdown Bug — Bugfix Design

## Overview

Na `MatchStatsSection` do `AdminPage.tsx`, o `useEffect` que carrega mapas e jogadores ao selecionar um jogo usa `Promise.all([getMapResultsByGame, getGamePlayers])` com um único `.catch(() => { setMapResults([]); setPlayers([]) })`. Quando `getGamePlayers` falha (ex: nenhum usuário tem `TeamId` configurado para os times do jogo), o catch descarta silenciosamente os mapas já carregados, zerando `mapResults` e exibindo "Nenhum mapa registrado para este jogo." mesmo com mapas existentes.

O fix consiste em separar os tratamentos de erro das duas chamadas, garantindo que a falha de uma não afete o resultado da outra. A abordagem recomendada é `Promise.allSettled` ou duas chamadas independentes com catches separados.

## Glossary

- **Bug_Condition (C)**: A condição que dispara o bug — `getGamePlayers` falha enquanto `getMapResultsByGame` retorna dados com sucesso
- **Property (P)**: O comportamento correto — `mapResults` deve refletir o retorno de `getMapResultsByGame` independentemente do resultado de `getGamePlayers`
- **Preservation**: O comportamento existente que não deve ser alterado pelo fix — carregamento normal quando ambas as chamadas têm sucesso, limpeza de estado ao trocar de jogo, e o fluxo de submit de estatísticas
- **MatchStatsSection**: O componente em `frontend/src/pages/AdminPage.tsx` (linha ~877) que gerencia o formulário de registro de estatísticas de partida
- **statsGameId**: O estado que controla qual jogo está selecionado; sua mudança dispara o `useEffect` de carregamento
- **mapResults**: Estado que armazena os `MapResult[]` retornados por `getMapResultsByGame`; é o estado afetado pelo bug

## Bug Details

### Bug Condition

O bug manifesta quando o admin seleciona um jogo na seção "Estatísticas de Partida" e `getGamePlayers` lança um erro. O `Promise.all` rejeita imediatamente ao primeiro erro, e o `.catch` compartilhado zera tanto `mapResults` quanto `players`, descartando os dados de mapas mesmo que `getMapResultsByGame` tenha retornado com sucesso.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input = { gameId: string, mapsResult: Result<MapResult[]>, playersResult: Result<GamePlayer[]> }
  OUTPUT: boolean

  RETURN input.mapsResult.isSuccess = true
         AND input.playersResult.isFailure = true
         AND input.gameId IS NOT EMPTY
END FUNCTION
```

### Examples

- Admin seleciona jogo "FURIA vs NAVI" que tem 2 mapas registrados, mas nenhum usuário tem `TeamId` configurado → `getGamePlayers` lança erro → dropdown de Mapa exibe "Nenhum mapa registrado para este jogo." (bug)
- Admin seleciona jogo onde `GET /games/{id}/players` retorna 404 ou 500 → mesmo resultado: mapas são descartados (bug)
- Admin seleciona jogo onde ambas as chamadas têm sucesso → dropdown de Mapa e dropdown de Jogador populados corretamente (sem bug)
- Admin seleciona jogo sem nenhum mapa registrado E `getGamePlayers` falha → "Nenhum mapa registrado para este jogo." (comportamento correto, não é bug)

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Quando ambas as chamadas retornam com sucesso, o dropdown de Mapa e o dropdown de Jogador devem continuar sendo populados normalmente
- Quando o jogo selecionado não possui mapas registrados (`getMapResultsByGame` retorna `[]`), a mensagem "Nenhum mapa registrado para este jogo." deve continuar sendo exibida
- Ao trocar de jogo, os estados `mapResults`, `players`, `mapResultId` e `playerId` devem continuar sendo limpos antes do novo carregamento
- O fluxo de submit de estatísticas via `POST /api/players/{playerId}/stats` não deve ser afetado

**Scope:**
Todos os inputs que NÃO envolvem a condição de bug (falha isolada de `getGamePlayers`) devem ser completamente inalterados pelo fix. Isso inclui:
- Seleção de jogo com ambas as chamadas bem-sucedidas
- Seleção de jogo sem mapas registrados
- Submit do formulário de estatísticas
- Troca de jogo selecionado

## Hypothesized Root Cause

Com base na análise do commit ff51291 e no código atual:

1. **Catch compartilhado no Promise.all**: O `Promise.all` rejeita ao primeiro erro e o `.catch` único zera ambos os estados (`mapResults` e `players`). Não há como distinguir qual chamada falhou.

2. **Ausência de tratamento de erro independente**: Antes do commit ff51291, apenas `getMapResultsByGame` era chamada. Ao adicionar `getGamePlayers` em paralelo, o desenvolvedor não considerou que a falha de jogadores deveria ser tratada de forma isolada.

3. **Endpoint `/games/{id}/players` pode falhar legitimamente**: Quando nenhum usuário tem `TeamId` configurado para os times do jogo, o endpoint pode retornar erro ou lista vazia dependendo da implementação — tornando a falha de `getGamePlayers` um cenário comum em ambientes de desenvolvimento/staging.

## Correctness Properties

Property 1: Bug Condition — Falha de getGamePlayers não afeta mapResults

_For any_ seleção de jogo onde `getMapResultsByGame` retorna uma lista não-vazia com sucesso e `getGamePlayers` falha com qualquer erro, o `useEffect` corrigido SHALL preservar `mapResults` com os dados retornados por `getMapResultsByGame`, exibindo o dropdown de Mapa populado corretamente.

**Validates: Requirements 2.1, 2.2**

Property 2: Preservation — Comportamento inalterado para inputs fora da bug condition

_For any_ seleção de jogo onde a bug condition NÃO se aplica (ambas as chamadas têm sucesso, ou `getMapResultsByGame` também falha, ou nenhum jogo está selecionado), o `useEffect` corrigido SHALL produzir exatamente o mesmo resultado que o código original, preservando o comportamento de carregamento normal, limpeza de estado e exibição de mensagens.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

## Fix Implementation

### Changes Required

**File**: `frontend/src/pages/AdminPage.tsx`

**Function**: `MatchStatsSection` — `useEffect` (linha ~899)

**Specific Changes**:

1. **Substituir `Promise.all` por `Promise.allSettled`**: Permite que ambas as chamadas completem independentemente, retornando o status de cada uma (`fulfilled` ou `rejected`).

2. **Tratar `mapResults` independentemente**: Se `getMapResultsByGame` for `fulfilled`, setar `mapResults` com o valor retornado. Se for `rejected`, setar `mapResults = []`.

3. **Tratar `players` independentemente**: Se `getGamePlayers` for `fulfilled`, setar `players` com o valor retornado. Se for `rejected`, setar `players = []` (comportamento atual preservado para este estado).

4. **Adicionar estado de erro para jogadores (opcional mas recomendado)**: Exibir uma mensagem de aviso quando `getGamePlayers` falha, sem bloquear o uso do dropdown de Mapa. Alinhado com o requisito 2.2.

**Pseudocode do fix:**
```
useEffect:
  IF NOT statsGameId THEN
    reset all states; return
  END IF

  setLoadingMaps(true); setMapResultId('')

  Promise.allSettled([
    getMapResultsByGame(statsGameId),
    getGamePlayers(statsGameId),
  ]).then(([mapsResult, playersResult]) => {
    IF mapsResult.status = 'fulfilled' THEN
      setMapResults(mapsResult.value)
    ELSE
      setMapResults([])
    END IF

    IF playersResult.status = 'fulfilled' THEN
      setPlayers(playersResult.value)
    ELSE
      setPlayers([])
      // opcional: setPlayersError('Erro ao carregar jogadores.')
    END IF
  }).finally(() => setLoadingMaps(false))
```

## Testing Strategy

### Validation Approach

A estratégia segue duas fases: primeiro, confirmar o bug no código não corrigido com testes exploratórios; depois, verificar que o fix funciona corretamente e não introduz regressões.

### Exploratory Bug Condition Checking

**Goal**: Demonstrar o bug ANTES do fix. Confirmar que o `Promise.all` com catch compartilhado descarta `mapResults` quando `getGamePlayers` falha.

**Test Plan**: Mockar `getMapResultsByGame` para retornar dados e `getGamePlayers` para rejeitar. Verificar que `mapResults` é zerado no código não corrigido.

**Test Cases**:
1. **Maps OK + Players Fail**: `getMapResultsByGame` retorna `[{id:'1', mapNumber:1, rounds:30}]`, `getGamePlayers` rejeita com erro → verificar que `mapResults` é `[]` no código bugado (demonstra o bug)
2. **Maps OK + Players 404**: `getGamePlayers` rejeita com status 404 → mesmo resultado
3. **Maps OK + Players 500**: `getGamePlayers` rejeita com status 500 → mesmo resultado

**Expected Counterexamples**:
- `mapResults` é `[]` mesmo com `getMapResultsByGame` retornando dados — confirma o bug
- Causa: o `.catch` compartilhado do `Promise.all` executa `setMapResults([])` independentemente de qual promise falhou

### Fix Checking

**Goal**: Verificar que para todos os inputs onde a bug condition se aplica, o código corrigido preserva `mapResults`.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := useEffect_fixed(input)
  ASSERT result.mapResults = input.mapsResult.value
  ASSERT result.mapResults.length > 0
END FOR
```

### Preservation Checking

**Goal**: Verificar que para todos os inputs onde a bug condition NÃO se aplica, o código corrigido produz o mesmo resultado que o original.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT useEffect_original(input).mapResults = useEffect_fixed(input).mapResults
  ASSERT useEffect_original(input).players = useEffect_fixed(input).players
END FOR
```

**Testing Approach**: Property-based testing é recomendado para preservation checking porque:
- Gera muitos casos de teste automaticamente (diferentes combinações de gameId, mapResults, players)
- Captura edge cases que testes manuais podem perder
- Fornece garantias fortes de que o comportamento é preservado para todos os inputs não-bugados

**Test Cases**:
1. **Both Success Preservation**: Ambas as chamadas retornam dados → comportamento idêntico ao original
2. **Empty Maps Preservation**: `getMapResultsByGame` retorna `[]` → mensagem "Nenhum mapa registrado" continua aparecendo
3. **No Game Selected Preservation**: `statsGameId` vazio → estados continuam sendo limpos
4. **Both Fail Preservation**: Ambas as chamadas falham → `mapResults = []` e `players = []` (igual ao original)

### Unit Tests

- Testar `useEffect` com `getMapResultsByGame` OK e `getGamePlayers` falhando → `mapResults` deve ser preservado
- Testar `useEffect` com ambas as chamadas com sucesso → comportamento normal
- Testar `useEffect` com `statsGameId` vazio → estados limpos
- Testar `useEffect` com `getMapResultsByGame` falhando → `mapResults = []`

### Property-Based Tests

- Gerar combinações aleatórias de resultados de `getMapResultsByGame` (sucesso com N mapas, falha) e `getGamePlayers` (sucesso, falha) e verificar que `mapResults` reflete apenas o resultado de `getMapResultsByGame`
- Gerar inputs fora da bug condition e verificar que o comportamento é idêntico ao original

### Integration Tests

- Fluxo completo: selecionar jogo → dropdown de Mapa populado mesmo com falha de jogadores
- Fluxo completo: selecionar jogo → ambas as chamadas OK → dropdown de Mapa e Jogador populados
- Fluxo completo: selecionar jogo → trocar de jogo → estados limpos corretamente

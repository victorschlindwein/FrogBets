# Bugfix Design: Fluxo E2E Completo da Plataforma FrogBets

## Overview

Este design aborda múltiplos bugs não identificados que impedem o funcionamento correto do fluxo completo E2E da plataforma FrogBets. O fluxo inclui: registro de 4 usuários → criação de 2 times → adição de 2 jogadores por time → criação de jogo entre os times → abertura de mercados → registro de apostas → verificação de dropdowns de jogadores → cobertura de apostas → início e encerramento do jogo → liquidação de apostas.

A estratégia de correção é baseada em testes exploratórios que executam o fluxo completo no código não corrigido para identificar os pontos de falha, seguido de correções cirúrgicas e verificação de preservação de comportamento existente.

## Glossary

- **Bug_Condition (C)**: A condição que desencadeia os bugs - quando o fluxo E2E completo é executado (registro → times → jogadores → jogo → apostas → liquidação)
- **Property (P)**: O comportamento desejado - o fluxo E2E deve completar com sucesso, com todos os dados corretos e consistentes
- **Preservation**: Comportamentos existentes que devem permanecer inalterados - todos os testes E2E existentes, validações de negócio, invariantes de saldo
- **E2E Flow**: Fluxo end-to-end completo que simula o uso real da plataforma por múltiplos usuários
- **Market Options**: Opções disponíveis em dropdowns de mercados de apostas (times ou jogadores)
- **Settlement**: Processo de liquidação de apostas após o encerramento de um jogo
- **Balance Invariant**: `VirtualBalance + ReservedBalance = constante` (exceto em liquidações)

## Bug Details

### Bug Condition

O bug se manifesta quando o fluxo E2E completo é executado sequencialmente. O sistema pode falhar em múltiplos pontos: ao exibir jogadores nos dropdowns de mercados, ao cobrir apostas, ao liquidar apostas, ou ao manter consistência de saldos durante operações concorrentes.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type E2EFlowExecution
  OUTPUT: boolean
  
  RETURN input.flowSteps INCLUDES [
           "register_4_users",
           "create_2_teams", 
           "add_2_players_per_team",
           "create_game_between_teams",
           "verify_player_dropdowns",
           "create_bets_on_markets",
           "cover_bets_by_other_users",
           "start_game",
           "finish_game_with_results",
           "verify_settlement"
         ]
         AND (
           NOT allPlayersVisibleInDropdowns(input)
           OR NOT betCoverageSuccessful(input)
           OR NOT settlementCorrect(input)
           OR NOT balanceInvariantPreserved(input)
         )
END FUNCTION
```

### Examples

- **Exemplo 1 - Dropdowns de Jogadores**: Após adicionar PlayerA1, PlayerA2 ao TeamA e PlayerB1, PlayerB2 ao TeamB, e criar um jogo entre TeamA vs TeamB, ao verificar os dropdowns de mercados TopKills/MostDeaths/MostUtilityDamage, o sistema pode não exibir todos os 4 jogadores ou exibir jogadores de outros times.

- **Exemplo 2 - Cobertura de Apostas**: User1 cria aposta no mercado MapWinner (Map 1) escolhendo TeamA. User2 tenta cobrir a aposta. O sistema pode falhar ao processar a cobertura, não atualizar o status para Active, ou não remover a aposta do marketplace.

- **Exemplo 3 - Liquidação de Apostas**: Admin marca o resultado do mercado MapWinner (Map 1) como TeamA. O sistema pode não liquidar corretamente as apostas, não creditar 2× o valor para o vencedor, ou não atualizar WinsCount/LossesCount.

- **Exemplo 4 - Race Condition**: User1 e User2 tentam cobrir a mesma aposta simultaneamente. O sistema pode permitir cobertura dupla, causando inconsistência de saldo ou estado de aposta inválido.

- **Exemplo 5 - Invariante de Saldo**: Após múltiplas operações (criar aposta, cobrir aposta, cancelar aposta, liquidar aposta), o invariante `VirtualBalance + ReservedBalance = constante` pode ser violado para algum usuário.

- **Exemplo 6 - Mercados de Jogador**: Ao criar aposta em mercado TopKills, o sistema pode não exibir as opções no formato correto `"<nickname>"` e `"NOT_<nickname>"` ou pode exibir jogadores que não pertencem aos times do jogo.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Todos os testes E2E existentes (auth, bets, games, marketplace, teams, leaderboard, players-ranking, navbar, dashboard) devem continuar passando sem regressões
- Validações de negócio existentes devem permanecer inalteradas (CANNOT_COVER_OWN_BET, DUPLICATE_BET_ON_MARKET, INSUFFICIENT_BALANCE, CANNOT_CANCEL_ACTIVE_BET)
- O invariante de saldo `VirtualBalance + ReservedBalance = constante` deve ser preservado em todas as operações financeiras
- Proteção de rotas (redirecionamento para /login quando não autenticado) deve continuar funcionando
- Controle de acesso admin (403 Forbidden para não-admins em endpoints administrativos) deve continuar funcionando
- Transações Serializable para cobertura de apostas devem continuar prevenindo race conditions

**Scope:**
Todas as operações que NÃO envolvem o fluxo E2E completo devem ser completamente não afetadas por este bugfix. Isso inclui:
- Operações isoladas de criação de aposta
- Operações isoladas de cobertura de aposta
- Operações isoladas de cancelamento de aposta
- Operações isoladas de liquidação de mercado
- Operações de autenticação e autorização
- Operações de gerenciamento de times e jogadores (quando executadas isoladamente)

## Hypothesized Root Cause

Baseado na descrição do bug e análise do código, as causas mais prováveis são:

1. **Endpoint GET /api/games/{id}/players Não Retorna Jogadores Corretos**
   - O endpoint pode estar filtrando jogadores incorretamente
   - Pode não estar considerando apenas os jogadores dos dois times específicos do jogo
   - Pode estar retornando jogadores de times deletados (IsDeleted = true)

2. **Frontend Não Popula Dropdowns Corretamente**
   - O componente GameDetailPage pode não estar chamando o endpoint /api/games/{id}/players
   - Pode não estar mapeando corretamente a resposta para as opções dos dropdowns
   - Pode não estar formatando as opções no padrão `"<nickname>"` e `"NOT_<nickname>"` para mercados de jogador

3. **Cobertura de Aposta Não Remove do Marketplace**
   - O frontend pode não estar atualizando o estado local após cobertura bem-sucedida
   - O endpoint POST /api/bets/{id}/cover pode não estar retornando status correto
   - O marketplace pode não estar re-fetchando a lista após cobertura

4. **Liquidação de Apostas Não Atualiza WinsCount/LossesCount**
   - O SettlementService pode não estar incrementando os contadores de vitórias/derrotas
   - Pode estar atualizando apenas o saldo mas esquecendo os contadores
   - Pode estar usando a lógica errada para determinar vencedor vs perdedor

5. **Race Condition na Cobertura de Apostas**
   - Apesar da transação Serializable, pode haver um bug na lógica de verificação de status
   - Pode haver um timing issue entre a verificação de status e a atualização
   - Pode não estar usando SELECT FOR UPDATE corretamente

6. **Registro de Usuário Não Cria CS2Player Automaticamente**
   - O AuthService.RegisterAsync pode não estar criando o CS2Player quando teamId é fornecido
   - Pode estar falhando silenciosamente na criação do player
   - Pode estar commitando a transação mesmo com falha parcial

## Correctness Properties

Property 1: Bug Condition - Fluxo E2E Completo Funciona Corretamente

_For any_ execução do fluxo E2E completo (registro de 4 usuários → criação de 2 times → adição de 2 jogadores por time → criação de jogo → apostas → cobertura → liquidação), o sistema SHALL completar todas as etapas com sucesso, exibir os 4 jogadores corretos nos dropdowns, processar coberturas corretamente, liquidar apostas com valores e contadores corretos, e manter o invariante de saldo preservado.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**

Property 2: Preservation - Comportamento Existente Não Muda

_For any_ operação que NÃO envolve o fluxo E2E completo (operações isoladas de apostas, autenticação, autorização, gerenciamento de times), o sistema SHALL produzir exatamente o mesmo comportamento do código original, preservando todas as validações de negócio, invariantes de saldo, proteção de rotas e controle de acesso.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8**

## Fix Implementation

### Changes Required

Assumindo que nossa análise de causa raiz está correta:

**File**: `frontend/src/pages/GameDetailPage.tsx`

**Function**: `GameDetailPage` component

**Specific Changes**:
1. **Adicionar Fetch de Jogadores**: Implementar chamada ao endpoint `GET /api/games/{id}/players` ao carregar a página
   - Armazenar lista de jogadores no estado local
   - Usar essa lista para popular os dropdowns de mercados de jogador

2. **Formatar Opções de Mercado de Jogador**: Ao renderizar dropdowns de TopKills/MostDeaths/MostUtilityDamage
   - Para cada jogador, criar duas opções: `"<nickname>"` e `"NOT_<nickname>"`
   - Garantir que apenas jogadores dos dois times do jogo sejam exibidos

3. **Atualizar Estado Após Cobertura**: Após cobertura bem-sucedida de aposta
   - Remover a aposta da lista local de apostas pendentes
   - Ou re-fetch a lista de apostas do usuário

**File**: `src/FrogBets.Api/Services/SettlementService.cs`

**Function**: `SettleMarketAsync`

**Specific Changes**:
4. **Atualizar WinsCount/LossesCount**: Após determinar vencedor e perdedor de cada aposta
   - Incrementar `user.WinsCount` para o vencedor
   - Incrementar `user.LossesCount` para o perdedor
   - Salvar as alterações no banco

5. **Verificar Lógica de Determinação de Vencedor**: Garantir que a lógica está correta
   - Para mercados de time: comparar `creatorOption` e `covererOption` com `winningOption`
   - Para mercados de jogador: considerar o prefixo `NOT_` corretamente

**File**: `src/FrogBets.Api/Controllers/GamesController.cs`

**Function**: `GetGamePlayers`

**Specific Changes**:
6. **Filtrar Apenas Times do Jogo**: Garantir que a query filtra apenas jogadores dos dois times específicos
   - Verificar que `teamIds` contém apenas os IDs dos times TeamA e TeamB do jogo
   - Adicionar filtro `!p.Team.IsDeleted` para excluir times deletados

7. **Ordenar Jogadores Consistentemente**: Ordenar por nome do time e depois por nickname
   - Garantir ordem determinística para facilitar testes

**File**: `frontend/src/pages/MarketplacePage.tsx`

**Function**: `handleCover`

**Specific Changes**:
8. **Remover Aposta Localmente Após Cobertura**: Após cobertura bem-sucedida
   - Filtrar a aposta coberta da lista local `bets.filter(b => b.id !== betId)`
   - Ou re-fetch a lista do marketplace

**File**: `src/FrogBets.Api/Services/AuthService.cs`

**Function**: `RegisterAsync`

**Specific Changes**:
9. **Garantir Criação de CS2Player**: Verificar que o bloco de criação de CS2Player está correto
   - Confirmar que o player é criado quando `teamId.HasValue`
   - Adicionar logging ou tratamento de erro mais robusto
   - Verificar que a transação não é commitada se a criação do player falhar

## Testing Strategy

### Validation Approach

A estratégia de testes segue uma abordagem de três fases: primeiro, executar testes exploratórios no código não corrigido para identificar os pontos exatos de falha e confirmar/refutar as hipóteses de causa raiz; segundo, implementar as correções; terceiro, verificar que o fluxo completo funciona e que nenhum comportamento existente foi quebrado.

### Exploratory Bug Condition Checking

**Goal**: Identificar os pontos exatos de falha no fluxo E2E ANTES de implementar correções. Confirmar ou refutar as hipóteses de causa raiz. Se refutarmos, precisaremos re-hipotizar.

**Test Plan**: Criar um teste Cypress E2E que executa o fluxo completo passo a passo, com asserções em cada etapa. Executar no código UNFIXED e observar onde o teste falha. Adicionar logs detalhados para capturar o estado em cada ponto.

**Test Cases**:
1. **E2E Flow - User Registration**: Registrar 4 usuários (user1, user2, user3, user4) com convites válidos (esperado: sucesso no unfixed code)
2. **E2E Flow - Team Creation**: Admin cria TeamA e TeamB (esperado: sucesso no unfixed code)
3. **E2E Flow - Player Assignment**: Admin adiciona user1, user2 ao TeamA e user3, user4 ao TeamB como CS2Players (pode falhar no unfixed code se RegisterAsync não criar players)
4. **E2E Flow - Game Creation**: Admin cria jogo TeamA vs TeamB com 3 mapas (esperado: sucesso no unfixed code)
5. **E2E Flow - Player Dropdowns**: Verificar que dropdowns de TopKills/MostDeaths/MostUtilityDamage exibem exatamente 4 jogadores (provável falha no unfixed code)
6. **E2E Flow - Bet Creation**: user1 cria aposta em MapWinner (Map 1) escolhendo TeamA (esperado: sucesso no unfixed code)
7. **E2E Flow - Bet Coverage**: user2 cobre a aposta de user1 (pode falhar no unfixed code se marketplace não atualizar)
8. **E2E Flow - Game Start**: Admin inicia o jogo (esperado: sucesso no unfixed code)
9. **E2E Flow - Game Finish**: Admin marca resultado MapWinner (Map 1) = TeamA (esperado: sucesso no unfixed code)
10. **E2E Flow - Settlement Verification**: Verificar que user1 recebeu 2× o valor, user2 perdeu o valor, WinsCount/LossesCount foram atualizados (provável falha no unfixed code)
11. **E2E Flow - Balance Invariant**: Verificar que `VirtualBalance + ReservedBalance` está correto para todos os 4 usuários (pode falhar no unfixed code)

**Expected Counterexamples**:
- Dropdowns de jogadores podem estar vazios ou exibir jogadores errados
- Cobertura de aposta pode não remover a aposta do marketplace
- Liquidação pode não atualizar WinsCount/LossesCount
- Invariante de saldo pode ser violado após liquidação
- Possíveis causas: endpoint /api/games/{id}/players não implementado ou incorreto, frontend não chama o endpoint, SettlementService não atualiza contadores, race condition na cobertura

### Fix Checking

**Goal**: Verificar que para todas as execuções do fluxo E2E completo, o sistema corrigido produz o comportamento esperado.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := executeE2EFlow_fixed(input)
  ASSERT allPlayersVisibleInDropdowns(result)
  ASSERT betCoverageSuccessful(result)
  ASSERT settlementCorrect(result)
  ASSERT balanceInvariantPreserved(result)
END FOR
```

### Preservation Checking

**Goal**: Verificar que para todas as operações que NÃO envolvem o fluxo E2E completo, o sistema corrigido produz o mesmo resultado que o sistema original.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT originalSystem(input) = fixedSystem(input)
END FOR
```

**Testing Approach**: Property-based testing é recomendado para preservation checking porque:
- Gera muitos casos de teste automaticamente através do domínio de entrada
- Captura edge cases que testes unitários manuais podem perder
- Fornece garantias fortes de que o comportamento está inalterado para todas as entradas não-buggy

**Test Plan**: Executar todos os testes E2E existentes no código UNFIXED para observar o comportamento atual, depois executar no código FIXED e verificar que o comportamento é idêntico.

**Test Cases**:
1. **Preservation - Auth Flow**: Executar frontend/cypress/e2e/auth.cy.ts e verificar que todos os testes passam (login, registro, logout, proteção de rotas)
2. **Preservation - Bets Flow**: Executar frontend/cypress/e2e/bets.cy.ts e verificar que todos os testes passam (listar apostas, cancelar aposta, exibir detalhes)
3. **Preservation - Games Flow**: Executar frontend/cypress/e2e/games.cy.ts e verificar que todos os testes passam (listar jogos, criar aposta, exibir erro de saldo insuficiente)
4. **Preservation - Marketplace Flow**: Executar frontend/cypress/e2e/marketplace.cy.ts e verificar que todos os testes passam (listar apostas, cobrir aposta, exibir erro)
5. **Preservation - Teams Flow**: Executar frontend/cypress/e2e/teams.cy.ts e verificar que todos os testes passam (listar times, criar time)
6. **Preservation - Business Rules**: Verificar que CANNOT_COVER_OWN_BET, DUPLICATE_BET_ON_MARKET, INSUFFICIENT_BALANCE, CANNOT_CANCEL_ACTIVE_BET continuam funcionando
7. **Preservation - Balance Invariant**: Executar testes de propriedade que verificam o invariante de saldo em operações isoladas (criar aposta, cobrir aposta, cancelar aposta)
8. **Preservation - Access Control**: Verificar que não-admins recebem 403 em endpoints administrativos, não-autenticados são redirecionados para /login

### Unit Tests

- Testar endpoint GET /api/games/{id}/players isoladamente com diferentes configurações de times e jogadores
- Testar SettlementService.SettleMarketAsync com diferentes cenários de vencedor/perdedor
- Testar AuthService.RegisterAsync com e sem teamId, verificando criação de CS2Player
- Testar lógica de formatação de opções de mercado de jogador no frontend
- Testar atualização de estado local após cobertura de aposta no frontend

### Property-Based Tests

- Gerar fluxos E2E aleatórios com diferentes números de usuários, times, jogadores, jogos, apostas
- Verificar que o invariante de saldo é preservado após qualquer sequência de operações
- Gerar apostas aleatórias e verificar que a cobertura sempre resulta em estado consistente
- Gerar resultados de mercado aleatórios e verificar que a liquidação sempre atualiza saldos e contadores corretamente
- Testar race conditions simulando coberturas concorrentes da mesma aposta

### Integration Tests

- Testar fluxo completo E2E via API (sem frontend) usando WebApplicationFactory
- Testar fluxo de registro → criação de player → criação de jogo → listagem de jogadores
- Testar fluxo de criação de aposta → cobertura → liquidação com verificação de saldos e contadores
- Testar fluxo de múltiplos usuários criando e cobrindo apostas simultaneamente
- Testar fluxo de criação de jogo com N mapas → verificar que N×4+1 mercados são criados → verificar que jogadores corretos aparecem em cada mercado

# Plano de Implementação: FrogBets

## Visão Geral

Implementação incremental da plataforma FrogBets usando .NET 8 Web API (backend), React (frontend) e PostgreSQL (banco de dados). Cada tarefa constrói sobre a anterior, garantindo que nenhum código fique órfão.

## Tarefas

- [x] 1. Estrutura do projeto e modelos de dados
  - Criar solução .NET 8 com projetos: `FrogBets.Api`, `FrogBets.Domain`, `FrogBets.Infrastructure`, `FrogBets.Tests`
  - Criar projeto React com Vite + TypeScript em `frontend/`
  - Definir entidades C# (`User`, `Game`, `Market`, `Bet`, `GameResult`, `Notification`) conforme modelos do design
  - Definir enums (`GameStatus`, `MarketType`, `MarketStatus`, `BetStatus`, `BetResult`)
  - Configurar DbContext com Entity Framework Core + Npgsql para PostgreSQL
  - Criar migrations iniciais com todas as tabelas e índices necessários
  - _Requirements: 1.1, 2.1, 3.1, 4.2_

- [x] 2. Autenticação e gerenciamento de usuários
  - [x] 2.1 Implementar `AuthService` com hash de senha (BCrypt) e geração/validação de JWT
    - Configurar JWT Bearer no pipeline do .NET 8
    - Implementar login retornando token com expiração configurável
    - Implementar logout invalidando token (blocklist em memória ou campo `TokenVersion`)
    - Mensagem de erro genérica `"Credenciais inválidas"` sem revelar qual campo
    - _Requirements: 1.1, 1.2, 1.3, 1.5_

  - [ ]* 2.2 Escrever property test para Property 1 (credenciais inválidas não revelam campo)
    - **Property 1: Credenciais inválidas não revelam qual campo está errado**
    - **Validates: Requirements 1.3**

  - [x] 2.3 Implementar `UsersController` com cadastro e endpoints de perfil/saldo
    - `POST /api/auth/login`, `POST /api/auth/logout`
    - `GET /api/users/me`, `GET /api/users/me/balance`
    - Atribuir saldo inicial de 1000 unidades ao cadastrar novo usuário
    - _Requirements: 1.2, 2.1, 2.2, 2.9_

  - [ ]* 2.4 Escrever testes de exemplo para fluxo de login com credenciais válidas e inválidas
    - Testar redirecionamento ao expirar sessão (1.4)
    - _Requirements: 1.2, 1.3, 1.4_

- [x] 3. Checkpoint — Garantir que todos os testes passam
  - Garantir que todos os testes passam; perguntar ao usuário se houver dúvidas.

- [x] 4. Gerenciamento de saldo virtual (`BalanceService`)
  - [x] 4.1 Implementar `BalanceService` com operações de reserva, devolução e crédito
    - `ReserveBalance(userId, amount)` — diminui `VirtualBalance`, aumenta `ReservedBalance`
    - `ReleaseBalance(userId, amount)` — reverte reserva
    - `CreditWinner(winnerId, amount)` — credita `2 * amount` ao vencedor, libera reserva
    - Todas as operações dentro de transações com isolamento `Serializable`
    - Rejeitar operação se `VirtualBalance < amount` com erro `INSUFFICIENT_BALANCE`
    - _Requirements: 2.3, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [ ]* 4.2 Escrever property test para Property 2 (invariante de saldo ao reservar)
    - **Property 2: VirtualBalance + ReservedBalance permanece inalterado após reserva**
    - **Validates: Requirements 2.3, 2.5**

  - [ ]* 4.3 Escrever property test para Property 3 (rejeição por saldo insuficiente)
    - **Property 3: Qualquer operação com valor V > saldo disponível é rejeitada sem alterar saldo**
    - **Validates: Requirements 2.7, 2.8**

  - [ ]* 4.4 Escrever property test para Property 4 (crédito correto ao vencedor)
    - **Property 4: Vencedor recebe 2*A e ReservedBalance diminui em A**
    - **Validates: Requirements 2.6, 7.2**

  - [ ]* 4.5 Escrever property test para Property 7 (round-trip de cancelamento restaura saldo)
    - **Property 7: Criar aposta pendente e cancelar restaura saldo exatamente**
    - **Validates: Requirements 2.4, 6.2**

- [x] 5. Gerenciamento de jogos e mercados (`GameService`)
  - [x] 5.1 Implementar `GameService` e `GamesController`
    - `POST /api/games` (admin) — cadastrar jogo com times, data e número de mapas
    - `GET /api/games` — listagem pública de jogos
    - `PATCH /api/games/{id}/start` (admin) — iniciar jogo, fechar criação de apostas
    - `POST /api/games/{id}/results` (admin) — registrar resultado de mercado/mapa
    - Rejeitar registro de resultado em jogo com status `Finished` com erro `409`
    - Criar mercados automaticamente ao cadastrar jogo (um por tipo de mercado por mapa/série)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [ ]* 5.2 Escrever property test para Property 5 (apostas bloqueadas para jogos iniciados)
    - **Property 5: Qualquer tentativa de criar aposta em jogo InProgress ou Finished é rejeitada**
    - **Validates: Requirements 3.3, 4.4**

  - [ ]* 5.3 Escrever property test para Property 16 (resultado de jogo liquidado é rejeitado)
    - **Property 16: Registrar resultado em jogo Finished retorna 409**
    - **Validates: Requirements 3.6**

  - [ ]* 5.4 Escrever testes de exemplo para criação de jogo pelo admin e listagem pública
    - _Requirements: 3.1, 3.2_

- [x] 6. Criação e cobertura de apostas (`BetService`)
  - [x] 6.1 Implementar `BetService.CreateBet` e endpoint `POST /api/bets`
    - Validar jogo disponível (status `Scheduled`), mercado aberto, saldo suficiente
    - Rejeitar aposta duplicada no mesmo mercado com erro `DUPLICATE_BET_ON_MARKET`
    - Reservar saldo via `BalanceService.ReserveBalance`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [ ]* 6.2 Escrever property test para Property 8 (unicidade de aposta por usuário e mercado)
    - **Property 8: Segunda aposta no mesmo mercado pelo mesmo usuário é rejeitada**
    - **Validates: Requirements 4.6**

  - [x] 6.3 Implementar `BetService.CoverBet` e endpoint `POST /api/bets/{id}/cover`
    - Usar `SELECT FOR UPDATE` para garantir exclusividade de cobertura
    - Rejeitar auto-cobertura com `CANNOT_COVER_OWN_BET`
    - Rejeitar cobertura de aposta não-pendente com `BET_NOT_AVAILABLE`
    - Atribuir `CovererOption` como opção oposta ao `CreatorOption`
    - Reservar saldo do cobrador via `BalanceService.ReserveBalance`
    - Criar notificação para o criador da aposta
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [ ]* 6.4 Escrever property test para Property 9 (cobertura registra contraparte e opção oposta)
    - **Property 9: Após cobertura, CoveredById e CovererOption estão corretos e status é Active**
    - **Validates: Requirements 5.2, 5.5**

  - [ ]* 6.5 Escrever property test para Property 10 (criador não pode cobrir a própria aposta)
    - **Property 10: Tentativa de auto-cobertura é sempre rejeitada**
    - **Validates: Requirements 5.3**

  - [ ]* 6.6 Escrever property test para Property 11 (aposta já coberta não pode ser coberta novamente)
    - **Property 11: Cobertura de aposta Active é sempre rejeitada**
    - **Validates: Requirements 5.4**

  - [ ]* 6.7 Escrever property test para Property 12 (notificação ao criador quando aposta é coberta)
    - **Property 12: Após cobertura, existe ao menos uma notificação não lida para o criador**
    - **Validates: Requirements 5.6**

- [x] 7. Cancelamento de apostas
  - [x] 7.1 Implementar `BetService.CancelBet` e endpoint `DELETE /api/bets/{id}`
    - Rejeitar cancelamento de aposta não-pendente com `CANNOT_CANCEL_ACTIVE_BET`
    - Rejeitar cancelamento por usuário não-criador com `NOT_BET_OWNER`
    - Devolver saldo via `BalanceService.ReleaseBalance`
    - Remover aposta da listagem de pendentes
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [ ]* 7.2 Escrever property test para Property 13 (apenas o criador pode cancelar)
    - **Property 13: Cancelamento por usuário diferente do criador é sempre rejeitado**
    - **Validates: Requirements 6.4**

  - [ ]* 7.3 Escrever property test para Property 14 (aposta ativa não pode ser cancelada)
    - **Property 14: Cancelamento de aposta Active é sempre rejeitado**
    - **Validates: Requirements 6.3**

- [x] 8. Checkpoint — Garantir que todos os testes passam
  - Garantir que todos os testes passam; perguntar ao usuário se houver dúvidas.

- [x] 9. Liquidação de apostas (`SettlementService`)
  - [x] 9.1 Implementar `SettlementService.SettleMarket`
    - Ao registrar resultado de mercado, buscar todas as apostas `Active` daquele mercado
    - Para cada aposta: comparar `CreatorOption` com `WinningOption` e determinar vencedor
    - Creditar vencedor via `BalanceService.CreditWinner`
    - Marcar aposta como `Settled` com `BetResult` correto
    - Se mercado for `Voided` (empate em mercado sem empate): devolver saldo a ambos via `ReleaseBalance` e marcar como `Voided`
    - Atualizar status do jogo para `Finished` quando todos os mercados estiverem liquidados
    - _Requirements: 7.1, 7.2, 7.3, 7.5, 3.5_

  - [ ]* 9.2 Escrever property test para Property 6 (liquidação completa de todas as apostas ativas)
    - **Property 6: Após registrar resultado, todas as N apostas ativas do mercado são Settled ou Voided**
    - **Validates: Requirements 3.5, 7.1**

  - [ ]* 9.3 Escrever property test para Property 15 (aposta anulada devolve saldo a ambos)
    - **Property 15: Aposta Voided restaura VirtualBalance de criador e cobrador**
    - **Validates: Requirements 7.5**

  - [ ]* 9.4 Escrever testes de integração para fluxo end-to-end
    - Criar aposta → cobrir → registrar resultado → verificar saldos
    - Concorrência: dois usuários cobrindo a mesma aposta simultaneamente (apenas um deve ter sucesso)
    - _Requirements: 2.6, 5.2, 7.2_

- [x] 10. Listagem de apostas, marketplace e histórico
  - [x] 10.1 Implementar `BetsController` com endpoints de listagem
    - `GET /api/bets` — apostas do usuário autenticado (Pending, Active, Settled)
    - `GET /api/marketplace` — apostas Pending de todos os usuários (exceto as do próprio usuário)
    - Resposta de cada aposta deve conter: mercado, opção do criador, valor, status, contraparte (quando existir) e opção da contraparte
    - Histórico de apostas Settled ordenado por `SettledAt` decrescente
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [ ]* 10.2 Escrever property test para Property 17 (resposta da API contém campos obrigatórios)
    - **Property 17: Toda aposta retornada pela API contém mercado, opção, valor, status e contraparte quando existir**
    - **Validates: Requirements 8.2**

  - [ ]* 10.3 Escrever property test para Property 18 (histórico ordenado por data decrescente)
    - **Property 18: Apostas liquidadas retornadas em ordem decrescente de SettledAt**
    - **Validates: Requirements 8.3**

- [x] 11. Leaderboard
  - [x] 11.1 Implementar `LeaderboardController` com endpoint `GET /api/leaderboard`
    - Retornar todos os usuários ordenados por `VirtualBalance` decrescente
    - Cada entrada deve conter: `username`, `virtualBalance`, `winsCount`, `lossesCount`
    - Atualizar contadores de vitórias/derrotas na liquidação de apostas
    - _Requirements: 9.1, 9.2, 9.3_

  - [ ]* 11.2 Escrever property test para Property 19 (leaderboard ordenado com campos obrigatórios)
    - **Property 19: Leaderboard retorna usuários em ordem decrescente de VirtualBalance com todos os campos**
    - **Validates: Requirements 9.1, 9.3**

- [x] 12. Checkpoint — Garantir que todos os testes passam
  - Garantir que todos os testes passam; perguntar ao usuário se houver dúvidas.

- [x] 13. Frontend — Autenticação e estrutura base
  - [x] 13.1 Configurar projeto React com Vite + TypeScript, React Router e Axios
    - Configurar cliente Axios com interceptor para injetar header `Authorization: Bearer <token>`
    - Armazenar token JWT em memória (não em localStorage)
    - Implementar rota protegida que redireciona para `/login` quando token ausente ou expirado
    - _Requirements: 1.1, 1.4_

  - [x] 13.2 Implementar `LoginPage` (`/login`)
    - Formulário de login com username e password
    - Exibir mensagem de erro genérica em caso de credenciais inválidas
    - Redirecionar para `/` após login bem-sucedido
    - _Requirements: 1.2, 1.3_

  - [ ]* 13.3 Escrever testes de componente para `LoginPage` com React Testing Library + Vitest
    - Mock de API com MSW
    - _Requirements: 1.2, 1.3_

- [x] 14. Frontend — Dashboard, saldo e apostas do usuário
  - [x] 14.1 Implementar `DashboardPage` (`/`) com exibição de saldo disponível e reservado
    - _Requirements: 2.9_

  - [x] 14.2 Implementar `BetsPage` (`/bets`) com listagem de apostas Pending, Active e Settled do usuário
    - Exibir para cada aposta: mercado, opção, valor, contraparte (quando existir) e status
    - Botão de cancelar para apostas Pending
    - _Requirements: 8.1, 8.2, 6.1_

  - [ ]* 14.3 Escrever testes de componente para listagem de apostas e fluxo de cancelamento
    - _Requirements: 8.1, 8.2, 6.1_

- [x] 15. Frontend — Marketplace e cobertura de apostas
  - [x] 15.1 Implementar `MarketplacePage` (`/marketplace`) com listagem de apostas Pending de outros usuários
    - Botão "Cobrir" para cada aposta disponível
    - Exibir confirmação antes de cobrir
    - _Requirements: 5.1, 8.4_

  - [ ]* 15.2 Escrever testes de componente para fluxo de cobertura de aposta
    - _Requirements: 5.1, 5.2_

- [x] 16. Frontend — Jogos e criação de apostas
  - [x] 16.1 Implementar `GamesPage` (`/games`) e `GameDetailPage` (`/games/:id`)
    - Listar jogos disponíveis com times e data
    - Exibir mercados disponíveis para apostas em jogos com status `Scheduled`
    - Formulário de criação de aposta (mercado, opção, valor)
    - _Requirements: 3.2, 4.1, 4.2, 4.3_

  - [ ]* 16.2 Escrever testes de componente para formulário de criação de aposta
    - _Requirements: 4.1, 4.2_

- [x] 17. Frontend — Leaderboard e página admin
  - [x] 17.1 Implementar `LeaderboardPage` (`/leaderboard`)
    - Tabela com username, saldo virtual, vitórias e derrotas
    - _Requirements: 9.1, 9.3_

  - [x] 17.2 Implementar `AdminPage` (`/admin`) acessível apenas para usuários com `IsAdmin = true`
    - Formulário para cadastrar jogo
    - Botão para iniciar jogo
    - Formulário para registrar resultado de mercado/mapa
    - _Requirements: 3.1, 3.3, 3.4_

  - [ ]* 17.3 Escrever testes de componente para leaderboard e exibição de saldo
    - _Requirements: 9.1, 9.3, 2.9_

- [x] 18. Checkpoint final — Garantir que todos os testes passam
  - Garantir que todos os testes passam; perguntar ao usuário se houver dúvidas.

## Notas

- Tarefas marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- Cada tarefa referencia os requisitos específicos para rastreabilidade
- Os checkpoints garantem validação incremental a cada fase
- Property tests usam **FsCheck** integrado com **xUnit** (mínimo 100 iterações por propriedade)
- Testes de componente React usam **React Testing Library** + **Vitest** com mocks via **MSW**
- Transações com isolamento `Serializable` e `SELECT FOR UPDATE` são obrigatórias nas operações de saldo e cobertura

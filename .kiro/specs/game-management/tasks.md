# Plano de Implementação: game-management

## Visão Geral

Implementar edição e exclusão de jogos agendados, restringir rotas públicas a usuários autenticados, e atualizar o frontend e a documentação para refletir as mudanças.

## Tarefas

- [x] 1. Estender IGameService com os novos métodos e records
  - Adicionar `UpdateGameRequest` record em `IGameService.cs`
  - Adicionar `UpdateGameAsync(Guid gameId, UpdateGameRequest request)` à interface
  - Adicionar `DeleteGameAsync(Guid gameId)` à interface
  - _Requirements: 1.1, 1.7, 2.1_

- [x] 2. Implementar UpdateGameAsync no GameService
  - [x] 2.1 Implementar lógica de atualização de campos e regeneração de mercados
    - Buscar jogo por ID; lançar `KeyNotFoundException` se não encontrado
    - Verificar `Status == Scheduled`; lançar `InvalidOperationException("GAME_CANNOT_BE_EDITED")` caso contrário
    - Atualizar apenas os campos não-nulos do `UpdateGameRequest` (PATCH semântico)
    - Quando `NumberOfMaps` mudar: carregar mercados com apostas, remover mercados sem apostas dos mapas removidos, criar 4 mercados para cada mapa adicionado, nunca remover/duplicar `SeriesWinner`
    - Salvar e retornar `GameDto` atualizado
    - _Requirements: 1.1, 1.2, 1.3, 1.7_

  - [ ]* 2.2 Escrever property test — Property 1: Edição preserva e atualiza campos corretamente
    - **Property 1: Edição preserva e atualiza campos corretamente**
    - **Validates: Requirements 1.1**

  - [ ]* 2.3 Escrever property test — Property 2: Edição é rejeitada para jogos não-agendados
    - **Property 2: Edição é rejeitada para jogos não-agendados**
    - **Validates: Requirements 1.3**

  - [ ]* 2.4 Escrever property test — Property 3: Regeneração de mercados preserva invariante de contagem
    - **Property 3: Regeneração de mercados preserva invariante de contagem (newN × 4 + 1)**
    - **Validates: Requirements 1.7**

  - [ ]* 2.5 Escrever testes unitários para UpdateGameAsync
    - Jogo inexistente → `KeyNotFoundException`
    - Jogo `InProgress` → `InvalidOperationException("GAME_CANNOT_BE_EDITED")`
    - Jogo `Finished` → `InvalidOperationException("GAME_CANNOT_BE_EDITED")`
    - Atualização parcial preserva campos não fornecidos
    - _Requirements: 1.1, 1.2, 1.3_

- [x] 3. Implementar DeleteGameAsync no GameService
  - [x] 3.1 Implementar lógica de exclusão com devolução de saldo
    - Buscar jogo com mercados e apostas; lançar `KeyNotFoundException` se não encontrado
    - Verificar `Status != InProgress && Status != Finished`; lançar `InvalidOperationException("GAME_CANNOT_BE_DELETED")` caso contrário
    - Para cada aposta `Pending`: `ReleaseBalanceAsync(bet.CreatorId, bet.Amount)` + `bet.Status = Cancelled`
    - Para cada aposta `Active`: `ReleaseBalanceAsync(creatorId, amount)` + `ReleaseBalanceAsync(coveredById, amount)` + `bet.Status = Voided`
    - Remover jogo do contexto (cascade remove mercados e apostas) e salvar em transação única
    - _Requirements: 2.1, 2.2, 2.3_

  - [ ]* 3.2 Escrever property test — Property 4: Exclusão restaura saldo de todos os usuários envolvidos
    - **Property 4: Exclusão restaura saldo de todos os usuários envolvidos**
    - **Validates: Requirements 2.1**

  - [ ]* 3.3 Escrever property test — Property 5: Exclusão é rejeitada para jogos em andamento ou finalizados
    - **Property 5: Exclusão é rejeitada para jogos em andamento ou finalizados**
    - **Validates: Requirements 2.3**

  - [ ]* 3.4 Escrever testes unitários para DeleteGameAsync
    - Jogo inexistente → `KeyNotFoundException`
    - Jogo `InProgress` → `InvalidOperationException("GAME_CANNOT_BE_DELETED")`
    - Jogo `Finished` → `InvalidOperationException("GAME_CANNOT_BE_DELETED")`
    - Apostas `Pending` canceladas e saldo devolvido ao criador
    - Apostas `Active` anuladas e saldo devolvido a criador e cobrador
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 4. Checkpoint — Garantir que todos os testes passam
  - Garantir que todos os testes passam, perguntar ao usuário se houver dúvidas.

- [x] 5. Adicionar endpoints PATCH e DELETE ao GamesController
  - [x] 5.1 Implementar `PATCH /api/games/{id}` com `PatchGameBody`
    - Adicionar record `PatchGameBody` com validações `[StringLength]` e `[Range]`
    - Implementar action `UpdateGame`: verificar admin via `IsAdminFromDb()`, chamar `_gameService.UpdateGameAsync`, tratar `KeyNotFoundException` (404) e `InvalidOperationException("GAME_CANNOT_BE_EDITED")` (409)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 5.2 Implementar `DELETE /api/games/{id}`
    - Implementar action `DeleteGame`: verificar admin via `IsAdminFromDb()`, chamar `_gameService.DeleteGameAsync`, tratar `KeyNotFoundException` (404) e `InvalidOperationException("GAME_CANNOT_BE_DELETED")` (409), retornar `204 No Content` em sucesso
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [ ]* 5.3 Escrever testes de integração para os novos endpoints
    - `PATCH /api/games/{id}` sem JWT → 401
    - `PATCH /api/games/{id}` com usuário não-admin → 403
    - `PATCH /api/games/{id}` com jogo `InProgress` → 409
    - `DELETE /api/games/{id}` sem JWT → 401
    - `DELETE /api/games/{id}` com usuário não-admin → 403
    - `DELETE /api/games/{id}` com jogo `InProgress` → 409
    - Adicionar casos em `tests/FrogBets.Tests/Integration/GamesIntegrationTests.cs`
    - _Requirements: 1.2, 1.3, 1.4, 1.5, 2.2, 2.3, 2.4, 2.5_

- [x] 6. Remover [AllowAnonymous] das rotas públicas existentes
  - [x] 6.1 Atualizar GamesController — remover `[AllowAnonymous]` de `GET /api/games` e `GET /api/games/{id}`
    - _Requirements: 3.1, 3.2, 3.6_

  - [x] 6.2 Atualizar TeamsController — remover `[AllowAnonymous]` de `GET /api/teams`
    - _Requirements: 3.3, 3.6_

  - [x] 6.3 Atualizar PlayersController — remover `[AllowAnonymous]` de `GET /api/players/ranking` e `GET /api/players/{id}/stats`
    - _Requirements: 3.4, 3.5, 3.6_

  - [ ]* 6.4 Escrever testes de integração para rotas agora privadas
    - `GET /api/games` sem JWT → 401
    - `GET /api/games/{id}` sem JWT → 401
    - `GET /api/teams` sem JWT → 401
    - `GET /api/players/ranking` sem JWT → 401
    - `GET /api/players/{id}/stats` sem JWT → 401
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 7. Atualizar o frontend para usar apiClient nos endpoints afetados
  - [x] 7.1 Atualizar `frontend/src/api/players.ts` — substituir `publicClient` por `apiClient` em `getPlayersRanking` e `getPlayerStats`
    - _Requirements: 3.7_

  - [x] 7.2 Verificar `GamesPage.tsx` e `GameDetailPage.tsx` — confirmar que já usam `apiClient` (sem `publicClient`)
    - _Requirements: 3.7_

  - [x] 7.3 Verificar chamadas a `/teams` no frontend — substituir qualquer uso de `publicClient` por `apiClient`
    - _Requirements: 3.7_

- [x] 8. Adicionar EditGameSection e botão Excluir no AdminPage
  - [x] 8.1 Implementar componente `EditGameSection` no `AdminPage.tsx`
    - Dropdown com jogos `Scheduled` (reutilizar lista de jogos já carregada)
    - Campos pré-preenchidos com valores atuais (TeamA, TeamB, ScheduledAt, NumberOfMaps)
    - Botão "Salvar alterações" → `PATCH /api/games/{id}` via `apiClient`
    - Feedback de sucesso/erro inline; recarregar lista de jogos após sucesso
    - Adicionar entrada `{ id: 'sec-edit-game', label: '✏️ Editar Jogo' }` no índice de navegação
    - _Requirements: 1.1_

  - [x] 8.2 Adicionar botão "Excluir" na listagem de jogos agendados do AdminPage
    - Confirmação via `confirm()` antes de enviar
    - `DELETE /api/games/{id}` via `apiClient` → recarregar lista de jogos
    - Feedback de erro inline em caso de falha
    - _Requirements: 2.1_

- [x] 9. Checkpoint — Garantir que todos os testes passam
  - Garantir que todos os testes passam, perguntar ao usuário se houver dúvidas.

- [x] 10. Atualizar documentação
  - [x] 10.1 Atualizar `README.md` — adicionar edição e exclusão de jogos na seção "Funcionalidades"
    - _Requirements: 4.1_

  - [x] 10.2 Atualizar `docs/TECHNICAL.md` — adicionar `PATCH /api/games/{id}` e `DELETE /api/games/{id}` na tabela de endpoints de Jogos
    - _Requirements: 4.2_

  - [x] 10.3 Atualizar `docs/TECHNICAL.md` — alterar coluna "Auth" de `GET /api/games`, `GET /api/games/{id}`, `GET /api/teams`, `GET /api/players/ranking` e `GET /api/players/{id}/stats` de `Público` para `JWT`
    - _Requirements: 4.3_

  - [x] 10.4 Atualizar `docs/TECHNICAL.md` — documentar códigos de erro `GAME_CANNOT_BE_EDITED` e `GAME_CANNOT_BE_DELETED` na tabela de erros de Jogos
    - _Requirements: 4.4_

## Notas

- Tarefas marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- Os property tests devem ser criados em `tests/FrogBets.Tests/GameManagementPropertyTests.cs` usando FsCheck (`[Property(MaxTest = 100)]`)
- Os testes unitários de serviço devem ser criados em `tests/FrogBets.Tests/GameManagementTests.cs`
- Os testes de integração devem ser adicionados em `tests/FrogBets.Tests/Integration/GamesIntegrationTests.cs`
- Nenhuma migração de banco de dados é necessária para esta feature

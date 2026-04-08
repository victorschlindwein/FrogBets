# Plano de Implementação: user-player-unification

## Overview

Unificar `User` e `CS2Player` em um fluxo único: ao registrar via convite, um `CS2Player` é criado automaticamente na mesma transação. A criação manual de jogadores pelo admin é removida. A seção "Jogadores" do painel admin passa a ser somente leitura com `Username` do `User` vinculado.

## Tasks

- [x] 1. Adicionar `UserId` à entidade `CS2Player` e configurar EF Core
  - Adicionar `public Guid? UserId { get; set; }` e `public User? User { get; set; }` em `src/FrogBets.Domain/Entities/CS2Player.cs`
  - Configurar a relação 1:1 em `OnModelCreating` em `FrogBetsDbContext`: `HasOne(p => p.User).WithOne().HasForeignKey<CS2Player>(p => p.UserId).OnDelete(DeleteBehavior.SetNull).IsRequired(false)`
  - Adicionar índice único com filtro: `HasIndex(p => p.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL")`
  - Criar migração EF Core: `dotnet ef migrations add AddUserIdToCS2Player --project src/FrogBets.Infrastructure --startup-project src/FrogBets.Api`
  - _Requirements: 2.1, 5.1, 5.4_

- [x] 2. Atualizar `IPlayerService` e `PlayerService`
  - [x] 2.1 Atualizar `CS2PlayerDto` em `IPlayerService.cs` para incluir `string? Username` como último campo
    - Remover `CreatePlayerRequest` e `CreatePlayerAsync` da interface pública
    - _Requirements: 2.4, 3.2, 4.3_

  - [x] 2.2 Atualizar `PlayerService.GetPlayersAsync()` para incluir `Username` no DTO
    - Fazer join com `User` via `UserId` ao buscar jogadores; retornar `Username = player.User?.Username` (null para legados)
    - _Requirements: 2.4, 5.2, 5.3_

  - [x]* 2.3 Escrever testes unitários para `PlayerService.GetPlayersAsync()`
    - Testar: jogador com `UserId` vinculado → `Username` presente no DTO
    - Testar: jogador legado sem `UserId` → `Username = null`, sem exceção
    - Testar: múltiplos jogadores mistos (legados + vinculados) → todos retornados
    - _Requirements: 2.4, 5.2, 5.3_

  - [x]* 2.4 Escrever property test para Property 2 — CS2Player vinculado expõe Username no DTO
    - `// Feature: user-player-unification, Property 2: CS2Player vinculado expõe Username no DTO`
    - Para qualquer conjunto de CS2Players com UserId não-nulo, `GetPlayersAsync()` retorna DTO com `Username == user.Username`
    - _Requirements: 2.4, 4.3_

  - [x]* 2.5 Escrever property test para Property 4 — Jogadores legados aparecem nas listagens
    - `// Feature: user-player-unification, Property 4: jogadores legados aparecem nas listagens`
    - Para qualquer conjunto incluindo registros com `UserId = null`, `GetPlayersAsync()` e `GetRankingAsync()` retornam todos sem exceção
    - _Requirements: 5.2, 5.3_

- [x] 3. Atualizar `AuthService.RegisterAsync` para criar `CS2Player` na mesma transação
  - [x] 3.1 Envolver a lógica de registro em transação explícita (`BeginTransactionAsync`)
    - Sequência: INSERT User → INSERT CS2Player (se `teamId != null`) → UPDATE Invite → COMMIT
    - Se qualquer passo falhar, reverter transação e lançar `InvalidOperationException("REGISTRATION_FAILED")`
    - `CS2Player` criado com: `UserId = user.Id`, `Nickname = username`, `TeamId = teamId.Value`, `PlayerScore = 0.0`, `MatchesCount = 0`
    - Se `teamId` for nulo, criar apenas o `User` (sem `CS2Player`) — compatibilidade com usuários sem time
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x]* 3.2 Escrever testes unitários para `AuthService.RegisterAsync`
    - Testar: registro com `teamId` válido → `CS2Player` criado com `Nickname == username`, `UserId == user.Id`, `PlayerScore == 0.0`, `MatchesCount == 0`
    - Testar: registro sem `teamId` → `User` criado, nenhum `CS2Player` criado
    - Testar: falha simulada na criação do `CS2Player` → `User` não persiste (rollback), erro `REGISTRATION_FAILED`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x]* 3.3 Escrever property test para Property 1 — Registro cria CS2Player com dados corretos
    - `// Feature: user-player-unification, Property 1: registro cria CS2Player com dados corretos`
    - Para qualquer username válido e teamId válido, após `RegisterAsync` bem-sucedido, existe exatamente um `CS2Player` com `Nickname == username`, `UserId == user.Id`, `PlayerScore == 0.0`, `MatchesCount == 0`
    - _Requirements: 1.1, 1.2, 1.4, 1.5_

- [x] 4. Checkpoint — Garantir que todos os testes passam
  - Garantir que todos os testes passam, perguntar ao usuário se houver dúvidas.

- [x] 5. Atualizar `PlayersController` — remover `POST /api/players` e expor `Username`
  - Remover o método `CreatePlayer` (ou substituir por `return NotFound()`) em `PlayersController.cs`
  - Remover o record `CreatePlayerBody` do arquivo
  - O método `GetPlayers` já retorna `CS2PlayerDto` — nenhuma alteração necessária após atualização do DTO
  - _Requirements: 3.1, 3.2, 4.2, 4.3_

- [x] 6. Atualizar `AuthController` para tratar `REGISTRATION_FAILED`
  - Adicionar catch para `InvalidOperationException` com mensagem `"REGISTRATION_FAILED"` em `Register`
  - Retornar `HTTP 500` com `{ error: { code: "REGISTRATION_FAILED", message: "Erro interno ao registrar usuário." } }`
  - _Requirements: 1.3_

- [x] 7. Atualizar frontend — `frontend/src/api/players.ts`
  - Adicionar campo `username?: string` ao tipo `CS2Player`
  - Remover a função `createPlayer` e o tipo de parâmetro associado
  - _Requirements: 3.3, 4.1_

- [x] 8. Atualizar frontend — `PlayersSection` em `AdminPage.tsx`
  - Remover o formulário de criação de jogadores (estado `nickname`, `realName`, `teamId`, `photoUrl`, `handleSubmit`, `success`, `error` relacionados à criação)
  - Exibir apenas a tabela de jogadores existentes
  - Adicionar coluna "Username" na tabela, exibindo `player.username ?? '—'`
  - Na seção de stats (`StatsSection` ou equivalente), exibir `player.username ?? player.nickname` como identificador principal no seletor de jogadores
  - _Requirements: 3.3, 3.4, 4.1_

- [x] 9. Checkpoint final — Garantir que todos os testes passam
  - Garantir que todos os testes passam (`dotnet test` + `npm run test -- --run`), perguntar ao usuário se houver dúvidas.

## Notes

- Tasks marcadas com `*` são opcionais e podem ser puladas para MVP mais rápido
- A constraint `UNIQUE` em `UserId` usa filtro `WHERE "UserId" IS NOT NULL` — múltiplos legados com `NULL` são permitidos
- O InMemory database ignora transações — usar `.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))` nos testes
- `GetRankingAsync()` permanece inalterado — continua usando `Nickname` do `CS2Player` (Property 3 validada implicitamente)

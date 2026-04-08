# Design Document — game-management

## Overview

Esta feature adiciona dois novos endpoints ao `GamesController` (`PATCH /api/games/{id}` e `DELETE /api/games/{id}`), remove os atributos `[AllowAnonymous]` de cinco rotas que hoje são públicas, e atualiza a documentação do projeto.

A lógica de negócio mais complexa está em dois pontos:

1. **Edição com regeneração de mercados** — ao alterar `NumberOfMaps`, mercados sem apostas são removidos e novos são criados para cobrir os mapas adicionados/removidos, preservando mercados que já têm apostas.
2. **Exclusão com devolução de saldo** — ao excluir um jogo `Scheduled`, todas as apostas `Pending` e `Active` são canceladas/anuladas e o saldo reservado é devolvido aos usuários envolvidos.

Nenhuma migração de banco de dados é necessária — todas as mudanças são de lógica de serviço e configuração de autorização.

---

## Architecture

O design segue o padrão já estabelecido no projeto:

```
GamesController (HTTP layer)
    │  valida autorização (IsAdminFromDb)
    │  valida input ([Required], [Range], [StringLength])
    ▼
IGameService (domain logic)
    │  verifica status do jogo
    │  regenera mercados (edit)
    │  cancela apostas + devolve saldo (delete)
    ▼
FrogBetsDbContext (EF Core)
    │
    ▼
PostgreSQL
```

`IBalanceService.ReleaseBalanceAsync` é reutilizado para devolver saldo nas apostas canceladas durante a exclusão — o mesmo mecanismo já usado em `BetService.CancelBetAsync`.

---

## Components and Interfaces

### IGameService — novos métodos

```csharp
/// <summary>
/// Atualiza campos de um jogo Scheduled. Regenera mercados se NumberOfMaps mudar.
/// Throws KeyNotFoundException se o jogo não existir.
/// Throws InvalidOperationException("GAME_CANNOT_BE_EDITED") se status != Scheduled.
/// </summary>
Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequest request);

/// <summary>
/// Exclui um jogo Scheduled, cancelando apostas e devolvendo saldo.
/// Throws KeyNotFoundException se o jogo não existir.
/// Throws InvalidOperationException("GAME_CANNOT_BE_DELETED") se status == InProgress ou Finished.
/// </summary>
Task DeleteGameAsync(Guid gameId);
```

### UpdateGameRequest

```csharp
public record UpdateGameRequest(
    string? TeamA,
    string? TeamB,
    DateTime? ScheduledAt,
    int? NumberOfMaps
);
```

Todos os campos são opcionais — apenas os campos presentes no body são atualizados (PATCH semântico).

### PatchGameBody (controller record)

```csharp
public record PatchGameBody(
    [StringLength(100, MinimumLength = 1)] string? TeamA,
    [StringLength(100, MinimumLength = 1)] string? TeamB,
    DateTime? ScheduledAt,
    [Range(1, 5)] int? NumberOfMaps
);
```

### GamesController — novos endpoints

```csharp
/// PATCH /api/games/{id} — admin: editar jogo agendado
[HttpPatch("{id:guid}")]
[Authorize]
public async Task<IActionResult> UpdateGame(Guid id, [FromBody] PatchGameBody body)

/// DELETE /api/games/{id} — admin: excluir jogo agendado
[HttpDelete("{id:guid}")]
[Authorize]
public async Task<IActionResult> DeleteGame(Guid id)
```

### Remoção de [AllowAnonymous]

Os seguintes endpoints têm `[AllowAnonymous]` removido (passam a exigir `[Authorize]` implícito via controller ou atributo explícito):

| Controller | Método | Rota |
|---|---|---|
| `GamesController` | GET | `/api/games` |
| `GamesController` | GET | `/api/games/{id}` |
| `TeamsController` | GET | `/api/teams` |
| `PlayersController` | GET | `/api/players/ranking` |
| `PlayersController` | GET | `/api/players/{id}/stats` |

### Frontend — substituição de publicClient

As páginas que usam `publicClient` para os endpoints afetados passam a usar `apiClient`:

| Arquivo | Endpoint afetado |
|---|---|
| `GamesPage.tsx` | `GET /api/games` (já usa `apiClient`) |
| `GameDetailPage.tsx` | `GET /api/games/{id}` (já usa `apiClient`) |
| `api/players.ts` | `GET /api/players/ranking`, `GET /api/players/{id}/stats` |
| `PlayersRankingPage.tsx` | `GET /api/players/ranking` |
| Qualquer uso de `publicClient` para `/teams` | `GET /api/teams` |

### Frontend — EditGameSection (AdminPage)

Nova seção no `AdminPage` para editar jogos agendados:

- Dropdown com jogos `Scheduled`
- Campos pré-preenchidos com valores atuais (TeamA, TeamB, ScheduledAt, NumberOfMaps)
- Botão "Salvar alterações" → `PATCH /api/games/{id}`
- Feedback de sucesso/erro inline

### Frontend — DeleteGame (AdminPage)

Botão "Excluir" na listagem de jogos agendados (dentro da seção de cadastro ou nova seção dedicada):

- Confirmação via `confirm()` antes de enviar
- `DELETE /api/games/{id}` → recarrega lista de jogos

---

## Data Models

Nenhuma alteração no schema do banco de dados. As entidades existentes são suficientes:

- `Game` — campos `TeamA`, `TeamB`, `ScheduledAt`, `NumberOfMaps`, `Status` já existem
- `Market` — já tem `GameId`, `Type`, `MapNumber`, `Status`
- `Bet` — já tem `Status` (`Pending`, `Active`, `Cancelled`, `Voided`) e `MarketId`
- `User` — `VirtualBalance` e `ReservedBalance` já existem

### Lógica de regeneração de mercados (UpdateGameAsync)

Quando `NumberOfMaps` muda de `oldN` para `newN`:

1. Carregar todos os mercados do jogo com suas apostas (`Include(m => m.Bets)`)
2. Para cada mapa de 1 a `max(oldN, newN)`:
   - Se o mapa está em `oldN` mas não em `newN`: remover mercados desse mapa **que não têm apostas**. Mercados com apostas são mantidos (não podem ser removidos sem afetar integridade).
   - Se o mapa está em `newN` mas não em `oldN`: criar os 4 mercados de mapa (`MapWinner`, `TopKills`, `MostDeaths`, `MostUtilityDamage`)
3. O mercado `SeriesWinner` nunca é removido nem duplicado
4. Atualizar `game.NumberOfMaps = newN`

### Lógica de exclusão (DeleteGameAsync)

1. Carregar jogo com mercados e apostas (`Include(g => g.Markets).ThenInclude(m => m.Bets)`)
2. Verificar `Status != InProgress && Status != Finished`
3. Para cada aposta `Pending`:
   - `ReleaseBalanceAsync(bet.CreatorId, bet.Amount)` — devolve saldo ao criador
   - `bet.Status = BetStatus.Cancelled`
4. Para cada aposta `Active`:
   - `ReleaseBalanceAsync(bet.CreatorId, bet.Amount)` — devolve saldo ao criador
   - `ReleaseBalanceAsync(bet.CoveredById!.Value, bet.Amount)` — devolve saldo ao cobrador
   - `bet.Status = BetStatus.Voided`
5. Remover mercados e jogo do contexto (`_db.Games.Remove(game)` com cascade)
6. `SaveChangesAsync()` em uma única transação

> A remoção em cascata já está configurada no EF Core (Markets são dependentes de Game, Bets são dependentes de Market). A operação de saldo usa `IsolationLevel.Serializable` via `IBalanceService`, garantindo consistência.

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Edição preserva e atualiza campos corretamente

*For any* jogo com status `Scheduled` e qualquer combinação válida de campos em `UpdateGameRequest`, após chamar `UpdateGameAsync`, o `GameDto` retornado SHALL conter exatamente os novos valores para os campos fornecidos e manter os valores anteriores para os campos não fornecidos.

**Validates: Requirements 1.1**

### Property 2: Edição é rejeitada para jogos não-agendados

*For any* jogo com status `InProgress` ou `Finished`, chamar `UpdateGameAsync` SHALL lançar `InvalidOperationException("GAME_CANNOT_BE_EDITED")`.

**Validates: Requirements 1.3**

### Property 3: Regeneração de mercados preserva invariante de contagem

*For any* jogo `Scheduled` com `oldN` mapas sem apostas, ao alterar para `newN` mapas (1 ≤ newN ≤ 5), o número total de mercados após a atualização SHALL ser `newN × 4 + 1` (4 mercados por mapa + 1 SeriesWinner).

**Validates: Requirements 1.7**

### Property 4: Exclusão restaura saldo de todos os usuários envolvidos

*For any* jogo `Scheduled` com apostas `Pending` e/ou `Active`, após chamar `DeleteGameAsync`, o `VirtualBalance` de cada usuário envolvido SHALL ser igual ao seu saldo antes da criação das apostas (i.e., `VirtualBalance + ReservedBalance` antes da exclusão == `VirtualBalance` após a exclusão para cada usuário afetado).

**Validates: Requirements 2.1**

### Property 5: Exclusão é rejeitada para jogos em andamento ou finalizados

*For any* jogo com status `InProgress` ou `Finished`, chamar `DeleteGameAsync` SHALL lançar `InvalidOperationException("GAME_CANNOT_BE_DELETED")`.

**Validates: Requirements 2.3**

---

## Error Handling

| Situação | Exceção no serviço | HTTP response |
|---|---|---|
| Jogo não encontrado (edit/delete) | `KeyNotFoundException` | `404 Not Found` + `GAME_NOT_FOUND` |
| Jogo não está `Scheduled` (edit) | `InvalidOperationException("GAME_CANNOT_BE_EDITED")` | `409 Conflict` + `GAME_CANNOT_BE_EDITED` |
| Jogo está `InProgress` ou `Finished` (delete) | `InvalidOperationException("GAME_CANNOT_BE_DELETED")` | `409 Conflict` + `GAME_CANNOT_BE_DELETED` |
| Body inválido (campos fora do range) | Model validation | `400 Bad Request` |
| Usuário não autenticado | ASP.NET Core auth middleware | `401 Unauthorized` |
| Usuário não é admin | `IsAdminFromDb()` retorna false | `403 Forbidden` |

Todos os erros seguem o formato padrão do projeto:

```json
{ "error": { "code": "GAME_CANNOT_BE_EDITED", "message": "O jogo não pode ser editado pois não está agendado." } }
```

---

## Testing Strategy

### Testes unitários (xUnit + InMemory)

Arquivo: `tests/FrogBets.Tests/GameManagementTests.cs`

Cobrir com exemplos específicos:
- `UpdateGameAsync` com jogo inexistente → `KeyNotFoundException`
- `DeleteGameAsync` com jogo inexistente → `KeyNotFoundException`
- Autorização: não-admin recebe 403 (testes de integração)
- Autenticação: sem JWT recebe 401 (testes de integração / smoke)

### Testes de propriedade (FsCheck)

Arquivo: `tests/FrogBets.Tests/GameManagementPropertyTests.cs`

Biblioteca: **FsCheck** (já usada no projeto, via `FsCheck.Xunit`)

Configuração mínima: `[Property(MaxTest = 100)]` por propriedade.

```csharp
// Feature: game-management, Property 1: Edição preserva e atualiza campos corretamente
[Property(MaxTest = 100)]
public Property UpdateGame_PreservesAndUpdatesFields() { ... }

// Feature: game-management, Property 2: Edição é rejeitada para jogos não-agendados
[Property(MaxTest = 100)]
public Property UpdateGame_RejectsNonScheduledGames() { ... }

// Feature: game-management, Property 3: Regeneração de mercados preserva invariante de contagem
[Property(MaxTest = 100)]
public Property UpdateGame_MarketCountInvariant() { ... }

// Feature: game-management, Property 4: Exclusão restaura saldo de todos os usuários envolvidos
[Property(MaxTest = 100)]
public Property DeleteGame_RestoresAllBalances() { ... }

// Feature: game-management, Property 5: Exclusão é rejeitada para jogos em andamento ou finalizados
[Property(MaxTest = 100)]
public Property DeleteGame_RejectsActiveOrFinishedGames() { ... }
```

Geradores necessários:
- `Gen.Elements(GameStatus.InProgress, GameStatus.Finished)` para propriedades 2 e 5
- `Gen.Choose(1, 5)` para `NumberOfMaps` (oldN e newN)
- Gerador de `UpdateGameRequest` com campos opcionais aleatórios

### Testes de integração

Arquivo: `tests/FrogBets.Tests/Integration/GamesIntegrationTests.cs` (já existe — adicionar casos)

Novos casos:
- `PATCH /api/games/{id}` sem JWT → 401
- `PATCH /api/games/{id}` com usuário não-admin → 403
- `PATCH /api/games/{id}` com jogo `InProgress` → 409
- `DELETE /api/games/{id}` sem JWT → 401
- `DELETE /api/games/{id}` com usuário não-admin → 403
- `DELETE /api/games/{id}` com jogo `InProgress` → 409
- `GET /api/games` sem JWT → 401
- `GET /api/games/{id}` sem JWT → 401
- `GET /api/teams` sem JWT → 401
- `GET /api/players/ranking` sem JWT → 401
- `GET /api/players/{id}/stats` sem JWT → 401

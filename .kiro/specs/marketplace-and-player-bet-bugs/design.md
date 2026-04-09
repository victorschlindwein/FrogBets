# marketplace-and-player-bet-bugs — Bugfix Design

## Overview

Dois bugs independentes afetam funcionalidades centrais da plataforma FrogBets:

**Bug 1 — Marketplace tela branca:** O `BetService.ToDto` retorna `BetDto` com campos planos (`MarketType`, `MapNumber`, `GameId`), mas a interface `MarketplaceBet` no frontend espera `market: { type, mapNumber, gameId }` aninhado. Como `bet.market` é `undefined` em runtime, a chamada `marketLabel(bet.market)` lança uma exceção que quebra o render do componente React inteiro.

**Bug 2 — Players faltando nas apostas:** O endpoint `GET /api/games/{id}/players` consulta `_db.Users` filtrando por `TeamId`, mas os jogadores de CS2 são entidades `CS2Player` separadas. Usuários sem `TeamId` vinculado (ou `CS2Player` sem `User` correspondente) ficam de fora, resultando em dropdown incompleto na `GameDetailPage`.

A estratégia de fix é minimal e cirúrgica: Bug 1 é corrigido no frontend mapeando os campos planos do `BetDto` para o formato aninhado esperado; Bug 2 é corrigido no backend alterando a query para buscar `CS2Player` em vez de `User`.

---

## Glossary

- **Bug_Condition (C)**: A condição que ativa o bug — para Bug 1: quando a API retorna `BetDto` com campos planos e o frontend tenta acessar `bet.market.type`; para Bug 2: quando o endpoint busca `Users` em vez de `CS2Players`.
- **Property (P)**: O comportamento correto esperado — para Bug 1: `bet.market` deve ser um objeto válido com `type`, `mapNumber` e `gameId`; para Bug 2: o endpoint deve retornar todos os `CS2Player` dos times do jogo.
- **Preservation**: Comportamentos existentes que não devem ser alterados pelo fix — cobertura de apostas, exibição de times, mensagem de lista vazia, retorno 404 para jogo inexistente.
- **BetDto**: Record C# em `IBetService.cs` que representa uma aposta serializada com campos planos (`MarketType`, `MapNumber`, `GameId`).
- **MarketplaceBet**: Interface TypeScript em `MarketplacePage.tsx` que espera `market: { type, mapNumber, gameId }` aninhado.
- **CS2Player**: Entidade de domínio em `src/FrogBets.Domain/Entities/CS2Player.cs` com `TeamId`, `Nickname` e `UserId` (nullable).
- **GetGamePlayers**: Endpoint `GET /api/games/{id}/players` em `GamesController.cs` que lista jogadores dos times de um jogo.

---

## Bug Details

### Bug 1 — Marketplace tela branca

O bug manifesta quando o usuário acessa `/marketplace` e a API retorna apostas pendentes. O `BetService.ToDto` serializa `b.Market.Type`, `b.Market.MapNumber` e `b.Market.GameId` como campos de primeiro nível no `BetDto`, mas o frontend espera um objeto `market` aninhado. Como o JSON não contém a chave `market`, `bet.market` é `undefined` em runtime, e `marketLabel(bet.market)` lança `TypeError: Cannot read properties of undefined`.

**Formal Specification:**
```
FUNCTION isBugCondition_Bug1(response)
  INPUT: response de tipo BetDto[] retornado por GET /api/marketplace
  OUTPUT: boolean

  RETURN response.length > 0
         AND response[0].market === undefined
         AND response[0].marketType !== undefined
END FUNCTION
```

**Exemplos:**

- API retorna `{ id: "...", marketType: "MapWinner", mapNumber: 1, gameId: "..." }` → `bet.market` é `undefined` → `marketLabel(undefined)` lança `TypeError` → tela branca
- API retorna `{ id: "...", marketType: "SeriesWinner", mapNumber: null, gameId: "..." }` → mesmo problema
- API retorna array vazio `[]` → `bets.length === 0` → renderiza mensagem "Nenhuma aposta disponível" → **sem bug** (caso de preservação)

---

### Bug 2 — Players faltando nas apostas

O bug manifesta quando o usuário acessa a `GameDetailPage` de um jogo cujos times possuem `CS2Player` cadastrados. O endpoint busca `Users` onde `u.TeamId` está nos times, mas `CS2Player` é uma entidade separada — um `CS2Player` pode existir sem `User` correspondente (ou com `UserId` nulo). Apenas usuários com `TeamId` vinculado aparecem; `CS2Player` sem `User` ficam de fora.

**Formal Specification:**
```
FUNCTION isBugCondition_Bug2(gameId, db)
  INPUT: gameId de tipo Guid, db de tipo FrogBetsDbContext
  OUTPUT: boolean

  teamIds := db.CS2Teams WHERE Name IN (game.TeamA, game.TeamB) SELECT Id
  cs2Players := db.CS2Players WHERE TeamId IN teamIds
  usersWithTeam := db.Users WHERE TeamId IN teamIds

  RETURN cs2Players.Count > usersWithTeam.Count
         OR EXISTS p IN cs2Players WHERE p.UserId IS NULL
END FUNCTION
```

**Exemplos:**

- Jogo com TeamA (3 CS2Players, 2 com UserId) e TeamB (3 CS2Players, 1 com UserId) → endpoint retorna 3 usuários em vez de 6 CS2Players
- Jogo com todos os CS2Players tendo UserId vinculado → endpoint retorna resultado correto por coincidência, mas a query ainda está semanticamente errada
- Jogo inexistente → retorna 404 → **sem bug** (caso de preservação)

---

## Expected Behavior

### Preservation Requirements

**Comportamentos que não devem mudar:**

- Quando não há apostas pendentes no marketplace, o sistema deve continuar exibindo "Nenhuma aposta disponível para cobertura"
- Quando o usuário cobre uma aposta com sucesso, a aposta deve continuar sendo removida da lista
- Quando a API falha ao carregar o marketplace, o sistema deve continuar exibindo a mensagem de erro
- Quando a `GameDetailPage` exibe mercados de time (MapWinner, SeriesWinner), os nomes dos dois times devem continuar aparecendo como opções
- Quando o jogo não está com status `Scheduled`, o formulário de aposta não deve aparecer
- Quando `GET /api/games/{id}/players` é chamado para um jogo inexistente, deve continuar retornando 404 com `GAME_NOT_FOUND`
- A seção de Trocas de Jogadores no marketplace deve continuar funcionando normalmente

**Escopo:**

Todas as entradas que NÃO envolvem a renderização de `BetRow` com `bet.market` (Bug 1) ou a listagem de jogadores de um jogo (Bug 2) devem ser completamente inalteradas pelo fix.

---

## Hypothesized Root Cause

### Bug 1 — Marketplace tela branca

1. **Mismatch de contrato entre backend e frontend**: O `BetDto` foi projetado como um DTO genérico para múltiplos contextos (`BetsPage`, `MarketplacePage`). A `MarketplacePage` foi escrita esperando um formato aninhado `market: { type, mapNumber, gameId }`, mas o `BetDto` serializa esses campos no nível raiz. Nenhum dos dois lados está "errado" isoladamente — o problema é a ausência de um mapeamento explícito no frontend ao consumir o endpoint `/marketplace`.

2. **Ausência de mapeamento no `useEffect`**: O `useEffect` em `MarketplacePage` faz `setBets(res.data)` diretamente sem transformar a resposta. Se houvesse um `res.data.map(dto => ({ ...dto, market: { type: dto.marketType, mapNumber: dto.mapNumber, gameId: dto.gameId } }))`, o bug não existiria.

### Bug 2 — Players faltando nas apostas

1. **Query na entidade errada**: `GetGamePlayers` consulta `_db.Users` em vez de `_db.CS2Players`. A intenção do endpoint é listar jogadores de CS2 dos times — a entidade correta é `CS2Player`, não `User`. Um `CS2Player` pode existir sem `User` vinculado (campo `UserId` é nullable).

2. **Confusão entre User e CS2Player**: O sistema tem duas entidades distintas — `User` (conta da plataforma) e `CS2Player` (jogador de CS2 com stats). Um `User` pode ter `TeamId` (membro de time), mas isso não o torna um `CS2Player`. A query atual mistura esses conceitos.

---

## Correctness Properties

Property 1: Bug Condition — Mapeamento de BetDto para market aninhado

_For any_ resposta da API `/marketplace` contendo um array de `BetDto` com campos planos (`marketType`, `mapNumber`, `gameId`), o frontend SHALL mapear cada item para um objeto `MarketplaceBet` com `market.type === dto.marketType`, `market.mapNumber === dto.mapNumber` e `market.gameId === dto.gameId`, sem lançar exceções durante a renderização.

**Validates: Requirements 2.1, 2.2**

Property 2: Bug Condition — Listagem completa de CS2Players por jogo

_For any_ jogo com N `CS2Player` distribuídos entre os dois times (independentemente de terem `UserId` vinculado ou não), o endpoint `GET /api/games/{id}/players` SHALL retornar exatamente N jogadores, com `id`, `nickname` e `teamName` de cada `CS2Player`.

**Validates: Requirements 2.3, 2.4**

Property 3: Preservation — Comportamento do marketplace sem apostas

_For any_ chamada a `GET /api/marketplace` que retorne array vazio, o frontend SHALL continuar exibindo a mensagem "Nenhuma aposta disponível para cobertura" sem erros, idêntico ao comportamento anterior ao fix.

**Validates: Requirements 3.1**

Property 4: Preservation — Comportamento do endpoint para jogo inexistente

_For any_ `gameId` que não corresponda a um jogo existente no banco, `GET /api/games/{id}/players` SHALL continuar retornando 404 com `{ error: { code: "GAME_NOT_FOUND" } }`, idêntico ao comportamento anterior ao fix.

**Validates: Requirements 3.5**

---

## Fix Implementation

### Bug 1 — Frontend: mapear BetDto para MarketplaceBet

**Arquivo:** `frontend/src/pages/MarketplacePage.tsx`

**Mudança:** No `useEffect` que chama `GET /api/marketplace`, transformar a resposta antes de chamar `setBets`. Adicionar uma interface `BetDtoResponse` que reflete o formato real da API, e mapear para `MarketplaceBet`.

**Pseudocódigo da transformação:**
```
FUNCTION mapDtoToMarketplaceBet(dto: BetDtoResponse): MarketplaceBet
  RETURN {
    id:            dto.id,
    marketId:      dto.marketId,
    creatorOption: dto.creatorOption,
    amount:        dto.amount,
    creatorId:     dto.creatorId,
    market: {
      type:      dto.marketType,
      mapNumber: dto.mapNumber,
      gameId:    dto.gameId
    }
  }
END FUNCTION
```

**Alterações específicas:**
1. Adicionar interface `BetDtoResponse` com os campos planos retornados pela API
2. No `useEffect`, substituir `setBets(res.data)` por `setBets(res.data.map(mapDtoToMarketplaceBet))`
3. A interface `MarketplaceBet` existente permanece inalterada (é o formato interno correto)

---

### Bug 2 — Backend: buscar CS2Player em vez de User

**Arquivo:** `src/FrogBets.Api/Controllers/GamesController.cs`

**Função:** `GetGamePlayers`

**Mudança:** Substituir a query em `_db.Users` por uma query em `_db.CS2Players`, incluindo a navegação `Team`.

**Antes:**
```csharp
var players = await _db.Users
    .Include(u => u.Team)
    .AsNoTracking()
    .Where(u => u.TeamId.HasValue && teamIds.Contains(u.TeamId.Value))
    .OrderBy(u => u.Team!.Name).ThenBy(u => u.Username)
    .Select(u => new { id = u.Id, nickname = u.Username, teamName = u.Team!.Name })
    .ToListAsync();
```

**Depois:**
```csharp
var players = await _db.CS2Players
    .Include(p => p.Team)
    .AsNoTracking()
    .Where(p => p.TeamId.HasValue && teamIds.Contains(p.TeamId.Value))
    .OrderBy(p => p.Team!.Name).ThenBy(p => p.Nickname)
    .Select(p => new { id = p.Id, nickname = p.Nickname, teamName = p.Team!.Name })
    .ToListAsync();
```

**Alterações específicas:**
1. Trocar `_db.Users` por `_db.CS2Players`
2. Trocar `Include(u => u.Team)` por `Include(p => p.Team)`
3. Trocar `u.TeamId` por `p.TeamId` no `Where`
4. Trocar `u.Username` por `p.Nickname` no `OrderBy` e `Select`
5. O contrato de resposta (`id`, `nickname`, `teamName`) permanece idêntico — sem breaking change

---

## Testing Strategy

### Validation Approach

A estratégia segue duas fases: primeiro, confirmar o bug no código não corrigido (exploratory); depois, verificar o fix e a preservação de comportamentos existentes.

---

### Exploratory Bug Condition Checking

**Goal**: Demonstrar os bugs no código atual antes do fix. Confirmar ou refutar a análise de causa raiz.

**Bug 1 — Test Plan**: Mockar `GET /api/marketplace` retornando um `BetDto` com campos planos e verificar que o componente lança erro ou renderiza em branco.

**Test Cases:**
1. **Marketplace com BetDto plano**: Mockar resposta com `{ marketType: "MapWinner", mapNumber: 1, gameId: "..." }` (sem `market` aninhado) → espera-se que o componente falhe ao renderizar (vai passar no código bugado, falhar no corrigido se o mock não for atualizado)
2. **marketLabel com undefined**: Chamar `marketLabel(undefined as any)` diretamente → espera-se `TypeError`

**Bug 2 — Test Plan**: Criar um jogo com `CS2Players` sem `User` correspondente e chamar o endpoint, verificando que esses jogadores não aparecem no resultado (comportamento bugado).

**Test Cases:**
1. **CS2Player sem UserId**: Seed com 2 CS2Players (um com UserId, um sem) no mesmo time → endpoint retorna apenas 1 (comportamento bugado confirmado)
2. **Todos CS2Players com UserId**: Seed com 2 CS2Players ambos com UserId → endpoint retorna 2 (coincidência — bug não manifesta neste caso)

**Expected Counterexamples:**
- Bug 1: `TypeError: Cannot read properties of undefined (reading 'type')` ao renderizar `BetRow`
- Bug 2: Resposta com N < total de CS2Players quando algum não tem UserId vinculado

---

### Fix Checking

**Goal**: Verificar que para todas as entradas onde a condição de bug se aplica, o código corrigido produz o comportamento esperado.

**Pseudocode:**
```
FOR ALL dto WHERE isBugCondition_Bug1(dto) DO
  result := renderMarketplacePage(dto)
  ASSERT result.market.type === dto.marketType
  ASSERT result.market.mapNumber === dto.mapNumber
  ASSERT result.market.gameId === dto.gameId
  ASSERT noExceptionThrown
END FOR

FOR ALL (gameId, db) WHERE isBugCondition_Bug2(gameId, db) DO
  result := GetGamePlayers_fixed(gameId, db)
  ASSERT result.Count === db.CS2Players.Count(p => teamIds.Contains(p.TeamId))
  ASSERT ALL p IN result: p.nickname === CS2Player.Nickname
END FOR
```

---

### Preservation Checking

**Goal**: Verificar que para entradas onde a condição de bug NÃO se aplica, o comportamento é idêntico ao original.

**Pseudocode:**
```
FOR ALL dto WHERE NOT isBugCondition_Bug1(dto) DO
  ASSERT render_original(dto) === render_fixed(dto)
END FOR

FOR ALL (gameId, db) WHERE NOT isBugCondition_Bug2(gameId, db) DO
  ASSERT GetGamePlayers_original(gameId, db) === GetGamePlayers_fixed(gameId, db)
END FOR
```

**Testing Approach**: Property-based testing é recomendado para a preservação do Bug 2 porque permite gerar muitas combinações de jogos e times automaticamente, garantindo que o comportamento para jogos inexistentes (404) e jogos sem CS2Players seja preservado.

**Test Cases:**
1. **Marketplace vazio**: Mockar resposta vazia → verificar mensagem "Nenhuma aposta disponível" continua aparecendo
2. **Cobertura de aposta**: Mockar `POST /bets/{id}/cover` com sucesso → verificar que a aposta é removida da lista
3. **Jogo inexistente**: Chamar `GET /api/games/{id}/players` com GUID inválido → verificar 404 com `GAME_NOT_FOUND`
4. **Jogo com apenas CS2Players com UserId**: Verificar que o resultado é idêntico ao original (todos aparecem)

---

### Unit Tests

**Frontend (Vitest):**
- `MarketplacePage` renderiza corretamente com `BetDto` de campos planos (após fix)
- `marketLabel` não lança exceção com `market` válido
- `BetRow` exibe mercado, opção e valor corretamente
- `MarketplacePage` exibe mensagem vazia quando API retorna `[]`

**Backend (xUnit):**
- `GetGamePlayers` retorna CS2Players sem UserId vinculado
- `GetGamePlayers` retorna todos os CS2Players dos dois times
- `GetGamePlayers` retorna 404 para jogo inexistente
- `GetGamePlayers` ordena por nome do time e depois por nickname

---

### Property-Based Tests

**Frontend (fast-check via Vitest):**
- Para qualquer array de `BetDto` com campos planos, o mapeamento produz `market.type === dto.marketType` para todos os itens (Property 1)
- Para qualquer array de `BetDto` com campos planos, o componente renderiza sem lançar exceções (Property 1)

**Backend (FsCheck via xUnit):**
- Para qualquer N CS2Players distribuídos entre dois times de um jogo, `GetGamePlayers` retorna exatamente N jogadores (Property 2)
- Para qualquer `gameId` inválido (GUID aleatório não existente no banco), `GetGamePlayers` retorna 404 (Property 4)

---

### Integration Tests

**Backend:**
- Fluxo completo: criar jogo com times → criar CS2Players sem UserId → chamar `GET /api/games/{id}/players` → verificar que todos aparecem
- Fluxo completo: criar jogo → chamar `GET /api/marketplace` → verificar que o contrato de resposta inclui os campos necessários para o mapeamento frontend

**Frontend (Cypress):**
- Acessar `/marketplace` com apostas pendentes → verificar que a lista renderiza sem tela branca
- Acessar `GameDetailPage` de um jogo → verificar que o dropdown de jogadores exibe todos os CS2Players dos dois times

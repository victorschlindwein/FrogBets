# Design Document — register-result-players-dropdown

## Overview

Esta feature substitui os campos de texto livre para mercados de jogador (`TopKills`, `MostDeaths`, `MostUtilityDamage`) na seção "Registrar Resultado" do `AdminPage` por dropdowns SELECT populados com os usuários reais que pertencem aos times do jogo. Paralelamente, o endpoint `GET /api/games/{id}/players` é reorientado para consultar `Users` (via `TeamId`) em vez de `CS2Players`, preparando a migração futura. A ordem de exibição dos mercados também é reorganizada: Team_Markets primeiro, Player_Markets depois.

## Architecture

A mudança é full-stack e envolve duas camadas independentes:

```
Frontend (React/TSX)                    Backend (ASP.NET Core 8)
─────────────────────────────────────   ──────────────────────────────────────
RegisterResultSection                   GamesController
  ├── carrega jogadores via             GET /api/games/{id}/players
  │   GET /api/games/{id}/players  ──►    └── query: Users WHERE TeamId IN
  ├── renderiza SELECT para                     (teamA_id, teamB_id)
  │   Player_Markets                      └── retorna: id, username, teamName
  ├── ordena mercados (Team first)
  └── submete username como
      winningOption
      POST /api/games/{id}/results ──►  GamesController.RegisterResult
                                          (sem alteração — já aceita username)
```

O backend não precisa de nova migração nem de novo serviço — apenas a query do endpoint existente muda. O frontend adiciona estado para a lista de jogadores e troca `<input type="text">` por `<select>` nos Player_Markets.

## Components and Interfaces

### Backend: `GamesController.GetGamePlayers`

Método existente reescrito. A nova query:

```csharp
// 1. Busca os IDs dos CS2Teams pelo nome (TeamA/TeamB do jogo)
var teamIds = await _db.CS2Teams
    .AsNoTracking()
    .Where(t => t.Name == game.TeamA || t.Name == game.TeamB)
    .Select(t => t.Id)
    .ToListAsync();

// 2. Busca Users cujo TeamId está nesses CS2Teams
var players = await _db.Users
    .Include(u => u.Team)
    .AsNoTracking()
    .Where(u => u.TeamId.HasValue && teamIds.Contains(u.TeamId.Value))
    .OrderBy(u => u.Team!.Name).ThenBy(u => u.Username)
    .Select(u => new { id = u.Id, username = u.Username, teamName = u.Team!.Name })
    .ToListAsync();
```

### Frontend: novo tipo `GamePlayer` (atualização)

O tipo `GamePlayer` em `frontend/src/api/players.ts` precisa adicionar o campo `username`:

```typescript
export interface GamePlayer {
  id: string;
  username: string;   // novo — substitui nickname
  teamName: string;
}
```

### Frontend: `RegisterResultSection` (alterações)

Novos estados adicionados ao componente:

```typescript
const [gamePlayers, setGamePlayers] = useState<GamePlayer[]>([])
const [loadingPlayers, setLoadingPlayers] = useState(false)
const [playersError, setPlayersError] = useState<string | null>(null)
```

Lógica de carregamento de jogadores (dentro do `useEffect` que já carrega mercados):

```typescript
// Carrega jogadores em paralelo com os mercados
setLoadingPlayers(true)
getGamePlayers(selectedGameId)
  .then(setGamePlayers)
  .catch(() => setPlayersError('Erro ao carregar jogadores.'))
  .finally(() => setLoadingPlayers(false))
```

Função de ordenação de mercados:

```typescript
const MARKET_ORDER: Record<string, number> = {
  MapWinner: 0, SeriesWinner: 1,
  TopKills: 2, MostDeaths: 3, MostUtilityDamage: 4,
}

function sortMarkets(markets: Market[]): Market[] {
  return [...markets].sort((a, b) => {
    const typeDiff = (MARKET_ORDER[a.type] ?? 99) - (MARKET_ORDER[b.type] ?? 99)
    if (typeDiff !== 0) return typeDiff
    return (a.mapNumber ?? 0) - (b.mapNumber ?? 0)
  })
}
```

Renderização do campo por tipo de mercado:

```tsx
const PLAYER_MARKETS = ['TopKills', 'MostDeaths', 'MostUtilityDamage']

// Para Player_Markets:
{PLAYER_MARKETS.includes(m.type) && (
  <select
    value={results[m.id] ?? ''}
    onChange={e => setResults(r => ({ ...r, [m.id]: e.target.value }))}
    disabled={gamePlayers.length === 0}
  >
    <option value="">— pular —</option>
    {gamePlayers.map(p => (
      <option key={p.id} value={p.username}>
        {p.username} ({p.teamName})
      </option>
    ))}
  </select>
)}
```

## Data Models

### Contrato do endpoint `GET /api/games/{id}/players` (novo)

```json
[
  { "id": "uuid", "username": "string", "teamName": "string" },
  ...
]
```

**Antes:** retornava `{ id, nickname, teamName }` baseado em `CS2Players`.  
**Depois:** retorna `{ id, username, teamName }` baseado em `Users`.

O campo `id` agora é `User.Id` (Guid) em vez de `CS2Player.Id`. O campo `username` substitui `nickname`. O campo `teamName` permanece com a mesma semântica (nome do `CS2Team` associado).

### Tipo `GamePlayer` no frontend

```typescript
// Antes
export interface GamePlayer {
  id: string;
  nickname: string;
  teamName: string;
}

// Depois
export interface GamePlayer {
  id: string;
  username: string;
  teamName: string;
}
```

O campo `nickname` é removido e substituído por `username`. Nenhum outro componente do frontend usa `GamePlayer.nickname` diretamente (verificado: `getGamePlayers` só é chamado em `AdminPage.tsx` dentro de `RegisterResultSection`).

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Dropdown options reflect GamePlayer data

*For any* list of `GamePlayer` objects returned by the endpoint, each Player_Market SELECT SHALL render exactly one `<option>` per player with `value = player.username` and label text equal to `"player.username (player.teamName)"`, plus one `<option value="">— pular —</option>` as the first option.

**Validates: Requirements 1.2, 4.1, 4.3**

### Property 2: Submitted winningOption is the selected username

*For any* Player_Market and any player selected from the dropdown, when the form is submitted, the `winningOption` sent to `POST /api/games/{id}/results` SHALL equal the `username` of the selected player (not the player's `id`).

**Validates: Requirements 1.3, 4.2**

### Property 3: Market ordering groups Team_Markets before Player_Markets

*For any* list of markets in any order, after applying the sort function, all markets of type `MapWinner` or `SeriesWinner` SHALL appear before all markets of type `TopKills`, `MostDeaths`, or `MostUtilityDamage`. Within each group, markets SHALL be ordered by ascending `mapNumber` (null treated as 0).

**Validates: Requirements 2.1, 2.2**

### Property 4: Endpoint returns correct users with correct fields

*For any* game with teams A and B, `GET /api/games/{id}/players` SHALL return exactly the set of `Users` whose `TeamId` matches the `CS2Team.Id` of team A or team B, and each returned object SHALL contain the fields `id` (= `User.Id`), `username` (= `User.Username`), and `teamName` (= `CS2Team.Name` associated with the user's `TeamId`).

**Validates: Requirements 3.1, 3.2**

## Error Handling

| Cenário | Comportamento |
|---|---|
| `GET /api/games/{id}/players` retorna lista vazia | Dropdowns de Player_Market renderizados com `disabled`, mensagem "Nenhum jogador encontrado para este jogo" |
| `GET /api/games/{id}/players` retorna erro HTTP | `playersError` é setado, mensagem de erro exibida, campos de Player_Market inacessíveis |
| Jogo não encontrado no backend | HTTP 404 com `{ error: { code: "GAME_NOT_FOUND" } }` |
| Nenhum usuário nos times do jogo | HTTP 200 com `[]` (lista vazia) |
| Submissão sem nenhum resultado preenchido | Validação frontend: "Preencha ao menos um resultado." (comportamento existente mantido) |

O carregamento de jogadores e de mercados ocorre em paralelo no mesmo `useEffect`. Se o carregamento de jogadores falhar mas o de mercados tiver sucesso, os Team_Markets (que não dependem de jogadores) continuam funcionais; apenas os Player_Markets ficam inacessíveis.

## Testing Strategy

### Abordagem dual

- **Testes unitários/de componente (Vitest + Testing Library + MSW):** cobrem cenários específicos, edge cases e integração entre estados do componente.
- **Property-based tests (fast-check via Vitest):** cobrem as 4 propriedades de corretude com inputs gerados aleatoriamente.

### Testes unitários (frontend)

Arquivo: `frontend/src/pages/AdminPage.registerresult.test.tsx`

Cenários a cobrir:
- Renderiza dropdowns SELECT para Player_Markets quando jogadores são carregados
- Renderiza SELECT com opção "— pular —" como padrão
- Exibe mensagem de erro quando endpoint de jogadores falha
- Exibe dropdowns desabilitados quando lista de jogadores está vazia
- Submete `winningOption` correto (username, não id)
- Team_Markets continuam usando SELECT de times (comportamento existente)

### Property-based tests (frontend)

Arquivo: `frontend/src/pages/AdminPage.registerresult.property.test.tsx`

Biblioteca: `fast-check` (já disponível no projeto via `frontend/src/test/fc-helpers.ts`)

Configuração: mínimo 100 iterações por propriedade.

```typescript
// Feature: register-result-players-dropdown, Property 1: Dropdown options reflect GamePlayer data
it.prop([fc.array(fc.record({ id: fc.uuid(), username: fc.string(), teamName: fc.string() }))])(
  'dropdown options reflect GamePlayer data',
  async (players) => { ... }
)

// Feature: register-result-players-dropdown, Property 2: Submitted winningOption is the selected username
// Feature: register-result-players-dropdown, Property 3: Market ordering groups Team_Markets before Player_Markets
// Feature: register-result-players-dropdown, Property 4: Endpoint returns correct users with correct fields
```

### Testes de integração (backend)

Arquivo: `tests/FrogBets.IntegrationTests/Controllers/GamesControllerPlayersTests.cs`

Cenários:
- Retorna 404 com `GAME_NOT_FOUND` para jogo inexistente (Requirement 3.3)
- Retorna 200 com `[]` quando nenhum usuário pertence aos times (Requirement 3.4)
- Retorna 401 sem token de autenticação (Requirement 3.5)
- Retorna 200 com token de usuário comum (não-admin) (Requirement 3.5)

### Testes unitários (backend — property-based com FsCheck)

Arquivo: `tests/FrogBets.Tests/GamePlayersEndpointTests.cs`

```csharp
// Feature: register-result-players-dropdown, Property 4: Endpoint returns correct users with correct fields
[Property(MaxTest = 100)]
public Property GetGamePlayers_ReturnsOnlyUsersFromGameTeams() { ... }
```

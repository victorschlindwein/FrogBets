# Player Market Dropdown — Bugfix Design

## Overview

Em `GameDetailPage.tsx`, mercados de jogador (`TopKills`, `MostDeaths`, `MostUtilityDamage`) renderizam um `<input type="text">` livre em vez de um `<select>` com os jogadores dos dois times do jogo. O fix consiste em:

1. Adicionar um endpoint `GET /api/games/{id}/players` no backend que retorna os jogadores dos times TeamA e TeamB do jogo.
2. Substituir o `<input>` pelo `<select>` no `BetForm`, carregando os jogadores via esse endpoint, no formato `"Nickname - Nome do Time"`.

A mudança é cirúrgica: apenas o branch `else` do `BetForm` é afetado. O comportamento de mercados de time e o envio de `creatorOption` permanecem intactos.

## Glossary

- **Bug_Condition (C)**: A condição que dispara o bug — quando `market.type` é `TopKills`, `MostDeaths` ou `MostUtilityDamage` (i.e., `!isTeamMarket`), o componente renderiza `<input type="text">` em vez de `<select>`.
- **Property (P)**: O comportamento correto — para mercados de jogador, o campo de opção deve ser um `<select>` populado com os jogadores dos times do jogo no formato `"Nickname - Nome do Time"`.
- **Preservation**: O comportamento de mercados de time (`MapWinner`, `SeriesWinner`) e o envio de `creatorOption` via `POST /api/bets` que não devem ser alterados.
- **BetForm**: Componente React em `GameDetailPage.tsx` que renderiza o formulário de criação de aposta para um mercado.
- **isTeamMarket**: Flag booleana em `BetForm` que determina se o mercado é de time (`MapWinner`, `SeriesWinner`). Atualmente, `!isTeamMarket` implica input de texto — esse é o branch bugado.
- **PLAYER_MARKETS**: Conjunto `['TopKills', 'MostDeaths', 'MostUtilityDamage']` — mercados que devem usar dropdown de jogadores.
- **GamePlayers endpoint**: Novo endpoint `GET /api/games/{id}/players` que retorna jogadores dos times TeamA e TeamB do jogo.

## Bug Details

### Bug Condition

O bug se manifesta quando `BetForm` é renderizado para um mercado cujo `type` pertence a `PLAYER_MARKETS`. O branch `else` do ternário que decide o campo de opção renderiza `<input type="text">` incondicionalmente para qualquer mercado que não seja de time, sem distinguir mercados de jogador.

**Formal Specification:**
```
FUNCTION isBugCondition(market)
  INPUT: market de tipo Market
  OUTPUT: boolean

  RETURN market.type IN ['TopKills', 'MostDeaths', 'MostUtilityDamage']
         AND o campo renderizado é <input type="text">
         AND NÃO é um <select> com jogadores do jogo
END FUNCTION
```

### Examples

- **TopKills, Mapa 1**: usuário vê campo de texto livre → deveria ver dropdown com jogadores dos dois times
- **MostDeaths, Mapa 2**: usuário digita qualquer string → deveria selecionar apenas jogadores válidos
- **MostUtilityDamage (série)**: campo aceita "jogador inexistente" → deveria restringir às opções do jogo
- **MapWinner** (não afetado): já exibe dropdown com TeamA/TeamB corretamente

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Mercados de time (`MapWinner`, `SeriesWinner`) continuam exibindo dropdown com `game.teamA` e `game.teamB`
- O campo `creatorOption` continua sendo enviado no body de `POST /api/bets` com o valor selecionado
- O formulário continua funcionando quando não há jogadores cadastrados (dropdown vazio, sem crash)
- Todos os outros campos do formulário (amount, submit, feedback de sucesso/erro) permanecem inalterados

**Scope:**
Todos os inputs que NÃO envolvem mercados de jogador devem ser completamente não afetados por este fix. Isso inclui:
- Cliques e interações com mercados de time
- O campo de valor (amount)
- O fluxo de submit e tratamento de erros

## Hypothesized Root Cause

O `BetForm` usa uma flag binária `isTeamMarket` para decidir o tipo de campo:

```tsx
const isTeamMarket = TEAM_MARKETS.includes(market.type)
// ...
{isTeamMarket ? <select>...</select> : <input type="text" />}
```

O problema é que o branch `else` assume que qualquer mercado não-time deve usar texto livre. Não existe tratamento específico para mercados de jogador. As causas são:

1. **Lógica binária incompleta**: A distinção deveria ser ternária (time / jogador / outro), não binária.
2. **Ausência de fonte de dados para jogadores**: Não existe chamada de API para buscar os jogadores do jogo no contexto do `BetForm` ou `GameDetailPage`.
3. **Ausência de endpoint dedicado**: `GET /api/players` existe mas é admin-only e retorna todos os jogadores. Não há endpoint que filtre por jogo/time para usuários autenticados.

## Correctness Properties

Property 1: Bug Condition — Mercados de Jogador Renderizam Dropdown

_For any_ mercado cujo `type` pertence a `['TopKills', 'MostDeaths', 'MostUtilityDamage']` (isBugCondition retorna true), o `BetForm` corrigido SHALL renderizar um `<select>` populado com os jogadores dos times `game.teamA` e `game.teamB`, no formato `"Nickname - Nome do Time"`, em vez de um `<input type="text">`.

**Validates: Requirements 2.1, 2.2**

Property 2: Preservation — Mercados de Time Não São Afetados

_For any_ mercado cujo `type` pertence a `['MapWinner', 'SeriesWinner']` (isBugCondition retorna false), o `BetForm` corrigido SHALL renderizar exatamente o mesmo `<select>` com `game.teamA` e `game.teamB` que o código original renderiza, preservando o comportamento existente de mercados de time.

**Validates: Requirements 3.1, 3.3**

## Fix Implementation

### Changes Required

**Arquivo 1**: `frontend/src/pages/GameDetailPage.tsx`

**Mudanças**:

1. **Adicionar constante `PLAYER_MARKETS`**: Definir o conjunto de tipos de mercado de jogador.
   ```tsx
   const PLAYER_MARKETS = ['TopKills', 'MostDeaths', 'MostUtilityDamage']
   ```

2. **Adicionar interface `GamePlayer`**: Tipo para os dados retornados pelo novo endpoint.
   ```tsx
   interface GamePlayer { nickname: string; teamName: string }
   ```

3. **Adicionar prop `players` ao `BetForm`**: Receber a lista de jogadores do jogo como prop.
   ```tsx
   function BetForm({ market, game, players }: { market: Market; game: Game; players: GamePlayer[] })
   ```

4. **Substituir o branch `else` no `BetForm`**: Trocar `<input type="text">` por `<select>` quando `isPlayerMarket`.
   ```tsx
   const isPlayerMarket = PLAYER_MARKETS.includes(market.type)
   // ...
   {isTeamMarket ? (
     <select>...</select>  // existente
   ) : isPlayerMarket ? (
     <select id={`option-${market.id}`} value={option} onChange={e => setOption(e.target.value)} required>
       <option value="">Selecione</option>
       {players.map(p => (
         <option key={`${p.nickname}-${p.teamName}`} value={p.nickname}>
           {p.nickname} - {p.teamName}
         </option>
       ))}
     </select>
   ) : (
     <input type="text" ... />  // fallback para outros tipos futuros
   )}
   ```

5. **Buscar jogadores em `GameDetailPage`**: Adicionar `useEffect` para chamar o novo endpoint após carregar o jogo.
   ```tsx
   const [players, setPlayers] = useState<GamePlayer[]>([])
   useEffect(() => {
     if (!id) return
     apiClient.get<GamePlayer[]>(`/games/${id}/players`)
       .then(res => setPlayers(res.data))
       .catch(() => setPlayers([]))
   }, [id])
   ```

6. **Passar `players` para `BetForm`**: Atualizar o JSX que renderiza `BetForm`.
   ```tsx
   <BetForm market={market} game={game} players={players} />
   ```

**Arquivo 2**: `src/FrogBets.Api/Controllers/GamesController.cs`

**Mudanças**:

1. **Adicionar endpoint `GET /api/games/{id}/players`**: Retorna jogadores dos times do jogo, acessível a qualquer usuário autenticado (não apenas admin).
   ```csharp
   [HttpGet("{id:guid}/players")]
   [Authorize]
   public async Task<IActionResult> GetGamePlayers(Guid id)
   {
       var game = await _db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
       if (game is null)
           return NotFound(new { error = new { code = "GAME_NOT_FOUND", message = "Jogo não encontrado." } });

       var players = await _db.CS2Players
           .Include(p => p.Team)
           .AsNoTracking()
           .Where(p => p.Team.Name == game.TeamA || p.Team.Name == game.TeamB)
           .OrderBy(p => p.Team.Name).ThenBy(p => p.Nickname)
           .Select(p => new { nickname = p.Nickname, teamName = p.Team.Name })
           .ToListAsync();

       return Ok(players);
   }
   ```

**Arquivo 3**: `frontend/src/api/players.ts`

**Mudanças**:

1. **Adicionar função `getGamePlayers`**: Encapsular a chamada ao novo endpoint.
   ```ts
   export interface GamePlayer { nickname: string; teamName: string }

   export const getGamePlayers = (gameId: string): Promise<GamePlayer[]> =>
     apiClient.get<GamePlayer[]>(`/games/${gameId}/players`).then(r => r.data)
   ```

## Testing Strategy

### Validation Approach

A estratégia segue duas fases: primeiro, confirmar o bug no código não corrigido com testes exploratórios; depois, verificar que o fix funciona (Property 1) e que o comportamento existente é preservado (Property 2).

### Exploratory Bug Condition Checking

**Goal**: Confirmar que `BetForm` renderiza `<input>` em vez de `<select>` para mercados de jogador no código atual.

**Test Plan**: Renderizar `BetForm` com `market.type = 'TopKills'` e verificar que o elemento renderizado é `<input type="text">`. Esses testes devem falhar no código não corrigido (confirmando o bug) e passar após o fix.

**Test Cases**:
1. **TopKills renderiza input**: Renderizar `BetForm` com `TopKills` → assert que existe `input[type="text"]` (falha após fix)
2. **MostDeaths renderiza input**: Idem para `MostDeaths`
3. **MostUtilityDamage renderiza input**: Idem para `MostUtilityDamage`
4. **Dropdown vazio quando sem jogadores**: Renderizar com `players=[]` → assert que `<select>` existe mas sem opções de jogador

**Expected Counterexamples**:
- `BetForm` com `TopKills` renderiza `<input type="text">` em vez de `<select>`
- Causa confirmada: branch `else` incondicionalmente renderiza input de texto

### Fix Checking

**Goal**: Verificar que para todos os mercados de jogador, o `BetForm` corrigido renderiza `<select>` com os jogadores corretos.

**Pseudocode:**
```
FOR ALL market WHERE isBugCondition(market) DO
  result := render BetForm_fixed(market, game, players)
  ASSERT result contains <select> with options matching players
  ASSERT each option value = player.nickname
  ASSERT each option label = "player.nickname - player.teamName"
END FOR
```

### Preservation Checking

**Goal**: Verificar que para mercados de time, o comportamento é idêntico ao original.

**Pseudocode:**
```
FOR ALL market WHERE NOT isBugCondition(market) AND market.type IN TEAM_MARKETS DO
  ASSERT BetForm_original(market, game) = BetForm_fixed(market, game, players=[])
END FOR
```

**Testing Approach**: Property-based testing é recomendado para preservation checking porque:
- Gera muitos casos de teste automaticamente com diferentes combinações de times
- Garante que nenhuma combinação de `teamA`/`teamB` quebra o dropdown de times
- Cobre edge cases como nomes de times com caracteres especiais

**Test Cases**:
1. **MapWinner preservado**: `BetForm` com `MapWinner` continua renderizando `<select>` com `teamA` e `teamB`
2. **SeriesWinner preservado**: Idem para `SeriesWinner`
3. **creatorOption enviado corretamente**: Ao selecionar jogador e submeter, `POST /api/bets` recebe o nickname no campo `creatorOption`
4. **Dropdown vazio não trava**: `players=[]` → `<select>` renderiza sem crash

### Unit Tests

- Renderizar `BetForm` com cada tipo de mercado de jogador e verificar presença de `<select>`
- Verificar que as opções do dropdown têm `value=nickname` e `label="nickname - teamName"`
- Verificar que `BetForm` com mercado de time não é afetado pela prop `players`
- Testar o endpoint `GET /api/games/{id}/players` com jogo existente e times com jogadores

### Property-Based Tests

- Gerar listas aleatórias de jogadores com teamName ∈ {teamA, teamB} e verificar que todos aparecem no dropdown
- Gerar mercados aleatórios de time e verificar que o dropdown de times é preservado independente da lista de jogadores
- Verificar que o valor submetido (`creatorOption`) é sempre um dos nicknames da lista de jogadores

### Integration Tests

- Fluxo completo: carregar `GameDetailPage` → verificar que jogadores são buscados → selecionar jogador → submeter aposta
- Verificar que mercados de time e de jogador coexistem na mesma página sem interferência
- Testar com jogo sem jogadores cadastrados: página carrega, dropdown vazio, sem erro

# Design Document — Admin Usability Improvements

## Overview

Esta feature melhora a usabilidade do `AdminPage.tsx` substituindo campos de texto livre (UUID e nickname) por dropdowns com dados reais da API. As mudanças são exclusivamente no frontend — nenhum endpoint novo é necessário.

Os três endpoints já existentes que serão consumidos:
- `GET /api/players` — lista todos os CS2Players (requer auth admin)
- `GET /api/users` — lista todos os usuários (requer auth admin)
- `GET /api/teams` — já consumido, sem mudança

## Architecture

A mudança segue o padrão já estabelecido no `AdminPage.tsx`: dados compartilhados são carregados no componente pai (`AdminPage`) e repassados como props para os filhos. Isso evita requisições redundantes.

```
AdminPage (root)
├── carrega users[] uma vez → repassa para LeaderManagementSection e DirectSwapSection
├── PlayersSection
│   └── carrega players[] internamente (escopo local, recarrega após cadastro)
├── LeaderManagementSection (recebe users[] como prop)
│   ├── Designar Líder — User_Dropdown (assignUserId)
│   └── Mover Usuário de Time — User_Dropdown (moveUserId)
└── DirectSwapSection (recebe users[] como prop)
    ├── User_Dropdown A (userAId)
    └── User_Dropdown B (userBId)
```

O fluxo de dados de `users[]` já existe parcialmente: `AdminPage` já chama `GET /api/users` e armazena em `users`. A mudança é repassar esse estado para `LeaderManagementSection` e `DirectSwapSection` como prop.

## Components and Interfaces

### Mudanças em `AdminPage` (componente raiz)

Adicionar `users` como prop de `LeaderManagementSection` e `DirectSwapSection`:

```tsx
<LeaderManagementSection teams={teams} users={users} />
<DirectSwapSection users={users} />
```

### `PlayersSection` — Player Dropdown

Adicionar estado `players: CS2Player[]` carregado via `GET /api/players` no `useEffect` inicial. Substituir o `<input type="text">` de nickname por um `<select>`:

```tsx
// Estado adicional
const [players, setPlayers] = useState<CS2Player[]>([])
const [playersError, setPlayersError] = useState<string | null>(null)

// No useEffect
getPlayers()
  .then(setPlayers)
  .catch(() => setPlayersError('Erro ao carregar jogadores.'))

// No formulário — substituir o input de nickname por:
<select id="playerNickname" value={nickname} onChange={e => setNickname(e.target.value)} required>
  <option value="">Selecione um jogador</option>
  {players.map(p => (
    <option key={p.id} value={p.nickname} disabled={!!p.teamId}>
      {p.teamId ? `${p.nickname} (${p.teamName})` : p.nickname}
    </option>
  ))}
</select>
{playersError && <p role="alert">{playersError}</p>}
```

Após cadastro bem-sucedido, `loadPlayers()` é chamado para atualizar o dropdown.

### `LeaderManagementSection` — User Dropdowns

Adicionar `users: User[]` à interface de props. Substituir os dois `<input type="text">` de UUID por `<select>`:

```tsx
// Designar Líder — substituir input de assignUserId
<select id="assignLeaderUserId" value={assignUserId} onChange={e => setAssignUserId(e.target.value)} required>
  <option value="">Selecione um usuário</option>
  {users.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
</select>

// Mover Usuário de Time — substituir input de moveUserId
<select id="moveUserId" value={moveUserId} onChange={e => setMoveUserId(e.target.value)} required>
  <option value="">Selecione um usuário</option>
  {users.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
</select>
```

### `DirectSwapSection` — User Dropdowns

Adicionar `users: User[]` à interface de props. Substituir os dois `<input type="text">` por `<select>`. Adicionar validação de usuários iguais:

```tsx
// Validação antes do submit
if (userAId === userBId) {
  setError('Os dois usuários devem ser diferentes.')
  return
}

// Substituir inputs por selects
<select id="swapUserAId" value={userAId} onChange={e => setUserAId(e.target.value)} required>
  <option value="">Selecione o Usuário A</option>
  {users.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
</select>

<select id="swapUserBId" value={userBId} onChange={e => setUserBId(e.target.value)} required>
  <option value="">Selecione o Usuário B</option>
  {users.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
</select>
```

## Data Models

Nenhuma mudança de modelo de dados. Os tipos já existentes são suficientes:

```typescript
// Já definido em AdminPage.tsx
interface User { id: string; username: string; isAdmin: boolean; isMasterAdmin?: boolean }

// Já definido em frontend/src/api/players.ts
interface CS2Player {
  id: string; nickname: string; realName?: string;
  teamId: string; teamName: string; photoUrl?: string;
  playerScore: number; matchesCount: number; createdAt: string;
}
```

O campo `teamId` de `CS2Player` é usado para determinar se o jogador é `Allocated_Player` (teamId não vazio) ou `Unallocated_Player` (teamId vazio/nulo). O campo `teamName` é usado para exibir o sufixo `(Nome do Time)` na opção desabilitada.

**Nota:** A API retorna `teamId` como string vazia `""` para jogadores sem time, não como `null`. A verificação deve ser `!!p.teamId` (falsy para string vazia).

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Estado de alocação determina completamente a renderização da opção no Player_Dropdown

*Para qualquer* lista de CS2Players, cada opção no Player_Dropdown deve ter o atributo `disabled` e conter o sufixo `(teamName)` se e somente se o jogador possui `teamId` não vazio; e deve estar habilitada e sem sufixo se e somente se o `teamId` for vazio.

**Validates: Requirements 1.2, 1.3, 1.4**

### Property 2: User Dropdown exibe username e carrega UUID correto

*Para qualquer* lista de usuários, cada opção do User_Dropdown exibe o `username` como texto visível e o `id` (UUID) como valor, de forma que selecionar um usuário pelo nome resulta no UUID correto sendo enviado à API.

**Validates: Requirements 2.2, 2.3, 3.2, 3.3, 4.2, 4.3**

### Property 3: Validação de usuários iguais na Troca Direta

*Para qualquer* UUID de usuário, se Usuário A e Usuário B forem definidos com o mesmo valor, o formulário de Troca Direta deve rejeitar a submissão e exibir mensagem de erro, sem chamar a API.

**Validates: Requirements 4.4**

## Error Handling

| Cenário | Comportamento |
|---|---|
| `GET /api/players` falha | Exibe `<p role="alert">Erro ao carregar jogadores.</p>` acima do formulário; o select não é renderizado (ou fica vazio) |
| `GET /api/users` falha | `users` permanece `[]`; os selects ficam com apenas a opção placeholder; nenhuma mensagem de erro adicional (já existe tratamento de erro genérico no `AdminPage`) |
| Usuário A = Usuário B na Troca Direta | Validação client-side antes do submit; exibe `<p role="alert">` sem chamar a API |
| `POST /api/players` falha | Comportamento existente mantido (já há tratamento de erro) |
| Logo de time com URL inválida | `onError` no `<img>` oculta o elemento via `style.display = 'none'` |

O carregamento de `users` no `AdminPage` já tem tratamento silencioso (`.catch(() => {})`). Para `players`, o erro é exibido na seção de Jogadores pois é um dado crítico para o formulário daquela seção.

---

## Novos Componentes e Mudanças (Requirements 6–11)

### Requirement 6: Convites em Massa com Descrição Individual

O fluxo atual só permite `description` quando `quantity === 1`. O novo fluxo gera um campo de descrição por convite quando `quantity > 1`.

**Mudança de estado em `InvitesSection`:**

```tsx
// Substituir o único campo `description` por um array
const [descriptions, setDescriptions] = useState<string[]>([''])

// Quando quantity muda, redimensionar o array
useEffect(() => {
  setDescriptions(prev => {
    const next = Array(quantity).fill('')
    prev.forEach((v, i) => { if (i < quantity) next[i] = v })
    return next
  })
}, [quantity])
```

**No formulário — substituir o bloco `{quantity === 1 && ...}` por:**

```tsx
<div className="form-group">
  <label>Destinatário(s) (opcional):</label>
  {descriptions.map((desc, i) => (
    <input
      key={i}
      type="text"
      placeholder={quantity > 1 ? `Convite ${i + 1}` : 'Nome ou identificação'}
      value={desc}
      onChange={e => setDescriptions(d => d.map((v, j) => j === i ? e.target.value : v))}
      style={{ marginBottom: '.4rem' }}
    />
  ))}
</div>
```

**No submit — enviar cada convite individualmente com sua descrição:**

```tsx
// Ao invés de um único POST com quantity, enviar N POSTs paralelos
const results = await Promise.all(
  descriptions.map(desc =>
    apiClient.post<{ tokens: string[] }>('/invites', {
      quantity: 1,
      description: desc || null,
    })
  )
)
setNewTokens(results.flatMap(r => r.data.tokens))
```

> **Nota:** Se o backend suportar `description` em geração em massa (array), preferir essa abordagem. Caso contrário, N POSTs paralelos é a solução correta.

### Requirement 7: Nome do Time nos Dropdowns de Jogadores

Padronizar o formato de exibição em todos os dropdowns de `CS2Player`:

```tsx
// Helper reutilizável
function playerLabel(p: CS2Player): string {
  return p.teamId ? `${p.nickname} - ${p.teamName}` : p.nickname
}

// Uso em PlayersSection (Player_Dropdown)
<option key={p.id} value={p.nickname} disabled={!!p.teamId}>
  {playerLabel(p)}
</option>

// Uso em MatchStatsSection (statsPlayerSelect)
<option key={p.id} value={p.id}>{playerLabel(p)}</option>
```

A função `playerLabel` deve ser definida no escopo do módulo (fora dos componentes) para ser reutilizável.

### Requirement 8: Filtro por Time no Dropdown "Designar Líder"

Adicionar estado `assignFilterTeamId` separado do `assignTeamId` (que já é o time destino do líder). Como o time selecionado para designar líder já é o `assignTeamId`, o filtro de usuários usa diretamente esse valor:

```tsx
// Usuários filtrados para o dropdown de Designar Líder
const assignEligibleUsers = assignTeamId
  ? users.filter(u => (u as unknown as { teamId: string | null }).teamId === assignTeamId)
  : []

// No select de usuário
<select id="assignLeaderUserId" value={assignUserId}
  onChange={e => setAssignUserId(e.target.value)}
  disabled={!assignTeamId} required>
  <option value="">
    {assignTeamId ? 'Selecione um usuário' : 'Selecione um time primeiro'}
  </option>
  {assignEligibleUsers.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
</select>
```

Quando `assignTeamId` muda, resetar `assignUserId`:

```tsx
// No onChange do select de time
onChange={e => { setAssignTeamId(e.target.value); setAssignUserId('') }}
```

**Nota:** O tipo `User` precisa ser estendido para incluir `teamId`:

```tsx
interface User {
  id: string; username: string; isAdmin: boolean; isMasterAdmin?: boolean
  teamId?: string | null  // adicionar este campo
}
```

### Requirement 9: Seleção de Time de Origem em "Mover Usuário de Time"

Adicionar estado `moveSourceTeamId` para o time de origem:

```tsx
const [moveSourceTeamId, setMoveSourceTeamId] = useState('')

// Usuários filtrados pelo time de origem
const moveEligibleUsers = moveSourceTeamId
  ? users.filter(u => (u as unknown as { teamId: string | null }).teamId === moveSourceTeamId)
  : []
```

**No formulário — adicionar dropdown de time de origem antes do dropdown de usuário:**

```tsx
<div className="form-group">
  <label htmlFor="moveSourceTeam">Time de Origem:</label>
  <select id="moveSourceTeam" value={moveSourceTeamId}
    onChange={e => { setMoveSourceTeamId(e.target.value); setMoveUserId('') }} required>
    <option value="">Selecione o time de origem</option>
    {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
  </select>
</div>
<div className="form-group">
  <label htmlFor="moveUserId">Usuário:</label>
  <select id="moveUserId" value={moveUserId}
    onChange={e => setMoveUserId(e.target.value)}
    disabled={!moveSourceTeamId} required>
    <option value="">
      {moveSourceTeamId ? 'Selecione um usuário' : 'Selecione o time de origem primeiro'}
    </option>
    {moveEligibleUsers.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
  </select>
</div>
```

### Requirement 10: Fluxo em Duas Etapas na Troca Direta

Adicionar estados de time para cada lado da troca:

```tsx
const [teamAId, setTeamAId] = useState('')
const [teamBId, setTeamBId] = useState('')
// userAId e userBId já existem

// Usuários filtrados por time
const usersTeamA = teamAId ? users.filter(u => (u as unknown as { teamId: string | null }).teamId === teamAId) : []
const usersTeamB = teamBId ? users.filter(u => (u as unknown as { teamId: string | null }).teamId === teamBId) : []
```

**No formulário — dois grupos independentes:**

```tsx
{/* Grupo A */}
<div className="form-group">
  <label htmlFor="swapTeamA">Time A:</label>
  <select id="swapTeamA" value={teamAId}
    onChange={e => { setTeamAId(e.target.value); setUserAId('') }} required>
    <option value="">Selecione o Time A</option>
    {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
  </select>
</div>
<div className="form-group">
  <label htmlFor="swapUserAId">Jogador A:</label>
  <select id="swapUserAId" value={userAId}
    onChange={e => setUserAId(e.target.value)}
    disabled={!teamAId} required>
    <option value="">{teamAId ? 'Selecione o Jogador A' : 'Selecione o Time A primeiro'}</option>
    {usersTeamA.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
  </select>
</div>

{/* Grupo B */}
<div className="form-group">
  <label htmlFor="swapTeamB">Time B:</label>
  <select id="swapTeamB" value={teamBId}
    onChange={e => { setTeamBId(e.target.value); setUserBId('') }} required>
    <option value="">Selecione o Time B</option>
    {teams.filter(t => t.id !== teamAId).map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
  </select>
</div>
<div className="form-group">
  <label htmlFor="swapUserBId">Jogador B:</label>
  <select id="swapUserBId" value={userBId}
    onChange={e => setUserBId(e.target.value)}
    disabled={!teamBId} required>
    <option value="">{teamBId ? 'Selecione o Jogador B' : 'Selecione o Time B primeiro'}</option>
    {usersTeamB.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
  </select>
</div>
```

`DirectSwapSection` precisa receber `teams` como prop adicional:

```tsx
function DirectSwapSection({ users, teams }: { users: User[]; teams: CS2Team[] })
```

### Requirement 11: Correção do Carregamento de Logo dos Times

O `TeamsSection` já possui o handler `onError` correto:

```tsx
onError={e => { (e.currentTarget as HTMLImageElement).style.display = 'none' }}
```

A investigação deve focar em:
1. Verificar se `team.logoUrl` está sendo retornado corretamente pelo `GET /api/teams`
2. Verificar se há problema de CORS ou URL relativa vs absoluta
3. Verificar se o campo `logoUrl` na entidade `CS2Team` do frontend corresponde ao campo retornado pela API

```typescript
// Verificar o tipo CS2Team em frontend/src/api/players.ts
interface CS2Team {
  id: string; name: string; logoUrl?: string; createdAt: string
}
```

Se `logoUrl` vier como `null` da API mas o tipo espera `undefined`, a condição `{team.logoUrl ? <img...> : '—'}` pode falhar silenciosamente. Garantir que a verificação seja `{team.logoUrl != null && team.logoUrl !== '' ? <img...> : '—'}`.

## Updated Architecture

```
AdminPage (root)
├── carrega users[] (com teamId) → repassa para LeaderManagementSection e DirectSwapSection
├── carrega teams[] → repassa para LeaderManagementSection e DirectSwapSection (novo)
├── InvitesSection
│   └── descriptions[] por convite (novo)
├── TeamsSection
│   └── logoUrl fix (novo)
├── PlayersSection
│   └── playerLabel() com "nickname - teamName" (novo)
├── MatchStatsSection
│   └── playerLabel() com "nickname - teamName" (novo)
├── LeaderManagementSection (recebe users[] e teams[] como props)
│   ├── Designar Líder — filtro por assignTeamId (novo)
│   └── Mover Usuário de Time — moveSourceTeamId + filtro (novo)
└── DirectSwapSection (recebe users[] e teams[] como props — novo)
    ├── teamAId → usersTeamA → userAId
    └── teamBId → usersTeamB → userBId
```

## Updated Data Models

```typescript
// User — adicionar teamId para permitir filtragem client-side
interface User {
  id: string; username: string; isAdmin: boolean; isMasterAdmin?: boolean
  teamId?: string | null  // novo campo necessário para filtros
}
```

O endpoint `GET /api/users` já retorna `teamId` (visível na tabela de usuários do `UsersSection`). Apenas o tipo TypeScript precisa ser atualizado.

## Updated Correctness Properties

### Property 4: playerLabel é consistente com o estado de alocação

*Para qualquer* CS2Player, `playerLabel(p)` retorna `"nickname - teamName"` se `p.teamId` não é vazio, e `"nickname"` caso contrário. A função é pura e determinística.

**Validates: Requirements 7.1, 7.2**

### Property 5: Filtro de usuários por time é subconjunto correto

*Para qualquer* lista de usuários e qualquer teamId selecionado, os usuários filtrados são exatamente aqueles cujo `teamId` é igual ao teamId selecionado — nem mais, nem menos.

**Validates: Requirements 8.1, 9.2, 10.2, 10.3**

## Testing Strategy

Esta feature é exclusivamente de UI/UX — substituição de inputs por selects com dados da API. PBT não é a ferramenta mais adequada para a maioria dos critérios (renderização de componentes, interações de formulário). A estratégia usa testes de componente com mocks.

**Testes de componente (Vitest + React Testing Library):**

- `PlayersSection`: renderiza select com jogadores; opções de alocados têm `disabled` e sufixo de time; opções de não-alocados estão habilitadas; erro de carregamento exibe mensagem.
- `LeaderManagementSection`: campos de Designar Líder e Mover Usuário renderizam selects com usernames; selecionar usuário usa o UUID correto no submit.
- `DirectSwapSection`: dois selects com usernames; submissão com mesmo usuário nos dois campos exibe erro e não chama a API.

**Testes de property (Vitest + fast-check) — para as propriedades 1, 2, 3 e 4:**

A biblioteca escolhida é **fast-check** (já compatível com o ecossistema Vite/Vitest do projeto).

- Property 1 & 2: gerar listas arbitrárias de `CS2Player` (com e sem `teamId`), renderizar `PlayersSection`, verificar que `disabled` e sufixo correspondem exatamente ao estado de alocação.
- Property 3: gerar listas arbitrárias de `User`, renderizar os dropdowns, verificar que o `value` de cada `<option>` é o UUID e o texto visível é o `username`.
- Property 4: gerar pares de UUIDs onde ambos são iguais, tentar submeter `DirectSwapSection`, verificar que a API não é chamada e o erro é exibido.

Cada property test deve rodar mínimo 100 iterações e ser tagueado com:
`// Feature: admin-usability-improvements, Property N: <descrição>`

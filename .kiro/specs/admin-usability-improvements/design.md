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

O carregamento de `users` no `AdminPage` já tem tratamento silencioso (`.catch(() => {})`). Para `players`, o erro é exibido na seção de Jogadores pois é um dado crítico para o formulário daquela seção.

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

# Implementation Plan: Admin Usability Improvements

## Overview

Substituir campos de texto livre (UUID e nickname) por dropdowns com dados reais da API em `AdminPage.tsx`. Mudanças exclusivamente no frontend — sem novos endpoints.

## Tasks

- [x] 1. Propagar `users[]` como prop para `LeaderManagementSection` e `DirectSwapSection`
  - Em `AdminPage` (componente raiz), adicionar `users` como prop nas chamadas de `<LeaderManagementSection>` e `<DirectSwapSection>`
  - Atualizar as interfaces de props de ambos os componentes para aceitar `users: User[]`
  - _Requirements: 5.1, 5.2_

- [x] 2. Implementar Player Dropdown na `PlayersSection`
  - [x] 2.1 Adicionar carregamento de `players[]` e substituir o `<input>` de nickname por `<select>`
    - Adicionar estado `players: CS2Player[]` e `playersError: string | null`
    - Chamar `getPlayers()` no `useEffect` inicial, populando o estado
    - Substituir o `<input type="text">` de nickname pelo `<select id="playerNickname">` com opções mapeadas de `players`
    - Opções de `Allocated_Player` (teamId não vazio): atributo `disabled` e sufixo `(teamName)`
    - Opções de `Unallocated_Player` (teamId vazio): habilitadas, sem sufixo
    - Exibir `<p role="alert">` com mensagem de erro se `getPlayers()` falhar
    - Após cadastro bem-sucedido, chamar `loadPlayers()` para atualizar o dropdown
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7_

  - [ ]* 2.2 Escrever property test para o Player Dropdown (Property 1)
    - **Property 1: Estado de alocação determina completamente a renderização da opção no Player_Dropdown**
    - Gerar listas arbitrárias de `CS2Player` com e sem `teamId` usando fast-check
    - Verificar que cada opção tem `disabled` e sufixo `(teamName)` se e somente se `teamId` não é vazio
    - Verificar que opções sem `teamId` estão habilitadas e sem sufixo
    - Mínimo 100 iterações; taguear com `// Feature: admin-usability-improvements, Property 1`
    - **Validates: Requirements 1.2, 1.3, 1.4**

- [x] 3. Implementar User Dropdowns na `LeaderManagementSection`
  - [x] 3.1 Substituir inputs de UUID por `<select>` nos formulários "Designar Líder" e "Mover Usuário de Time"
    - Substituir `<input id="assignLeaderUserId">` por `<select>` com opções de `users.map(u => <option key={u.id} value={u.id}>{u.username}</option>)`
    - Substituir `<input id="moveUserId">` por `<select>` com o mesmo padrão
    - O valor submetido deve ser o `id` (UUID) do usuário selecionado
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 3.3_

  - [ ]* 3.2 Escrever property test para User Dropdowns (Property 2)
    - **Property 2: User Dropdown exibe username e carrega UUID correto**
    - Gerar listas arbitrárias de `User`, renderizar os dropdowns de `LeaderManagementSection`
    - Verificar que o texto visível de cada `<option>` é o `username` e o `value` é o `id` (UUID)
    - Mínimo 100 iterações; taguear com `// Feature: admin-usability-improvements, Property 2`
    - **Validates: Requirements 2.2, 2.3, 3.2, 3.3**

- [x] 4. Checkpoint — Garantir que os testes passam
  - Garantir que todos os testes passam. Perguntar ao usuário se houver dúvidas.

- [x] 5. Implementar User Dropdowns na `DirectSwapSection`
  - [x] 5.1 Substituir inputs de UUID por `<select>` e adicionar validação de usuários iguais
    - Substituir `<input id="swapUserAId">` por `<select>` com opções de `users`
    - Substituir `<input id="swapUserBId">` por `<select>` com opções de `users`
    - Adicionar validação client-side antes do submit: se `userAId === userBId`, exibir `<p role="alert">` e não chamar a API
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [ ]* 5.2 Escrever property test para validação de usuários iguais (Property 3)
    - **Property 3: Validação de usuários iguais na Troca Direta**
    - Gerar pares de UUIDs onde ambos são iguais, tentar submeter `DirectSwapSection`
    - Verificar que a API não é chamada e o erro é exibido
    - Mínimo 100 iterações; taguear com `// Feature: admin-usability-improvements, Property 3`
    - **Validates: Requirements 4.4**

- [x] 6. Checkpoint final — Garantir que todos os testes passam
  - Garantir que todos os testes passam. Perguntar ao usuário se houver dúvidas.

- [x] 7. Corrigir carregamento de logo dos times (Bug — Requirement 11)
  - Investigar por que `team.logoUrl` aparece em branco no painel admin
  - Verificar o tipo `CS2Team` em `frontend/src/api/players.ts` — garantir que `logoUrl` está tipado como `string | null | undefined` e não apenas `string`
  - Atualizar a condição de renderização em `TeamsSection` para `{team.logoUrl != null && team.logoUrl !== '' ? <img...> : '—'}`
  - Verificar se o endpoint `GET /api/teams` retorna o campo `logoUrl` corretamente (inspecionar resposta da API)
  - _Requirements: 11.1, 11.2, 11.3_

- [x] 8. Adicionar campo `teamId` ao tipo `User` no frontend
  - Atualizar a interface `User` em `AdminPage.tsx` para incluir `teamId?: string | null`
  - Verificar que `GET /api/users` já retorna `teamId` (já é exibido na tabela de usuários)
  - Este campo é pré-requisito para os filtros dos Requirements 8, 9 e 10
  - _Requirements: 8.1, 9.2, 10.2, 10.3_

- [x] 9. Implementar `playerLabel` e aplicar em todos os dropdowns de jogadores (Requirement 7)
  - Adicionar função helper `playerLabel(p: CS2Player): string` no escopo do módulo
  - Retorna `"nickname - teamName"` se `p.teamId` não é vazio, senão apenas `p.nickname`
  - Aplicar em `PlayersSection` (Player_Dropdown): substituir texto das opções por `playerLabel(p)`
  - Aplicar em `MatchStatsSection` (statsPlayerSelect): substituir `{p.username ?? p.nickname} — {p.teamName}` por `playerLabel(p)`
  - _Requirements: 7.1, 7.2, 7.3_

- [x] 10. Implementar convites em massa com descrição individual (Requirement 6)
  - Substituir o estado `description: string` por `descriptions: string[]` em `InvitesSection`
  - Adicionar `useEffect` que redimensiona o array `descriptions` quando `quantity` muda
  - Substituir o bloco `{quantity === 1 && <input description>}` por N inputs (um por convite)
  - Atualizar o submit para enviar N POSTs paralelos com `quantity: 1` e `description` individual
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 11. Implementar filtro por time no dropdown "Designar Líder" (Requirement 8)
  - Calcular `assignEligibleUsers` filtrando `users` pelo `teamId === assignTeamId`
  - Desabilitar o select de usuário enquanto nenhum time estiver selecionado
  - Resetar `assignUserId` ao mudar `assignTeamId`
  - _Requirements: 8.1, 8.2, 8.3_

- [x] 12. Implementar seleção de time de origem em "Mover Usuário de Time" (Requirement 9)
  - Adicionar estado `moveSourceTeamId` em `LeaderManagementSection`
  - Adicionar dropdown "Time de Origem" antes do dropdown de usuário no formulário "Mover Usuário de Time"
  - Calcular `moveEligibleUsers` filtrando `users` pelo `teamId === moveSourceTeamId`
  - Desabilitar o select de usuário enquanto nenhum time de origem estiver selecionado
  - Resetar `moveUserId` ao mudar `moveSourceTeamId`
  - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [x] 13. Implementar fluxo em duas etapas na Troca Direta (Requirement 10)
  - Adicionar `teams: CS2Team[]` como prop em `DirectSwapSection`
  - Atualizar a chamada em `AdminPage` para `<DirectSwapSection users={users} teams={teams} />`
  - Adicionar estados `teamAId` e `teamBId` em `DirectSwapSection`
  - Calcular `usersTeamA` e `usersTeamB` filtrando `users` pelos respectivos teamIds
  - Substituir os dois selects de usuário por dois grupos (Time + Usuário) independentes
  - Filtrar o dropdown de Time B para excluir o Time A já selecionado
  - Resetar seleção de usuário ao mudar o time correspondente
  - Manter a validação existente de usuários iguais
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6_

- [x] 14. Checkpoint final — Garantir que todos os testes passam após novos itens
  - Garantir que todos os testes passam. Perguntar ao usuário se houver dúvidas.

## Notes

- Tasks marcadas com `*` são opcionais e podem ser puladas para MVP mais rápido
- O design usa TypeScript (React + Vitest + fast-check)
- Nenhum endpoint novo é necessário — apenas consumo de `GET /api/players` e `GET /api/users` já existentes
- A verificação de alocação de jogador deve usar `!!p.teamId` (falsy para string vazia, não null)
- `users[]` já é carregado no `AdminPage` raiz — apenas repassar como prop, sem nova requisição

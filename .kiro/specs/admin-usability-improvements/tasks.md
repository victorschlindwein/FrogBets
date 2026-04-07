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

## Notes

- Tasks marcadas com `*` são opcionais e podem ser puladas para MVP mais rápido
- O design usa TypeScript (React + Vitest + fast-check)
- Nenhum endpoint novo é necessário — apenas consumo de `GET /api/players` e `GET /api/users` já existentes
- A verificação de alocação de jogador deve usar `!!p.teamId` (falsy para string vazia, não null)
- `users[]` já é carregado no `AdminPage` raiz — apenas repassar como prop, sem nova requisição

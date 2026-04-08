# Implementation Plan: teams-menu

## Overview

Implementação incremental da página `/teams` no frontend FrogBets, com os respectivos endpoints de backend necessários. A ordem segue: backend primeiro (novos endpoints), depois API client, depois UI (página + navbar + rota), e por fim os testes de propriedade.

## Tasks

- [x] 1. Adicionar endpoints de backend em TeamsController.cs
  - [x] 1.1 Implementar `GET /api/teams/{id}/players` acessível por qualquer usuário autenticado
    - Adicionar método `GetPlayersByTeam(Guid teamId)` em `TeamsController.cs`
    - Injetar `IPlayerService` (já disponível no controller) e chamar `GetPlayersByTeamAsync(teamId)`
    - Implementar `GetPlayersByTeamAsync` em `IPlayerService` e `PlayerService` retornando `CS2Player[]` filtrado por `teamId`
    - Retornar 200 com a lista; retornar 404 se o time não existir
    - _Requirements: 3.1_

  - [x] 1.2 Implementar `PUT /api/teams/{id}/logo` restrito ao líder do time
    - Adicionar método `UpdateLogo(Guid teamId, UpdateLogoBody body)` em `TeamsController.cs`
    - Verificar liderança via `IsLeaderOfTeam(teamId)` consultando o banco (não via claim, conforme diretriz de segurança)
    - Implementar `UpdateLogoAsync(teamId, logoUrl)` em `ITeamService` e `TeamService`
    - Retornar 200 com o time atualizado; retornar 403 se não for líder; retornar 404 se time não existir
    - Adicionar record `UpdateLogoBody(string? LogoUrl)` no arquivo do controller
    - _Requirements: 5.3, 5.6_

  - [x] 1.3 Implementar `DELETE /api/teams/{id}/logo` restrito ao líder do time
    - Adicionar método `RemoveLogo(Guid teamId)` em `TeamsController.cs`
    - Reutilizar `IsLeaderOfTeam` e `UpdateLogoAsync(teamId, null)`
    - Retornar 204 No Content; retornar 403 se não for líder; retornar 404 se time não existir
    - _Requirements: 5.4, 5.6_

  - [x] 1.4 Adicionar helper `IsLeaderOfTeam` em TeamsController.cs
    - Consultar `_db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsTeamLeader && u.TeamId == teamId)`
    - Extrair `userId` via `User.FindFirstValue(ClaimTypes.NameIdentifier)`
    - _Requirements: 5.6_

- [x] 2. Checkpoint — testar endpoints de backend
  - Garantir que todos os testes .NET passam: `dotnet test --configuration Release --verbosity quiet`
  - Verificar que `GET /api/teams/{id}/players` retorna 200 para usuário autenticado e 401 para anônimo
  - Verificar que `PUT` e `DELETE` `/api/teams/{id}/logo` retornam 403 para não-líderes

- [x] 3. Adicionar funções de API no frontend (api/players.ts)
  - [x] 3.1 Adicionar `getPlayersByTeam(teamId: string): Promise<CS2Player[]>`
    - Chamar `apiClient.get<CS2Player[]>(`/teams/${teamId}/players`)`
    - _Requirements: 3.1_

  - [x] 3.2 Adicionar `uploadTeamLogo(teamId: string, logoUrl: string): Promise<CS2Team>`
    - Chamar `apiClient.put<CS2Team>(`/teams/${teamId}/logo`, { logoUrl })`
    - _Requirements: 5.3_

  - [x] 3.3 Adicionar `removeTeamLogo(teamId: string): Promise<void>`
    - Chamar `apiClient.delete(`/teams/${teamId}/logo`)`
    - _Requirements: 5.4_

- [x] 4. Criar TeamsPage.tsx com lógica de dados e renderização
  - [x] 4.1 Criar `frontend/src/pages/TeamsPage.tsx` com estado e carregamento de dados
    - Definir interface `State { teams, playersByTeam, loading, error }`
    - Buscar times via `getTeams()` e, em paralelo via `Promise.all`, buscar jogadores de cada time via `getPlayersByTeam(teamId)`
    - Exibir mensagem de carregamento enquanto `loading === true`
    - Exibir `<p role="alert">` com mensagem de erro quando qualquer requisição falha
    - Exibir `<p>Nenhum time cadastrado.</p>` quando a lista de times é vazia
    - Exportar também a função pura `groupPlayersByTeam(players: CS2Player[])` para uso nos testes de propriedade
    - _Requirements: 3.1, 3.2, 3.3, 4.1, 4.2, 4.3_

  - [x] 4.2 Implementar componente `TeamCard` inline em TeamsPage.tsx
    - Props: `{ team: CS2Team, players: CS2Player[], isLeader: boolean, onLogoUpdate: (teamId, logoUrl | null) => void }`
    - Exibir `<img src={team.logoUrl}>` quando `logoUrl` preenchido; placeholder `🐸` quando nulo/vazio
    - Exibir `<h2>` com nome do time
    - Listar jogadores: foto (`<img>` ou placeholder) + nickname
    - Exibir "Nenhum jogador cadastrado." quando `players` é vazio
    - Quando `isLeader === true`: exibir botão "Alterar logo" (input file) e, se há logo, botão "Remover logo"
    - Quando `isLeader === false`: ocultar controles de logo
    - _Requirements: 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 5.1, 5.2, 5.7_

  - [x] 4.3 Implementar lógica de upload e remoção de logo em TeamsPage.tsx
    - Handler de upload: chamar `uploadTeamLogo(teamId, logoUrl)` e atualizar estado com a URL retornada
    - Handler de remoção: chamar `removeTeamLogo(teamId)` e setar `logoUrl` como `null` no estado
    - Em caso de erro em qualquer operação: exibir mensagem de erro descritiva no card; manter logo anterior
    - Detectar se o usuário autenticado é líder de um time via `GET /api/users/me` (campo `teamId` + `isTeamLeader`) ou via dados já disponíveis no contexto
    - _Requirements: 5.3, 5.4, 5.5_

- [x] 5. Registrar rota e adicionar link na Navbar
  - [x] 5.1 Adicionar rota `/teams` em App.tsx dentro do bloco `<ProtectedRoute>`
    - Importar `TeamsPage` e adicionar `<Route path="/teams" element={<TeamsPage />} />`
    - _Requirements: 2.1, 2.2_

  - [x] 5.2 Adicionar link "Times" em Navbar.tsx
    - Inserir `<Link to="/teams" onClick={closeMenu}>Times</Link>` após o link "Ranking CS2" e antes do bloco admin
    - _Requirements: 1.1, 1.2, 1.3_

- [x] 6. Checkpoint — verificar tipos TypeScript
  - Rodar `npx tsc --noEmit` no diretório `frontend` e garantir zero erros de tipo
  - _Requirements: todos_

- [x] 7. Escrever testes de exemplo e de propriedade para TeamsPage
  - [x] 7.1 Criar `frontend/src/pages/TeamsPage.test.tsx` com setup MSW e testes de exemplo
    - Configurar handlers MSW para `GET /api/teams`, `GET /api/teams/{id}/players`, `PUT /api/teams/{id}/logo`, `DELETE /api/teams/{id}/logo`
    - Testar: mensagem de carregamento enquanto APIs estão pendentes
    - Testar: mensagem de erro quando `GET /api/teams` falha
    - Testar: mensagem de erro quando `GET /api/teams/{id}/players` falha
    - Testar: "Nenhum time cadastrado." quando API retorna lista vazia
    - Testar: botões de logo visíveis quando `isLeader === true`
    - Testar: botões de logo ausentes quando `isLeader === false`
    - Testar: logo atualizada no card após upload bem-sucedido
    - Testar: placeholder exibido após remoção de logo bem-sucedida
    - _Requirements: 3.2, 3.3, 4.1, 4.2, 4.3, 5.1, 5.2, 5.5, 5.7_

  - [x]* 7.2 Escrever property test — Property 1: correspondência times/cards
    - Instalar `fast-check` como devDependency: `npm install --save-dev fast-check`
    - **Property 1: número de Team_Cards renderizados = número de times retornados pela API**
    - **Validates: Requirements 3.3, 4.3**
    - Usar `fc.array(arbTeam)` para gerar listas arbitrárias de times (incluindo lista vazia)
    - Mockar `GET /api/teams` com a lista gerada e `GET /api/teams/{id}/players` com `[]`
    - Verificar que `screen.getAllByRole('article').length === teams.length` (ou 0 com mensagem de vazio)

  - [x]* 7.3 Escrever property test — Property 2: nome e logo exibidos corretamente
    - **Property 2: nome do time sempre no DOM; img presente ↔ logoUrl preenchido**
    - **Validates: Requirements 3.4, 3.5, 3.6**
    - Usar `fc.record({ id: fc.uuid(), name: fc.string({ minLength: 1 }), logoUrl: fc.option(fc.webUrl()) })` como `arbTeam`
    - Verificar que o nome aparece no DOM e que `<img>` existe apenas quando `logoUrl` é não-nulo/não-vazio

  - [x]* 7.4 Escrever property test — Property 3: jogadores do time exibidos corretamente
    - **Property 3: nicknames de todos os jogadores no DOM; foto presente ↔ photoUrl preenchido; lista vazia → mensagem**
    - **Validates: Requirements 3.7, 3.8, 3.9**
    - Usar `fc.array(arbPlayer)` para gerar listas arbitrárias de jogadores
    - Verificar nicknames, fotos e mensagem de lista vazia

  - [x]* 7.5 Escrever property test — Property 4: agrupamento de jogadores por time
    - **Property 4: `groupPlayersByTeam` produz mapa correto — cada chave contém exatamente os jogadores com aquele teamId; jogadores sem teamId não aparecem**
    - **Validates: Requirements 3.1, 3.7**
    - Testar a função pura `groupPlayersByTeam` exportada de `TeamsPage.tsx` sem renderização
    - Usar `fc.array(fc.record({ ...arbPlayer, teamId: fc.option(fc.uuid()) }))`

  - [x]* 7.6 Escrever property test — Property 5: controles de logo visíveis apenas para o líder
    - **Property 5: botões de upload/remoção presentes no DOM ↔ `isLeader === true`**
    - **Validates: Requirements 5.1, 5.2, 5.7**
    - Usar `fc.boolean()` para `isLeader` e `arbTeam` para o time
    - Renderizar `TeamCard` diretamente com o prop `isLeader` gerado
    - Verificar presença/ausência dos botões de logo

- [x] 8. Criar teste E2E em Cypress
  - [x]* 8.1 Criar `frontend/cypress/e2e/teams.cy.ts`
    - Testar fluxo completo: login → clicar em "Times" na navbar → verificar que a página carrega com times
    - Testar que `/teams` sem autenticação redireciona para `/login`
    - _Requirements: 1.1, 1.2, 2.1, 2.2_

- [x] 9. Checkpoint final — garantir que todos os testes passam
  - Rodar `dotnet test --configuration Release --verbosity quiet`
  - Rodar `cd frontend && npm run test`
  - Rodar `npx tsc --noEmit` no frontend
  - Garantir zero erros e zero falhas antes de commitar

## Notes

- Tasks marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- Cada task referencia os requisitos específicos para rastreabilidade
- A função `groupPlayersByTeam` deve ser exportada de `TeamsPage.tsx` para permitir testes de propriedade sem renderização
- `IsTeamLeader` nunca deve ser lido do JWT — sempre consultar o banco (diretriz de segurança do projeto)
- O upload de logo recebe uma URL string (não arquivo binário), mantendo consistência com o campo `logoUrl` existente

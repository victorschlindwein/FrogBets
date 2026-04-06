# Plano de Implementação: Team Membership

## Overview

Implementação incremental do sistema de pertencimento a times e marketplace de trocas, seguindo a ordem: domínio → migração EF Core → serviços de membership e trocas → ajustes em serviços e controllers existentes → novos controllers → registro no DI → frontend.

## Tasks

- [x] 1. Adicionar campos de domínio à entidade User e criar entidades TradeListing e TradeOffer
  - Adicionar `TeamId` (Guid?, nullable) e `IsTeamLeader` (bool, default false) à entidade `User` em `src/FrogBets.Domain/Entities/User.cs`
  - Adicionar propriedade de navegação `public CS2Team? Team { get; set; }` à entidade `User`
  - Criar `src/FrogBets.Domain/Entities/TradeListing.cs` com propriedades `Id`, `UserId`, `User`, `TeamId`, `Team`, `CreatedAt`
  - Criar `src/FrogBets.Domain/Entities/TradeOffer.cs` com propriedades `Id`, `OfferedUserId`, `OfferedUser`, `TargetUserId`, `TargetUser`, `ProposerTeamId`, `ProposerTeam`, `ReceiverTeamId`, `ReceiverTeam`, `Status`, `CreatedAt`, `UpdatedAt`
  - Criar enum `TradeOfferStatus { Pending, Accepted, Rejected, Cancelled }` no mesmo arquivo ou em arquivo separado
  - _Requirements: 2.1, 5.1, 6.2_

- [x] 2. Configurar EF Core e gerar migração AddTeamMembership
  - Adicionar `DbSet<TradeListing>` e `DbSet<TradeOffer>` ao `FrogBetsDbContext`
  - Configurar `OnModelCreating`: FK de `User → CS2Team` com `OnDelete(DeleteBehavior.SetNull)`, unique index em `TradeListing.UserId`, conversão de `TradeOffer.Status` para string
  - Gerar migração: `dotnet ef migrations add AddTeamMembership --project src/FrogBets.Infrastructure --startup-project src/FrogBets.Api`
  - _Requirements: 1.2, 1.3, 2.1, 4.1_

- [x] 3. Implementar ITeamMembershipService e TeamMembershipService
  - Criar `src/FrogBets.Api/Services/ITeamMembershipService.cs` com interface contendo `AssignLeaderAsync`, `RemoveLeaderAsync` e `MoveUserAsync` conforme design
  - Criar `src/FrogBets.Api/Services/TeamMembershipService.cs` implementando:
    - `AssignLeaderAsync(teamId, userId)`: verificar existência do time e do usuário, verificar que o usuário pertence ao time (→ `USER_NOT_IN_TEAM`), verificar que o usuário não é líder de outro time (→ `ALREADY_LEADER_OF_OTHER_TEAM`), remover líder atual do time se existir, definir `IsTeamLeader = true`
    - `RemoveLeaderAsync(teamId)`: encontrar líder atual do time, definir `IsTeamLeader = false`
    - `MoveUserAsync(requesterId, requesterIsAdmin, targetUserId, destinationTeamId?)`: verificar autorização (admin ou líder do time de origem), verificar existência do time de destino (→ `TEAM_NOT_FOUND`), se `destinationTeamId` for nulo definir `TeamId = null`, se o usuário movido era líder definir `IsTeamLeader = false` automaticamente
  - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [ ]* 3.1 Escrever property test — Propriedade 4: Invariante de líder único por time
    - Para qualquer sequência de designações de líder para um time, o número de usuários com `IsTeamLeader = true` nesse time deve ser no máximo 1
    - **Property 4: Invariante de líder único por time**
    - **Validates: Requirements 2.2, 2.3, 2.4**

  - [ ]* 3.2 Escrever property test — Propriedade 5: Remoção de líder limpa IsTeamLeader
    - Para qualquer usuário com `IsTeamLeader = true`, após `RemoveLeaderAsync`, `IsTeamLeader` deve ser `false`
    - **Property 5: Remoção de líder limpa IsTeamLeader**
    - **Validates: Requirements 2.3**

  - [ ]* 3.3 Escrever property test — Propriedade 6: Remoção do time limpa IsTeamLeader automaticamente
    - Para qualquer usuário com `IsTeamLeader = true`, após `MoveUserAsync` com `destinationTeamId = null`, `IsTeamLeader` deve ser `false`
    - **Property 6: Remoção do time limpa IsTeamLeader automaticamente**
    - **Validates: Requirements 2.7**

  - [ ]* 3.4 Escrever property test — Propriedade 7: Movimentação de membro atualiza TeamId corretamente
    - Para qualquer usuário membro de um time e qualquer time de destino válido, após `MoveUserAsync` autorizado, `TeamId` deve ser igual ao id do time de destino
    - **Property 7: Movimentação de membro atualiza TeamId corretamente**
    - **Validates: Requirements 3.1, 3.2**

  - [ ]* 3.5 Escrever property test — Propriedade 8: Remoção de time pelo admin define TeamId como nulo
    - Para qualquer usuário com `TeamId` preenchido, após `MoveUserAsync` com `destinationTeamId = null` pelo admin, `TeamId` deve ser `null`
    - **Property 8: Remoção de time pelo admin define TeamId como nulo**
    - **Validates: Requirements 3.3**

- [x] 4. Implementar ITradeService e TradeService
  - Criar `src/FrogBets.Api/Services/ITradeService.cs` com interface contendo `GetListingsAsync`, `AddListingAsync`, `RemoveListingAsync`, `GetReceivedOffersAsync`, `CreateOfferAsync`, `AcceptOfferAsync`, `RejectOfferAsync` e `DirectSwapAsync` conforme design
  - Criar `src/FrogBets.Api/Services/TradeService.cs` implementando:
    - `AddListingAsync`: verificar que o solicitante é líder do time do membro alvo (→ `FORBIDDEN`), verificar que o membro não está já listado (→ `ALREADY_LISTED`), persistir `TradeListing`
    - `RemoveListingAsync`: verificar autorização, remover `TradeListing`
    - `GetListingsAsync`: retornar todos os `TradeListing` com dados de usuário e time
    - `GetReceivedOffersAsync`: retornar ofertas com `ReceiverTeamId` igual ao time do solicitante e status `Pending`
    - `CreateOfferAsync`: validar que membro ofertado pertence ao time do líder (→ `FORBIDDEN`), validar que membro alvo está disponível (→ `TARGET_NOT_AVAILABLE`), validar que não são do mesmo time (→ `SAME_TEAM_TRADE`), persistir `TradeOffer` com status `Pending`
    - `AcceptOfferAsync`: verificar que o membro alvo pertence ao time do solicitante (→ `FORBIDDEN`), verificar status `Pending` (→ `OFFER_NOT_PENDING`), trocar `TeamId`s, definir status `Accepted`, remover `TradeListing` dos dois membros, cancelar outras ofertas pendentes envolvendo qualquer um dos dois membros
    - `RejectOfferAsync`: verificar autorização e status, definir status `Rejected`
    - `DirectSwapAsync`: verificar `IsAdmin`, verificar que os membros são de times distintos (→ `SAME_TEAM_TRADE`), trocar `TeamId`s, remover `TradeListing` dos dois, cancelar ofertas pendentes envolvendo qualquer um dos dois
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 7.1, 7.2, 7.3, 7.4, 7.5_

  - [ ]* 4.1 Escrever property test — Propriedade 9: Marcação de disponibilidade é refletida na listagem
    - Para qualquer membro do time do líder, após `AddListingAsync`, o membro deve aparecer em `GetListingsAsync`
    - **Property 9: Marcação de disponibilidade é refletida na listagem**
    - **Validates: Requirements 4.1**

  - [ ]* 4.2 Escrever property test — Propriedade 10: Remoção de disponibilidade remove da listagem
    - Para qualquer membro marcado como disponível, após `RemoveListingAsync`, o membro não deve aparecer em `GetListingsAsync`
    - **Property 10: Remoção de disponibilidade remove da listagem**
    - **Validates: Requirements 4.2**

  - [ ]* 4.3 Escrever property test — Propriedade 11: Transferência de time remove disponibilidade automaticamente
    - Para qualquer membro marcado como disponível, após transferência de time, o membro não deve aparecer em `GetListingsAsync`
    - **Property 11: Transferência de time remove disponibilidade automaticamente**
    - **Validates: Requirements 4.6**

  - [ ]* 4.4 Escrever property test — Propriedade 12: Oferta criada tem status Pendente
    - Para qualquer oferta válida criada por `CreateOfferAsync`, o status inicial deve ser `Pending`
    - **Property 12: Oferta criada tem status Pendente**
    - **Validates: Requirements 5.1**

  - [ ]* 4.5 Escrever property test — Propriedade 13: Aceitação de oferta troca os TeamIds e limpa disponibilidades
    - Para qualquer oferta `Pending` aceita por `AcceptOfferAsync`, os `TeamId`s devem ser trocados, status `Accepted` e nenhum dos dois membros deve aparecer na listagem
    - **Property 13: Aceitação de oferta troca os TeamIds e limpa disponibilidades**
    - **Validates: Requirements 6.1, 6.2, 6.3**

  - [ ]* 4.6 Escrever property test — Propriedade 14: Recusa de oferta não altera TeamIds
    - Para qualquer oferta `Pending` recusada por `RejectOfferAsync`, status deve ser `Rejected` e `TeamId`s dos membros devem permanecer inalterados
    - **Property 14: Recusa de oferta não altera TeamIds**
    - **Validates: Requirements 6.4**

  - [ ]* 4.7 Escrever property test — Propriedade 15: Aceitação cancela outras ofertas pendentes dos membros envolvidos
    - Para qualquer conjunto de ofertas pendentes com membros sobrepostos, após `AcceptOfferAsync`, todas as outras ofertas pendentes envolvendo qualquer um dos dois membros devem ter status `Cancelled`
    - **Property 15: Aceitação cancela outras ofertas pendentes dos membros envolvidos**
    - **Validates: Requirements 6.7, 7.2**

  - [ ]* 4.8 Escrever property test — Propriedade 16: Troca direta pelo admin realiza swap de TeamIds
    - Para quaisquer dois membros de times distintos, após `DirectSwapAsync`, cada membro deve estar no time do outro, nenhum deve aparecer na listagem e todas as ofertas pendentes envolvendo qualquer um dos dois devem ser canceladas
    - **Property 16: Troca direta pelo admin realiza swap de TeamIds**
    - **Validates: Requirements 7.1, 7.2, 7.3**

- [x] 5. Ajustar AuthService.RegisterAsync para aceitar teamId opcional
  - Adicionar campo `TeamId?` (Guid?) ao `RegisterRequest` em `src/FrogBets.Api/Services/IAuthService.cs`
  - Em `AuthService.RegisterAsync`: se `teamId` for informado, verificar existência do time (→ `TEAM_NOT_FOUND`), atribuir `user.TeamId = teamId`; se não informado, manter `TeamId = null`
  - _Requirements: 1.2, 1.3, 1.4_

  - [ ]* 5.1 Escrever property test — Propriedade 1: Cadastro com time preserva o TeamId
    - Para qualquer `teamId` válido informado no cadastro, o usuário criado deve ter exatamente esse `TeamId`
    - **Property 1: Cadastro com time preserva o TeamId**
    - **Validates: Requirements 1.3**

  - [ ]* 5.2 Escrever property test — Propriedade 2: Cadastro sem time resulta em TeamId nulo
    - Para qualquer combinação válida de username/password/inviteId sem `teamId`, o usuário criado deve ter `TeamId = null`
    - **Property 2: Cadastro sem time resulta em TeamId nulo**
    - **Validates: Requirements 1.2**

  - [ ]* 5.3 Escrever property test — Propriedade 3: TeamId inválido no cadastro é rejeitado
    - Para qualquer GUID não existente informado como `teamId`, a operação deve ser rejeitada com código `TEAM_NOT_FOUND`
    - **Property 3: TeamId inválido no cadastro é rejeitado**
    - **Validates: Requirements 1.4**

- [x] 6. Ajustar TeamsController.GetTeams para remover restrição isAdmin
  - Em `src/FrogBets.Api/Controllers/TeamsController.cs`, remover a verificação de `isAdmin` do endpoint `GET /api/teams`, mantendo apenas autenticação obrigatória
  - _Requirements: 1.1_

- [x] 7. Adicionar endpoints de gestão de líderes ao TeamsController
  - Adicionar endpoint `POST /api/teams/{teamId}/leader/{userId}` (isAdmin): chamar `TeamMembershipService.AssignLeaderAsync`, mapear erros para HTTP conforme design
  - Adicionar endpoint `DELETE /api/teams/{teamId}/leader` (isAdmin): chamar `TeamMembershipService.RemoveLeaderAsync`, mapear erros para HTTP
  - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6_

- [x] 8. Adicionar endpoint de movimentação de usuário ao UsersController
  - Adicionar endpoint `PATCH /api/users/{id}/team` em `src/FrogBets.Api/Controllers/UsersController.cs`
  - Aceitar body `{ teamId: Guid? }` (nulo para remover do time)
  - Verificar autorização: isAdmin via claim OU IsTeamLeader consultando o banco
  - Chamar `TeamMembershipService.MoveUserAsync`, mapear erros para HTTP
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 9. Criar TradesController com todos os endpoints de trocas
  - Criar `src/FrogBets.Api/Controllers/TradesController.cs` com os endpoints:
    - `GET /api/trades/listings` (qualquer autenticado): chamar `TradeService.GetListingsAsync`
    - `POST /api/trades/listings` (isTeamLeader ou isAdmin): body `{ userId }`, chamar `TradeService.AddListingAsync`
    - `DELETE /api/trades/listings/{userId}` (isTeamLeader ou isAdmin): chamar `TradeService.RemoveListingAsync`
    - `GET /api/trades/offers` (isTeamLeader): chamar `TradeService.GetReceivedOffersAsync`
    - `POST /api/trades/offers` (isTeamLeader ou isAdmin): body `{ offeredUserId, targetUserId }`, chamar `TradeService.CreateOfferAsync`
    - `PATCH /api/trades/offers/{id}/accept` (isTeamLeader): chamar `TradeService.AcceptOfferAsync`
    - `PATCH /api/trades/offers/{id}/reject` (isTeamLeader): chamar `TradeService.RejectOfferAsync`
    - `POST /api/trades/direct` (isAdmin): body `{ userAId, userBId }`, chamar `TradeService.DirectSwapAsync`
  - Verificar `IsTeamLeader` consultando o banco (não via claim), seguindo o padrão de verificação de `isAdmin` existente
  - Mapear todos os erros de domínio para os códigos HTTP definidos no design
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 10. Registrar serviços no Program.cs
  - Adicionar `builder.Services.AddScoped<ITeamMembershipService, TeamMembershipService>()` e `builder.Services.AddScoped<ITradeService, TradeService>()` em `src/FrogBets.Api/Program.cs`
  - _Requirements: 2.2, 3.1, 4.1, 5.1_

- [x] 11. Checkpoint — Verificar compilação e testes
  - Garantir que o projeto compila sem erros e todos os testes passam. Perguntar ao usuário se há dúvidas antes de prosseguir para o frontend.

- [x] 12. Frontend: adicionar seleção de time no RegisterPage
  - Em `frontend/src/api/client.ts` (ou arquivo adequado), adicionar função `getTeams()` para `GET /api/teams` e `register(data)` para aceitar `teamId?` opcional
  - Em `frontend/src/pages/RegisterPage.tsx`, buscar a lista de times ao montar o componente e exibir um campo `<select>` opcional para seleção de time
  - Incluir o `teamId` selecionado no payload enviado ao `POST /api/auth/register`
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 13. Frontend: adicionar seção de trocas no MarketplacePage
  - Em `frontend/src/api/client.ts`, adicionar funções para os endpoints de trocas: `getTradeListings()`, `createTradeListing(userId)`, `removeTradeListing(userId)`, `getReceivedOffers()`, `createTradeOffer(offeredUserId, targetUserId)`, `acceptOffer(id)`, `rejectOffer(id)`
  - Em `frontend/src/pages/MarketplacePage.tsx`, adicionar seção "Marketplace de Trocas" com:
    - Listagem de membros disponíveis para troca, agrupados por time
    - Para líderes: formulário para marcar/desmarcar membros do próprio time como disponíveis
    - Para líderes: listagem de ofertas recebidas com status `Pendente` e botões de aceitar/recusar
    - Para líderes: formulário para criar nova oferta de troca
  - _Requirements: 4.5, 5.6, 6.1, 6.4_

- [x] 14. Frontend: adicionar seção de gestão de líderes e troca direta no AdminPage
  - Em `frontend/src/api/client.ts`, adicionar funções: `assignLeader(teamId, userId)`, `removeLeader(teamId)`, `moveUserTeam(userId, teamId?)`, `directSwap(userAId, userBId)`
  - Em `frontend/src/pages/AdminPage.tsx`, adicionar seção "Gestão de Líderes" com:
    - Formulário para designar líder (select de time + select de usuário do time)
    - Formulário para remover líder de um time
    - Formulário para mover usuário de time (select de usuário + select de time destino, com opção de remover do time)
  - Adicionar seção "Troca Direta" com formulário para selecionar dois usuários de times distintos e executar a troca
  - _Requirements: 2.2, 2.3, 3.2, 3.3, 7.1_

- [x] 15. Checkpoint final — Garantir que todos os testes passam
  - Garantir que todos os testes passam. Perguntar ao usuário se há dúvidas antes de concluir.

## Notes

- Tasks marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- Os testes de propriedade usam **FsCheck** (`FsCheck.Xunit`) com mínimo de 100 iterações por propriedade
- Os testes de propriedade usam `UseInMemoryDatabase` para isolar o estado entre execuções
- Cada teste de propriedade deve ter o comentário: `// Feature: team-membership, Property N: <texto>`
- `IsTeamLeader` não é incluído no JWT — o controller consulta o banco para verificar o papel atual
- Seguir o padrão de tratamento de erros existente: `{ "error": { "code": "...", "message": "..." } }`
- O `BetsController` não requer nenhuma alteração (Requisito 8 já está satisfeito)

# Requirements Document

## Introduction

Esta spec cobre a remoção completa da entidade `CS2Player` do sistema FrogBets e a unificação do modelo de domínio em `User` + `CS2Team`. A entidade `CS2Player` existe hoje como intermediária entre `User` e `CS2Team`, mas é redundante: todo usuário registrado já é um player. A refatoração elimina essa duplicidade, move `PlayerScore` e `MatchesCount` para `User`, faz `MatchStats` referenciar `UserId` diretamente, e simplifica o frontend removendo o cadastro manual de jogadores.

## Glossary

- **System**: o backend ASP.NET Core 8 + frontend React/TypeScript do FrogBets
- **User**: entidade principal de usuário da plataforma (`Users` table)
- **CS2Team**: entidade de time de CS2 (`CS2Teams` table)
- **CS2Player**: entidade intermediária a ser removida (`CS2Players` table)
- **MatchStats**: registro de estatísticas de uma partida por usuário (`MatchStats` table)
- **PlayerScore**: pontuação acumulada de rating HLTV do usuário
- **MatchesCount**: número de partidas com estatísticas registradas para o usuário
- **RatingCalculator**: serviço que calcula o rating HLTV 2.0 adaptado
- **PlayersController**: controller REST em `/api/players`
- **MatchStatsService**: serviço que registra e consulta estatísticas de partida
- **PlayerService**: serviço que lista jogadores e ranking
- **AuthService**: serviço de autenticação e registro
- **TeamMembershipService**: serviço de gestão de membros de time
- **TeamService**: serviço de gestão de times
- **AdminPage**: página de administração do frontend
- **PlayersRankingPage**: página de ranking de jogadores do frontend
- **Migration**: migração EF Core para alteração do schema do banco

---

## Requirements

### Requirement 1: Migração do Modelo de Domínio

**User Story:** Como desenvolvedor, quero remover a entidade `CS2Player` e mover seus campos relevantes para `User`, para que o modelo de domínio reflita que todo usuário é um player.

#### Acceptance Criteria

1. THE System SHALL remover a entidade `CS2Player` e sua tabela correspondente do banco de dados via Migration EF Core.
2. THE System SHALL adicionar os campos `PlayerScore` (double, default 0.0) e `MatchesCount` (int, default 0) à entidade `User`.
3. THE System SHALL alterar a entidade `MatchStats` para que o campo `PlayerId` seja renomeado para `UserId` e referencie `User.Id` diretamente.
4. THE System SHALL remover o `DbSet<CS2Player>` do `FrogBetsDbContext` e adicionar a configuração de mapeamento dos novos campos em `User`.
5. THE System SHALL configurar a relação `MatchStats → User` com `OnDelete(DeleteBehavior.Restrict)` e índice único em `(UserId, MapResultId)`.
6. WHEN a Migration for aplicada, THE System SHALL preservar os dados históricos de `MatchStats` migrando os valores de `PlayerId` para o `UserId` do `User` vinculado ao `CS2Player` correspondente.

---

### Requirement 2: Atualização do MatchStatsService

**User Story:** Como admin, quero registrar estatísticas de partida diretamente para um usuário, para que não seja necessário manter a entidade `CS2Player`.

#### Acceptance Criteria

1. WHEN o admin registrar estatísticas via `POST /api/players/{id}/stats`, THE MatchStatsService SHALL validar que o `id` corresponde a um `User` existente (em vez de um `CS2Player`).
2. WHEN as estatísticas forem registradas com sucesso, THE MatchStatsService SHALL incrementar `User.PlayerScore` com o rating calculado e `User.MatchesCount` em 1.
3. WHEN um `UserId` inválido for fornecido, THE MatchStatsService SHALL lançar `InvalidOperationException("RESOURCE_NOT_FOUND")`.
4. WHEN estatísticas duplicadas forem detectadas para o mesmo `UserId` e `MapResultId`, THE MatchStatsService SHALL lançar `InvalidOperationException("STATS_ALREADY_REGISTERED")`.
5. WHEN o valor de `KastPercent` estiver fora do intervalo [0, 100], THE MatchStatsService SHALL lançar `InvalidOperationException("INVALID_KAST_VALUE")`.
6. THE MatchStatsService SHALL retornar um `MatchStatsDto` com `UserId` no campo anteriormente chamado `PlayerId`, mantendo compatibilidade de contrato com o frontend.

---

### Requirement 3: Atualização do PlayerService e IPlayerService

**User Story:** Como admin, quero listar usuários com suas estatísticas e ver o ranking baseado em `User`, para que a gestão de jogadores seja simplificada.

#### Acceptance Criteria

1. THE PlayerService SHALL listar todos os `Users` que possuem `TeamId` não nulo, retornando `Username`, `TeamId`, `TeamName`, `PlayerScore`, `MatchesCount` e `CreatedAt`.
2. THE PlayerService SHALL listar `Users` de um time específico filtrando por `TeamId`.
3. THE PlayerService SHALL calcular o ranking ordenando `Users` por `PlayerScore` descrescente, atribuindo posições sequenciais.
4. THE System SHALL remover os métodos `CreatePlayerAsync` e `AssignTeamAsync` do `IPlayerService`, pois a criação e atribuição de time passam a ser gerenciadas pelo `AuthService` e `TeamMembershipService`.
5. THE System SHALL renomear o DTO de `CS2PlayerDto` para `UserPlayerDto` (ou equivalente), removendo os campos `Nickname`, `RealName` e `PhotoUrl` que eram exclusivos de `CS2Player`.
6. THE PlayerService SHALL expor `Username` como o identificador de exibição do jogador no lugar de `Nickname`.

---

### Requirement 4: Atualização do AuthService

**User Story:** Como sistema, quero que o registro de um novo usuário não crie mais um `CS2Player`, para que o fluxo de registro seja simplificado.

#### Acceptance Criteria

1. WHEN um novo usuário for registrado via `POST /api/auth/register`, THE AuthService SHALL criar apenas a entidade `User`, sem criar `CS2Player`.
2. WHEN o registro incluir um `teamId` válido, THE AuthService SHALL definir `User.TeamId` com o valor fornecido.
3. THE AuthService SHALL inicializar `User.PlayerScore = 0.0` e `User.MatchesCount = 0` no momento do registro.
4. IF o `teamId` fornecido não existir, THEN THE AuthService SHALL lançar `InvalidOperationException("TEAM_NOT_FOUND")`.

---

### Requirement 5: Atualização do TeamMembershipService

**User Story:** Como sistema, quero que a movimentação de usuários entre times não sincronize mais com `CS2Player`, para que a lógica de membership seja simplificada.

#### Acceptance Criteria

1. WHEN um usuário for movido para outro time via `MoveUserAsync`, THE TeamMembershipService SHALL atualizar apenas `User.TeamId`, sem criar ou atualizar registros de `CS2Player`.
2. WHEN um usuário for removido de um time, THE TeamMembershipService SHALL definir `User.TeamId = null` e `User.IsTeamLeader = false`.
3. THE TeamMembershipService SHALL remover toda lógica de sincronização com `CS2Players` (criação, atualização de `TeamId` em `CS2Player`).

---

### Requirement 6: Atualização do TeamService

**User Story:** Como sistema, quero que a exclusão de um time não precise mais limpar registros de `CS2Player`, para que a lógica de deleção seja simplificada.

#### Acceptance Criteria

1. WHEN um time for excluído via `DeleteTeamAsync`, THE TeamService SHALL limpar `User.TeamId` e `User.IsTeamLeader` de todos os membros do time.
2. THE TeamService SHALL remover a chamada `ExecuteUpdateAsync` que limpava `CS2Player.TeamId` ao excluir um time.

---

### Requirement 7: Atualização do PlayersController

**User Story:** Como admin, quero que o endpoint de jogadores opere sobre `Users` diretamente, sem a necessidade de cadastrar jogadores manualmente.

#### Acceptance Criteria

1. THE PlayersController SHALL remover o endpoint `POST /api/players` (criação manual de jogador).
2. THE PlayersController SHALL remover o endpoint `PATCH /api/players/{id}/team` (atribuição manual de time).
3. THE System SHALL manter o endpoint `GET /api/players` retornando usuários com time atribuído.
4. THE System SHALL manter o endpoint `GET /api/players/ranking` retornando o ranking baseado em `User.PlayerScore`.
5. THE System SHALL manter os endpoints `POST /api/players/{id}/stats` e `GET /api/players/{id}/stats`, onde `{id}` passa a ser `UserId`.

---

### Requirement 8: Atualização do GamesController

**User Story:** Como usuário autenticado, quero que `GET /api/games/{id}/players` retorne os usuários dos times do jogo, para que a listagem de jogadores seja consistente com o novo modelo.

#### Acceptance Criteria

1. WHEN `GET /api/games/{id}/players` for chamado, THE GamesController SHALL retornar os `Users` cujo `TeamId` corresponde a um dos times do jogo.
2. THE GamesController SHALL retornar para cada usuário: `id` (UserId), `nickname` (Username), `teamName` (nome do time).
3. IF o jogo não for encontrado, THEN THE GamesController SHALL retornar 404 com código `GAME_NOT_FOUND`.

---

### Requirement 9: Atualização do TeamsController

**User Story:** Como usuário autenticado, quero que `GET /api/teams/{id}/players` retorne os usuários do time, para que a listagem seja baseada em `User`.

#### Acceptance Criteria

1. WHEN `GET /api/teams/{id}/players` for chamado, THE System SHALL retornar os `Users` com `TeamId` igual ao `{id}` fornecido.
2. THE System SHALL retornar para cada usuário: `id`, `username`, `playerScore`, `matchesCount`, `createdAt`.
3. IF o time não for encontrado, THEN THE System SHALL retornar 404 com código `TEAM_NOT_FOUND`.

---

### Requirement 10: Atualização do Frontend — API Client

**User Story:** Como desenvolvedor frontend, quero que as interfaces TypeScript reflitam o novo modelo sem `CS2Player`, para que o código do frontend seja consistente.

#### Acceptance Criteria

1. THE System SHALL remover a interface `CS2Player` de `frontend/src/api/players.ts`.
2. THE System SHALL adicionar uma interface `UserPlayer` com campos: `id`, `username`, `teamId`, `teamName`, `playerScore`, `matchesCount`, `createdAt`.
3. THE System SHALL atualizar a interface `PlayerRankingItem` para usar `userId` em vez de `playerId` e `username` em vez de `nickname`.
4. THE System SHALL atualizar a interface `GamePlayer` para usar `username` em vez de `nickname`.
5. THE System SHALL remover as funções `createPlayer` e `assignTeam` (se existirem) de `players.ts`.
6. THE System SHALL manter as funções `getPlayers`, `getPlayersRanking`, `registerMatchStats`, `getPlayerStats`, `getGamePlayers` e `getPlayersByTeam`, atualizando seus tipos de retorno.

---

### Requirement 11: Atualização do Frontend — AdminPage

**User Story:** Como admin, quero que o painel de administração não exiba mais a seção de cadastro manual de jogadores, para que a interface reflita o novo fluxo simplificado.

#### Acceptance Criteria

1. THE AdminPage SHALL remover completamente a seção "👤 Jogadores" (`PlayersSection` e `sec-players`) do painel de administração.
2. THE AdminPage SHALL remover o item "👤 Jogadores" do índice de navegação (`NAV_ITEMS_BASE`).
3. THE AdminPage SHALL atualizar a seção "📊 Estatísticas" (`MatchStatsSection`) para listar `Users` (com `username`) em vez de `CS2Players` (com `nickname`) no seletor de jogador.
4. WHEN o admin selecionar um jogo na seção de Estatísticas, THE AdminPage SHALL buscar os usuários do jogo via `GET /api/games/{id}/players` e exibir `username` como identificador.
5. THE AdminPage SHALL remover as importações de `CS2Player`, `getPlayers` e `createPlayer` que não forem mais utilizadas.

---

### Requirement 12: Atualização do Frontend — PlayersRankingPage

**User Story:** Como usuário, quero que a página de ranking continue funcionando após a remoção de `CS2Player`, exibindo os usuários ordenados por score.

#### Acceptance Criteria

1. THE PlayersRankingPage SHALL continuar exibindo o ranking via `GET /api/players/ranking`.
2. THE PlayersRankingPage SHALL exibir `username` na coluna "Nickname" em vez de `nickname`.
3. THE PlayersRankingPage SHALL usar `item.userId` como chave React em vez de `item.playerId`.
4. WHEN não houver usuários com estatísticas, THE PlayersRankingPage SHALL exibir a mensagem "Nenhum jogador encontrado."

---

### Requirement 13: Atualização dos Testes

**User Story:** Como desenvolvedor, quero que os testes reflitam o novo modelo sem `CS2Player`, para que a suíte de testes continue válida e confiável.

#### Acceptance Criteria

1. THE System SHALL reescrever `UserPlayerUnificationTests.cs` para testar o novo fluxo: registro cria `User` com `PlayerScore = 0` e `MatchesCount = 0`, sem criar `CS2Player`.
2. THE System SHALL atualizar `PlayerRatingSystemTests.cs` para usar `User` diretamente em vez de `CS2Player` nos helpers de seed e nas asserções.
3. THE System SHALL garantir que a propriedade de acumulação de `PlayerScore` em `User` após múltiplos registros de `MatchStats` seja coberta por property-based test.
4. THE System SHALL garantir que o ranking baseado em `User.PlayerScore` seja coberto por property-based test.
5. FOR ALL registros de `MatchStats` com `UserId` válido e `MapResultId` válido, THE System SHALL verificar que `User.PlayerScore` acumula corretamente (round-trip: score final = soma dos ratings individuais).
6. THE System SHALL remover todos os helpers de seed que criam `CS2Player` diretamente nos testes.

# Requirements Document

## Introduction

Esta feature adiciona operações de edição e exclusão de jogos (Games) ao FrogBets, restringe todas as rotas de API a usuários autenticados (removendo rotas públicas existentes) e atualiza a documentação do projeto. O objetivo é dar ao admin controle completo sobre o ciclo de vida de um jogo — inclusive cancelar ou corrigir jogos cadastrados incorretamente — e garantir que nenhum dado da plataforma seja acessível sem autenticação.

## Glossary

- **GamesController**: Controller ASP.NET Core responsável pelos endpoints de jogos em `src/FrogBets.Api/Controllers/GamesController.cs`.
- **GameService**: Serviço de domínio responsável pela lógica de negócio de jogos.
- **Game**: Entidade que representa uma partida de CS2 agendada, com times, data, número de mapas e status (`Scheduled`, `InProgress`, `Finished`).
- **Market**: Mercado de apostas vinculado a um Game. Gerado automaticamente na criação do jogo.
- **Bet**: Aposta vinculada a um Market. Pode estar `Pending`, `Active`, `Settled`, `Cancelled` ou `Voided`.
- **Admin**: Usuário com `IsAdmin = true` no banco de dados, verificado via claim `isAdmin` no JWT.
- **Rota pública**: Endpoint marcado com `[AllowAnonymous]` que não exige JWT.
- **Rota privada**: Endpoint que exige JWT Bearer válido (`[Authorize]`).
- **GAME_NOT_FOUND**: Código de erro retornado quando o jogo solicitado não existe.
- **GAME_CANNOT_BE_EDITED**: Código de erro retornado quando se tenta editar um jogo que não está no status `Scheduled`.
- **GAME_CANNOT_BE_DELETED**: Código de erro retornado quando se tenta excluir um jogo com status `InProgress` ou `Finished`.

---

## Requirements

### Requirement 1: Editar jogo agendado

**User Story:** Como admin, quero editar os dados de um jogo agendado (times, data e número de mapas), para que eu possa corrigir informações cadastradas incorretamente antes do jogo começar.

#### Acceptance Criteria

1. WHEN um admin envia `PATCH /api/games/{id}` com campos válidos, THE GamesController SHALL atualizar os campos `TeamA`, `TeamB`, `ScheduledAt` e/ou `NumberOfMaps` do jogo e retornar `200 OK` com os dados atualizados.
2. WHEN o jogo não existe, THE GamesController SHALL retornar `404 Not Found` com código `GAME_NOT_FOUND`.
3. WHEN o jogo possui status diferente de `Scheduled`, THE GamesController SHALL retornar `409 Conflict` com código `GAME_CANNOT_BE_EDITED`.
4. WHEN um usuário não-admin envia `PATCH /api/games/{id}`, THE GamesController SHALL retornar `403 Forbidden`.
5. WHEN um usuário não autenticado envia `PATCH /api/games/{id}`, THE GamesController SHALL retornar `401 Unauthorized`.
6. WHEN o body contém campos inválidos (ex: `TeamA` vazio, `NumberOfMaps` fora do intervalo 1–5), THE GamesController SHALL retornar `400 Bad Request`.
7. WHEN o admin altera `NumberOfMaps`, THE GameService SHALL recalcular e regenerar os mercados do jogo, removendo os mercados sem apostas e adicionando os novos mercados necessários.

### Requirement 2: Excluir jogo

**User Story:** Como admin, quero excluir um jogo que não vai mais ocorrer, para que ele não apareça na listagem e não confunda os usuários.

#### Acceptance Criteria

1. WHEN um admin envia `DELETE /api/games/{id}` para um jogo com status `Scheduled`, THE GamesController SHALL cancelar todas as apostas `Pending` e `Active`, devolver o saldo reservado aos respectivos usuários (criador e cobrador), excluir o jogo e todos os seus mercados, e retornar `204 No Content`.
2. WHEN o jogo não existe, THE GamesController SHALL retornar `404 Not Found` com código `GAME_NOT_FOUND`.
3. WHEN o jogo possui status `InProgress` ou `Finished`, THE GamesController SHALL retornar `409 Conflict` com código `GAME_CANNOT_BE_DELETED`.
4. WHEN um usuário não-admin envia `DELETE /api/games/{id}`, THE GamesController SHALL retornar `403 Forbidden`.
5. WHEN um usuário não autenticado envia `DELETE /api/games/{id}`, THE GamesController SHALL retornar `401 Unauthorized`.

### Requirement 3: Tornar todas as rotas de API privadas

**User Story:** Como admin, quero que todas as rotas da API exijam autenticação, para que nenhum dado da plataforma seja acessível sem um JWT válido.

#### Acceptance Criteria

1. THE GamesController SHALL remover `[AllowAnonymous]` de `GET /api/games` e exigir JWT Bearer válido.
2. THE GamesController SHALL remover `[AllowAnonymous]` de `GET /api/games/{id}` e exigir JWT Bearer válido.
3. THE TeamsController SHALL remover `[AllowAnonymous]` de `GET /api/teams` e exigir JWT Bearer válido.
4. THE PlayersController SHALL remover `[AllowAnonymous]` de `GET /api/players/ranking` e exigir JWT Bearer válido.
5. THE PlayersController SHALL remover `[AllowAnonymous]` de `GET /api/players/{id}/stats` e exigir JWT Bearer válido.
6. WHEN um usuário não autenticado acessa qualquer rota listada nos critérios 1–5, THE API SHALL retornar `401 Unauthorized`.
7. THE Frontend SHALL atualizar todas as chamadas que usavam `publicClient` para usar `apiClient` (com JWT) nos endpoints afetados.

### Requirement 4: Atualizar documentação

**User Story:** Como desenvolvedor, quero que a documentação reflita as mudanças de endpoints e comportamentos, para que eu possa entender o estado atual da API sem precisar ler o código.

#### Acceptance Criteria

1. THE README.md SHALL refletir as funcionalidades de edição e exclusão de jogos na seção "Funcionalidades".
2. THE docs/TECHNICAL.md SHALL listar os novos endpoints `PATCH /api/games/{id}` e `DELETE /api/games/{id}` na tabela de Jogos com método, rota, autenticação e descrição corretos.
3. THE docs/TECHNICAL.md SHALL atualizar a coluna "Auth" das rotas `GET /api/games`, `GET /api/games/{id}`, `GET /api/teams`, `GET /api/players/ranking` e `GET /api/players/{id}/stats` de `Público` para `JWT`.
4. THE docs/TECHNICAL.md SHALL documentar os novos códigos de erro `GAME_CANNOT_BE_EDITED` e `GAME_CANNOT_BE_DELETED` na tabela de códigos de erro da seção de Jogos.

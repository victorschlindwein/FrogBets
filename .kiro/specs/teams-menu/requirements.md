# Requirements Document

## Introduction

Adicionar uma página "Times" ao frontend da plataforma FrogBets, acessível via menu de navegação. A página exibe todos os times de CS2 cadastrados, com nome, logo e lista de jogadores de cada time. O objetivo é dar visibilidade à composição dos times para os membros do grupo FrogEventos, facilitando o acompanhamento das partidas e apostas.

O backend já possui o endpoint `GET /api/teams` (retorna times com nome e logo) e `GET /api/players` (retorna jogadores com vínculo de time). A feature é puramente frontend: uma nova rota, um novo item no menu e uma nova página.

## Glossary

- **Teams_Page**: Página React em `/teams` que exibe os times e seus jogadores.
- **Teams_Menu_Item**: Link "Times" adicionado à Navbar de navegação.
- **Team_Card**: Componente visual que representa um time com sua logo, nome e lista de jogadores.
- **Teams_API**: Módulo de chamadas Axios para o endpoint `GET /api/teams`.
- **Players_API**: Módulo existente em `frontend/src/api/players.ts` com funções `getTeams` e `getPlayers`.
- **CS2Team**: Entidade de time com campos `id`, `name`, `logoUrl`.
- **CS2Player**: Entidade de jogador com campos `id`, `nickname`, `realName`, `photoUrl`, `playerScore`, `teamId`.

## Requirements

### Requirement 1: Item de menu "Times"

**User Story:** Como membro do FrogEventos, quero ver um link "Times" na barra de navegação, para que eu possa acessar a página de times rapidamente.

#### Acceptance Criteria

1. THE Navbar SHALL exibir um link com o texto "Times" apontando para a rota `/teams`.
2. WHEN o usuário clica no link "Times", THE Navbar SHALL fechar o menu mobile (se estiver aberto) e navegar para `/teams`.
3. WHILE o usuário está autenticado, THE Teams_Menu_Item SHALL estar visível na Navbar.

---

### Requirement 2: Rota `/teams` protegida

**User Story:** Como membro autenticado, quero que a página de times seja protegida por autenticação, para que apenas membros do grupo possam acessá-la.

#### Acceptance Criteria

1. THE App SHALL registrar a rota `/teams` dentro do bloco `<ProtectedRoute />`.
2. IF o usuário não está autenticado e acessa `/teams`, THEN THE App SHALL redirecionar o usuário para `/login`.

---

### Requirement 3: Carregamento e exibição dos times

**User Story:** Como membro do FrogEventos, quero ver todos os times cadastrados com seus jogadores, para que eu possa conhecer a composição de cada time.

#### Acceptance Criteria

1. WHEN a Teams_Page é carregada, THE Teams_Page SHALL buscar os times via `GET /api/teams` e os jogadores via `GET /api/teams/{id}/players` (ou endpoint equivalente acessível por qualquer usuário autenticado).
2. WHILE os dados estão sendo carregados, THE Teams_Page SHALL exibir uma mensagem de carregamento.
3. WHEN os dados são carregados com sucesso, THE Teams_Page SHALL exibir um Team_Card para cada time retornado pela API.
4. THE Team_Card SHALL exibir o nome do time.
5. WHERE o time possui `logoUrl` preenchido, THE Team_Card SHALL exibir a logo do time como imagem.
6. WHERE o time não possui `logoUrl`, THE Team_Card SHALL exibir um placeholder visual no lugar da logo.
7. THE Team_Card SHALL listar os jogadores pertencentes ao time, exibindo o `nickname` de cada jogador.
8. WHERE o jogador possui `photoUrl` preenchido, THE Team_Card SHALL exibir a foto do jogador.
9. WHERE o time não possui jogadores cadastrados, THE Team_Card SHALL exibir a mensagem "Nenhum jogador cadastrado."

---

### Requirement 5: Upload e remoção de logo pelo líder do time

**User Story:** Como líder de time, quero poder adicionar ou remover a logo do meu time, para que a identidade visual do time esteja sempre atualizada.

#### Acceptance Criteria

1. WHILE o usuário autenticado é líder do time exibido, THE Team_Card SHALL exibir um botão para adicionar ou substituir a logo do time.
2. WHILE o usuário autenticado é líder do time exibido e o time possui `logoUrl` preenchido, THE Team_Card SHALL exibir um botão para remover a logo do time.
3. WHEN o líder seleciona um arquivo de imagem e confirma o upload, THE Teams_Page SHALL enviar o arquivo via `PUT /api/teams/{id}/logo` e atualizar a logo exibida com a URL retornada pela API.
4. WHEN o líder confirma a remoção da logo, THE Teams_Page SHALL enviar a requisição via `DELETE /api/teams/{id}/logo` e substituir a logo pelo placeholder visual.
5. IF a requisição de upload ou remoção de logo falha, THEN THE Teams_Page SHALL exibir uma mensagem de erro descritiva.
6. THE Logo_Upload_Endpoint SHALL rejeitar requisições de usuários que não são líderes do time informado, retornando HTTP 403.
7. WHILE o usuário autenticado não é líder do time exibido, THE Team_Card SHALL ocultar os controles de upload e remoção de logo.

---

### Requirement 4: Tratamento de erros

**User Story:** Como membro do FrogEventos, quero receber feedback claro quando a página de times não consegue carregar os dados, para que eu saiba que houve um problema.

#### Acceptance Criteria

1. IF a requisição a `GET /api/teams` falha, THEN THE Teams_Page SHALL exibir uma mensagem de erro descritiva.
2. IF a requisição a `GET /api/players` falha, THEN THE Teams_Page SHALL exibir uma mensagem de erro descritiva.
3. WHEN nenhum time é retornado pela API, THE Teams_Page SHALL exibir a mensagem "Nenhum time cadastrado."

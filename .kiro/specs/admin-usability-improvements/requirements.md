# Requirements Document

## Introduction

O painel de administração do FrogBets atualmente exige que o admin copie e cole UUIDs manualmente em campos de texto para identificar usuários e jogadores. Isso é propenso a erros e prejudica a usabilidade. Esta feature substitui todos os campos de UUID por dropdowns com nomes legíveis, e converte o campo de nickname livre na tela de atribuição de jogador a time por um dropdown com os jogadores existentes, indicando visualmente quais já estão alocados.

## Glossary

- **Admin_Panel**: O componente `AdminPage.tsx` do frontend que concentra todas as operações administrativas.
- **CS2Player**: Entidade que representa um jogador de CS2, com nickname, nome real, time e estatísticas.
- **CS2Team**: Entidade que representa um time de CS2.
- **User**: Usuário da plataforma FrogBets, identificado por UUID e username.
- **Player_Dropdown**: Elemento `<select>` que lista todos os CS2Players existentes, com opções desabilitadas para jogadores já alocados a um time.
- **User_Dropdown**: Elemento `<select>` que lista todos os usuários da plataforma pelo username, substituindo campos de input de UUID.
- **Allocated_Player**: CS2Player que já possui um `teamId` associado.
- **Unallocated_Player**: CS2Player cujo `teamId` é nulo ou vazio.

## Requirements

### Requirement 1: Player Dropdown na Tela de Cadastro de Jogador

**User Story:** Como admin, quero selecionar um jogador existente a partir de um dropdown ao atribuí-lo a um time, para evitar digitar nicknames manualmente e ter visibilidade de quais jogadores já estão alocados.

#### Acceptance Criteria

1. WHEN a seção "Jogadores" do Admin_Panel é renderizada, THE Admin_Panel SHALL buscar a lista completa de CS2Players via `GET /api/players` e exibir um `<select>` no lugar do campo de texto de nickname.
2. THE Player_Dropdown SHALL listar todos os CS2Players existentes, exibindo o nickname de cada um como texto da opção.
3. WHEN um CS2Player é um Allocated_Player, THE Player_Dropdown SHALL exibir a opção correspondente com o atributo `disabled` e com o nome do time entre parênteses ao lado do nickname (ex: `s1mple (Natus Vincere)`).
4. WHEN um CS2Player é um Unallocated_Player, THE Player_Dropdown SHALL exibir a opção correspondente habilitada, sem sufixo de time.
5. WHEN o admin seleciona um Unallocated_Player no Player_Dropdown e submete o formulário, THE Admin_Panel SHALL enviar `POST /api/players` com o `nickname` do jogador selecionado e o `teamId` escolhido.
6. IF a lista de CS2Players não puder ser carregada, THEN THE Admin_Panel SHALL exibir uma mensagem de erro indicando falha ao carregar jogadores.
7. WHEN um novo jogador é cadastrado com sucesso, THE Admin_Panel SHALL recarregar a lista de CS2Players para refletir o novo estado no Player_Dropdown.

### Requirement 2: User Dropdown na Seção "Designar Líder"

**User Story:** Como admin, quero selecionar um usuário pelo username ao designar um líder de time, para não precisar copiar e colar UUIDs manualmente.

#### Acceptance Criteria

1. WHEN a seção "Gestão de Líderes" do Admin_Panel é renderizada, THE Admin_Panel SHALL buscar a lista de usuários via `GET /api/users` e exibir um User_Dropdown no campo "ID do Usuário" do formulário "Designar Líder".
2. THE User_Dropdown SHALL listar todos os usuários da plataforma exibindo o `username` como texto da opção.
3. WHEN o admin seleciona um usuário no User_Dropdown e submete o formulário, THE Admin_Panel SHALL usar o `id` (UUID) do usuário selecionado para chamar `POST /api/teams/{teamId}/leader/{userId}`.
4. IF a lista de usuários não puder ser carregada, THEN THE Admin_Panel SHALL exibir uma mensagem de erro indicando falha ao carregar usuários.

### Requirement 3: User Dropdown na Seção "Mover Usuário de Time"

**User Story:** Como admin, quero selecionar um usuário pelo username ao movê-lo de time, para não precisar copiar e colar UUIDs manualmente.

#### Acceptance Criteria

1. WHEN a seção "Gestão de Líderes" do Admin_Panel é renderizada, THE Admin_Panel SHALL exibir um User_Dropdown no campo "ID do Usuário" do formulário "Mover Usuário de Time".
2. THE User_Dropdown SHALL listar todos os usuários da plataforma exibindo o `username` como texto da opção, reutilizando a mesma lista carregada para a seção de líderes.
3. WHEN o admin seleciona um usuário no User_Dropdown e submete o formulário, THE Admin_Panel SHALL usar o `id` (UUID) do usuário selecionado para chamar `PATCH /api/users/{id}/team`.

### Requirement 4: User Dropdowns na Seção "Troca Direta"

**User Story:** Como admin, quero selecionar os dois usuários pelo username ao realizar uma troca direta de time, para não precisar copiar e colar UUIDs manualmente.

#### Acceptance Criteria

1. WHEN a seção "Troca Direta" do Admin_Panel é renderizada, THE Admin_Panel SHALL buscar a lista de usuários via `GET /api/users` e exibir User_Dropdowns nos campos "ID do Usuário A" e "ID do Usuário B".
2. THE User_Dropdown de Usuário A e o User_Dropdown de Usuário B SHALL listar todos os usuários da plataforma exibindo o `username` como texto da opção.
3. WHEN o admin seleciona os dois usuários e submete o formulário, THE Admin_Panel SHALL usar os `id`s (UUIDs) dos usuários selecionados para chamar `POST /api/trades/direct`.
4. IF o admin selecionar o mesmo usuário nos dois dropdowns, THEN THE Admin_Panel SHALL exibir uma mensagem de erro de validação antes de submeter o formulário.

### Requirement 5: Carregamento Compartilhado de Dados

**User Story:** Como admin, quero que os dropdowns de usuários e jogadores sejam carregados de forma eficiente, para que o painel não faça requisições redundantes desnecessárias.

#### Acceptance Criteria

1. THE Admin_Panel SHALL carregar a lista de usuários uma única vez no nível da página principal e repassá-la como prop para as seções que precisam de User_Dropdowns (Gestão de Líderes e Troca Direta).
2. WHEN a lista de usuários é atualizada (ex: após promoção ou remoção de admin), THE Admin_Panel SHALL recarregar a lista e propagar a atualização para todos os User_Dropdowns.
3. THE Admin_Panel SHALL carregar a lista de CS2Players uma única vez na seção "Jogadores" e recarregá-la após cada cadastro bem-sucedido.

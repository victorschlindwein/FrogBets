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
- **InvitesSection**: Subcomponente do Admin_Panel responsável por gerar e listar convites.
- **LeaderManagementSection**: Subcomponente do Admin_Panel responsável por designar líderes e mover usuários de time.
- **DirectSwapSection**: Subcomponente do Admin_Panel responsável por realizar trocas diretas de usuários entre times.
- **TeamsSection**: Subcomponente do Admin_Panel responsável por cadastrar e listar times.

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

### Requirement 6: Identificação de Destinatário em Convites em Massa

**User Story:** Como admin, quero poder identificar para quem enviei cada convite mesmo ao gerar em massa, para ter rastreabilidade de quem recebeu cada token.

#### Acceptance Criteria

1. WHEN o admin gera convites com quantidade maior que 1, THE InvitesSection SHALL exibir um campo "Descrição" para cada convite gerado, permitindo identificar o destinatário individualmente.
2. WHEN o admin submete o formulário de geração em massa, THE InvitesSection SHALL enviar o campo `description` individualmente para cada convite via `POST /api/invites`.
3. THE InvitesSection SHALL exibir a coluna "Descrição" na listagem de convites, mostrando o valor do campo `description` retornado por `GET /api/invites` ou "—" quando ausente.
4. WHEN um convite possui `description` preenchido, THE InvitesSection SHALL exibir o valor na listagem sem truncamento para descrições de até 100 caracteres.

### Requirement 7: Nome do Time nos Dropdowns de Jogadores

**User Story:** Como admin, quero ver o nome do time ao lado do nome do jogador em todos os dropdowns onde jogadores aparecem, para facilitar atribuições sem precisar memorizar quem pertence a qual time.

#### Acceptance Criteria

1. WHEN um CS2Player é um Allocated_Player, THE Admin_Panel SHALL exibir o nome do jogador seguido do nome do time separado por " - " em todos os dropdowns de jogadores (ex: `FalleN - FURIA`).
2. WHEN um CS2Player é um Unallocated_Player, THE Admin_Panel SHALL exibir apenas o nickname do jogador, sem sufixo de time.
3. THE Admin_Panel SHALL aplicar o formato `"nickname - teamName"` de forma consistente em todos os componentes que renderizam dropdowns de CS2Players, incluindo `PlayersSection` e `MatchStatsSection`.

### Requirement 8: Filtro por Time no Dropdown "Designar Líder"

**User Story:** Como admin, quero que o dropdown de usuários na seção "Designar Líder" mostre apenas os membros do time selecionado, para evitar designar como líder um usuário que não pertence ao time.

#### Acceptance Criteria

1. WHEN o admin seleciona um time no formulário "Designar Líder", THE LeaderManagementSection SHALL filtrar o User_Dropdown de usuários para exibir apenas os usuários cujo `teamId` corresponde ao time selecionado.
2. WHILE nenhum time está selecionado no formulário "Designar Líder", THE LeaderManagementSection SHALL exibir o User_Dropdown vazio ou desabilitado, sem listar usuários.
3. WHEN o admin altera a seleção de time no formulário "Designar Líder", THE LeaderManagementSection SHALL redefinir a seleção de usuário para o estado vazio.

### Requirement 9: Seleção de Time Antes de Listar Usuários em "Mover Usuário de Time"

**User Story:** Como admin, quero primeiro selecionar o time de origem antes de ver os usuários disponíveis para mover, para não precisar percorrer uma lista com todos os usuários da plataforma.

#### Acceptance Criteria

1. WHEN a seção "Mover Usuário de Time" é renderizada, THE LeaderManagementSection SHALL exibir primeiro um dropdown de seleção de "Time de Origem" antes do dropdown de usuários.
2. WHEN o admin seleciona um time de origem, THE LeaderManagementSection SHALL filtrar o User_Dropdown para exibir apenas os membros daquele time.
3. WHILE nenhum time de origem está selecionado, THE LeaderManagementSection SHALL manter o User_Dropdown de usuários desabilitado ou oculto.
4. WHEN o admin altera o time de origem, THE LeaderManagementSection SHALL redefinir a seleção de usuário para o estado vazio.

### Requirement 10: Fluxo em Duas Etapas na Troca Direta

**User Story:** Como admin, quero selecionar o time antes de ver os jogadores disponíveis em cada lado da troca direta, para não precisar percorrer uma lista com todos os usuários da plataforma.

#### Acceptance Criteria

1. WHEN a seção "Troca Direta" é renderizada, THE DirectSwapSection SHALL exibir dois grupos independentes: "Jogador A" (com seleção de Time A → Usuário A) e "Jogador B" (com seleção de Time B → Usuário B).
2. WHEN o admin seleciona o Time A, THE DirectSwapSection SHALL filtrar o dropdown de Usuário A para exibir apenas os membros do Time A.
3. WHEN o admin seleciona o Time B, THE DirectSwapSection SHALL filtrar o dropdown de Usuário B para exibir apenas os membros do Time B.
4. WHILE nenhum time está selecionado em um grupo, THE DirectSwapSection SHALL manter o dropdown de usuário correspondente desabilitado ou oculto.
5. WHEN o admin altera a seleção de time em qualquer grupo, THE DirectSwapSection SHALL redefinir a seleção de usuário daquele grupo para o estado vazio.
6. IF o admin selecionar o mesmo usuário nos dois grupos, THEN THE DirectSwapSection SHALL exibir uma mensagem de erro de validação antes de submeter o formulário.

### Requirement 11: Correção do Carregamento de Logo dos Times

**User Story:** Como admin, quero que as logos dos times sejam exibidas corretamente no painel, para ter confirmação visual de que as imagens foram cadastradas corretamente.

#### Acceptance Criteria

1. WHEN um CS2Team possui `logoUrl` preenchido, THE TeamsSection SHALL exibir a imagem da logo na listagem de times.
2. IF a imagem de logo não puder ser carregada (URL inválida ou erro de rede), THEN THE TeamsSection SHALL ocultar o elemento de imagem sem exibir ícone de imagem quebrada.
3. THE TeamsSection SHALL garantir que a URL da logo seja passada corretamente ao atributo `src` do elemento `<img>`, sem transformações que possam invalidar a URL original.

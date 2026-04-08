# Requirements Document

## Introduction

Atualmente, `User` (apostador) e `CS2Player` (jogador de CS2) são entidades independentes — o admin precisa cadastrar jogadores manualmente após o usuário se registrar. Esta feature unifica o fluxo: ao se registrar via convite, um `CS2Player` é criado automaticamente com o `username` como `nickname`. A criação manual de jogadores pelo admin é removida. A seção "Jogadores" do painel admin passa a ser somente leitura. Stats de partida continuam referenciando `CS2Player`, que agora possui FK obrigatória para `User`.

## Glossary

- **User**: Usuário da plataforma. Realiza apostas e é simultaneamente jogador de CS2.
- **CS2Player**: Entidade que representa o perfil de jogador de CS2 de um `User`. Contém nickname, score acumulado e estatísticas de partida.
- **Nickname**: Nome exibido no contexto de CS2 (ranking, stats). Inicialmente igual ao `username`, mas campo separado para permitir alteração futura.
- **Username**: Identificador de login do `User`. Imutável após o registro.
- **MatchStats**: Estatísticas de um `CS2Player` em um mapa específico (kills, deaths, assists, damage, KAST, rating).
- **AuthService**: Serviço responsável pelo registro e autenticação de usuários.
- **PlayerService**: Serviço responsável pela gestão de `CS2Player`.
- **Admin**: Usuário com `IsAdmin = true`. Gerencia jogos, convites, times e visualiza jogadores.

---

## Requirements

### Requirement 1: Criação automática de CS2Player no registro

**User Story:** Como membro do grupo FrogEventos, quero que meu perfil de jogador de CS2 seja criado automaticamente quando me registro via convite, para que eu não precise de uma etapa manual separada.

#### Acceptance Criteria

1. WHEN um `User` é criado com sucesso via `POST /api/auth/register`, THE `AuthService` SHALL criar um `CS2Player` vinculado ao `User` com `Nickname` igual ao `Username` do `User`.
2. WHEN um `User` é criado com sucesso via `POST /api/auth/register`, THE `AuthService` SHALL persistir o `CS2Player` na mesma operação de registro, antes de retornar o token JWT.
3. IF a criação do `CS2Player` falhar durante o registro, THEN THE `AuthService` SHALL reverter a criação do `User` e retornar erro `REGISTRATION_FAILED`.
4. THE `CS2Player` SHALL conter os campos `UserId` (FK para `User`), `Nickname`, `PlayerScore` (inicial: 0.0) e `MatchesCount` (inicial: 0) ao ser criado.
5. THE `CS2Player` SHALL manter `TeamId` como campo obrigatório — o time é definido no momento do registro via `TeamId` do `User`.

---

### Requirement 2: Relação 1:1 entre User e CS2Player

**User Story:** Como desenvolvedor, quero que cada `User` tenha exatamente um `CS2Player` vinculado, para garantir consistência entre o perfil de apostador e o perfil de jogador.

#### Acceptance Criteria

1. THE `CS2Player` SHALL conter uma FK `UserId` referenciando `User`, com constraint `UNIQUE` no banco de dados.
2. THE `CS2Player` SHALL manter `Nickname` e `Username` como campos separados — `Nickname` pertence ao `CS2Player`, `Username` pertence ao `User`.
3. IF uma tentativa de criar um segundo `CS2Player` para o mesmo `User` for realizada, THEN THE `PlayerService` SHALL lançar erro `PLAYER_ALREADY_EXISTS_FOR_USER`.
4. WHEN o `CS2Player` de um `User` é consultado, THE `PlayerService` SHALL retornar o `Username` do `User` vinculado junto ao DTO do `CS2Player`.

---

### Requirement 3: Remoção da criação manual de jogadores pelo admin

**User Story:** Como admin, não quero mais precisar cadastrar jogadores manualmente, pois o cadastro agora é automático no registro.

#### Acceptance Criteria

1. WHEN uma requisição `POST /api/players` é recebida, THE `PlayersController` SHALL retornar `HTTP 405 Method Not Allowed` ou `HTTP 404 Not Found`, removendo o endpoint de criação manual.
2. THE `PlayerService` SHALL remover a operação `CreatePlayerAsync` da interface pública ou torná-la inacessível via API.
3. WHEN o admin acessa a seção "Jogadores" do painel, THE `AdminPage` SHALL exibir apenas a lista de jogadores existentes, sem formulário de criação.
4. WHEN o admin acessa a seção "Jogadores" do painel, THE `AdminPage` SHALL exibir o `Username` do `User` vinculado a cada `CS2Player` na listagem.

---

### Requirement 4: Seleção de jogador por username ao registrar stats

**User Story:** Como admin, quero selecionar o jogador pelo username do usuário ao registrar estatísticas de partida, para identificar facilmente quem é quem sem depender de nicknames separados.

#### Acceptance Criteria

1. WHEN o admin acessa o formulário de registro de stats, THE `AdminPage` SHALL exibir a lista de jogadores com o `Username` do `User` vinculado como identificador principal.
2. WHEN o admin registra stats via `POST /api/players/{id}/stats`, THE `PlayersController` SHALL aceitar o `id` do `CS2Player` (sem alteração na assinatura do endpoint).
3. WHEN o admin consulta `GET /api/players`, THE `PlayersController` SHALL retornar o `Username` do `User` vinculado em cada item da resposta.
4. WHEN o admin consulta `GET /api/players/ranking`, THE `PlayersController` SHALL retornar o `Nickname` do `CS2Player` (não o `Username`) como identificador público no ranking.

---

### Requirement 5: Compatibilidade com dados existentes

**User Story:** Como admin, quero que os `CS2Players` já cadastrados manualmente continuem funcionando após a migração, para não perder o histórico de stats.

#### Acceptance Criteria

1. WHEN a migração de banco de dados é executada, THE `Database` SHALL adicionar a coluna `UserId` (nullable) na tabela `CS2Players` sem remover registros existentes.
2. WHILE existirem `CS2Players` sem `UserId` vinculado (legados), THE `PlayersController` SHALL continuar retornando esses jogadores nas listagens e no ranking.
3. IF um `CS2Player` legado não tiver `UserId` vinculado, THEN THE `PlayerService` SHALL retornar `null` ou string vazia no campo `Username` do DTO, sem lançar exceção.
4. THE `Database` SHALL aplicar a constraint `UNIQUE` em `UserId` apenas para valores não-nulos, permitindo múltiplos registros legados com `UserId = NULL`.

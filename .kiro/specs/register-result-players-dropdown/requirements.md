# Requirements Document

## Introduction

A seção "Registrar Resultado" no `AdminPage` atualmente exibe campos de texto livre para mercados de jogador (`TopKills`, `MostDeaths`, `MostUtilityDamage`). Isso é propenso a erros de digitação e inconsistências com os nomes reais dos jogadores cadastrados.

Esta feature substitui os campos de texto livre por dropdowns SELECT populados com os usuários (Users) que jogaram aquela partida, alinhando o fluxo de registro de resultado com o fluxo de apostas. Também reorienta o endpoint `GET /api/games/{id}/players` para retornar dados baseados em `Users` em vez de `CS2Players`, preparando o terreno para a futura absorção de `CS2Players` por `Users`. Por fim, reagrupa a ordem de exibição dos mercados: primeiro os de resultado de partida (`MapWinner`, `SeriesWinner`), depois os de jogador.

## Glossary

- **AdminPage**: Página de administração do frontend (`frontend/src/pages/AdminPage.tsx`)
- **RegisterResultSection**: Componente dentro do `AdminPage` responsável por registrar resultados de mercados
- **Market**: Mercado de aposta associado a um jogo. Tipos: `MapWinner`, `SeriesWinner`, `TopKills`, `MostDeaths`, `MostUtilityDamage`
- **Player_Market**: Mercado cujo resultado é um jogador (`TopKills`, `MostDeaths`, `MostUtilityDamage`)
- **Team_Market**: Mercado cujo resultado é um time (`MapWinner`, `SeriesWinner`)
- **GamePlayer**: Representação de um jogador (User) que participa de um jogo, retornado pelo endpoint `GET /api/games/{id}/players`
- **GamesController**: Controller ASP.NET em `src/FrogBets.Api/Controllers/GamesController.cs`
- **User**: Entidade principal de usuário da plataforma (`Id`, `Username`, `TeamId`)
- **CS2Player**: Entidade legada que associa um `User` a um `CS2Team`. Será absorvida por `User` no futuro

## Requirements

### Requirement 1: Dropdown de jogadores nos mercados de jogador

**User Story:** Como admin, quero selecionar o jogador vencedor de um mercado de jogador a partir de um dropdown, para evitar erros de digitação e garantir consistência com os nomes cadastrados.

#### Acceptance Criteria

1. WHEN o admin seleciona um jogo em andamento na seção "Registrar Resultado", THE RegisterResultSection SHALL carregar a lista de jogadores daquele jogo via `GET /api/games/{id}/players` e exibir um SELECT dropdown para cada mercado do tipo `TopKills`, `MostDeaths` ou `MostUtilityDamage`.
2. THE RegisterResultSection SHALL popular cada dropdown de Player_Market com os nomes de usuário (username) dos jogadores retornados pelo endpoint, mais uma opção "— pular —" como valor vazio padrão.
3. WHEN o admin submete o formulário, THE RegisterResultSection SHALL enviar o `username` do jogador selecionado como `winningOption` para `POST /api/games/{id}/results`, mantendo o mesmo formato já aceito pelo backend.
4. IF o endpoint `GET /api/games/{id}/players` retornar uma lista vazia, THEN THE RegisterResultSection SHALL exibir os dropdowns de Player_Market desabilitados com a mensagem "Nenhum jogador encontrado para este jogo".
5. IF o endpoint `GET /api/games/{id}/players` retornar erro, THEN THE RegisterResultSection SHALL exibir uma mensagem de erro e manter os campos de Player_Market inacessíveis.

### Requirement 2: Reordenação dos mercados no formulário

**User Story:** Como admin, quero ver primeiro os mercados de resultado de partida e depois os de jogador, para seguir uma ordem lógica ao registrar resultados.

#### Acceptance Criteria

1. WHEN a lista de mercados é exibida na seção "Registrar Resultado", THE RegisterResultSection SHALL ordenar os mercados de forma que `MapWinner` e `SeriesWinner` apareçam antes de `TopKills`, `MostDeaths` e `MostUtilityDamage`.
2. WHILE mercados de múltiplos mapas estão presentes, THE RegisterResultSection SHALL agrupar os mercados por tipo (Team_Markets primeiro, Player_Markets depois) mantendo a ordenação por número de mapa dentro de cada grupo.

### Requirement 3: Endpoint orientado a Users

**User Story:** Como desenvolvedor, quero que `GET /api/games/{id}/players` retorne dados baseados em `Users` em vez de `CS2Players`, para preparar a migração futura e garantir que o dropdown reflita os usuários reais da plataforma.

#### Acceptance Criteria

1. WHEN `GET /api/games/{id}/players` é chamado, THE GamesController SHALL retornar a lista de `Users` cujo `TeamId` corresponde a um dos dois times do jogo, em vez de consultar `CS2Players`.
2. THE GamesController SHALL retornar para cada jogador os campos: `id` (User.Id), `username` (User.Username), `teamName` (nome do CS2Team associado ao TeamId do User).
3. IF o jogo não for encontrado, THEN THE GamesController SHALL retornar HTTP 404 com código de erro `GAME_NOT_FOUND`.
4. IF nenhum usuário pertencer aos times do jogo, THEN THE GamesController SHALL retornar HTTP 200 com lista vazia.
5. THE GamesController SHALL manter o endpoint autenticado (`[Authorize]`) sem exigir papel de admin, preservando o comportamento atual de acesso.

### Requirement 4: Compatibilidade do frontend com o novo contrato de GamePlayer

**User Story:** Como desenvolvedor, quero que o frontend use o campo `username` retornado pelo novo endpoint, para exibir corretamente os nomes nos dropdowns e enviar o valor correto como `winningOption`.

#### Acceptance Criteria

1. THE RegisterResultSection SHALL usar o campo `username` do `GamePlayer` como valor e label do SELECT dropdown para Player_Markets.
2. WHEN o admin seleciona um jogador e submete o formulário, THE RegisterResultSection SHALL enviar `winningOption: username` (e não o `id` do usuário) para o endpoint de resultado, pois o backend valida o resultado pelo nickname/username do jogador.
3. THE RegisterResultSection SHALL exibir o `teamName` do jogador como informação auxiliar no dropdown (ex: `"PlayerName (TeamName)"`), para facilitar a identificação quando os dois times têm jogadores com nomes similares.

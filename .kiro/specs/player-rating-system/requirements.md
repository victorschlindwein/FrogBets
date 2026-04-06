# Documento de Requisitos

## Introdução

O sistema de rating de jogadores de CS2 da plataforma FrogBets permite que administradores cadastrem times e seus jogadores, registrem estatísticas individuais por partida e acompanhem um ranking de jogadores baseado em um rating calculado no estilo HLTV Rating 2.0 adaptado.

O rating é calculado a partir de cinco pilares extraídos das estatísticas de cada jogador por partida: KPR (kills por round), DPR (mortes por round), ADR (dano médio por round), KAST% (porcentagem de rounds com contribuição) e Impact (impacto aproximado). O score acumulado de cada jogador sobe ou desce conforme o rating obtido em cada partida registrada.

## Glossário

- **Rating_Calculator**: Componente responsável por calcular o rating de um jogador a partir das estatísticas de uma partida.
- **CS2_Player**: Entidade que representa um jogador de CS2 cadastrado na plataforma, distinto do `User` (usuário apostador).
- **CS2_Team**: Entidade que representa um time de CS2, composto por um ou mais `CS2_Player`.
- **Match_Stats**: Conjunto de estatísticas individuais de um `CS2_Player` em uma partida específica: kills, deaths, assists, dano total, rounds totais e KAST%.
- **Player_Rating**: Valor numérico calculado pelo `Rating_Calculator` para um `CS2_Player` em uma partida, baseado nas `Match_Stats`.
- **Player_Score**: Score acumulado de um `CS2_Player`, atualizado após cada partida com base no `Player_Rating` obtido.
- **Player_Ranking**: Lista ordenada de `CS2_Player` pelo `Player_Score` acumulado, exibida publicamente.
- **Admin**: Usuário da plataforma com `IsAdmin = true`, responsável por gerenciar times, jogadores e estatísticas.
- **KPR**: Kills Per Round — total de kills dividido pelo total de rounds da partida.
- **DPR**: Deaths Per Round — total de deaths dividido pelo total de rounds da partida.
- **ADR**: Average Damage per Round — dano total dividido pelo total de rounds da partida.
- **KAST**: Porcentagem de rounds onde o jogador teve Kill, Assist, Sobreviveu ou foi Trado (valor inteiro, ex: 75 para 75%).
- **Impact**: Métrica de impacto aproximada, calculada como `KPR + (assists_por_round * 0.4)`.

## Requisitos

### Requisito 1: Cadastro de Times

**User Story:** Como admin, quero cadastrar times de CS2, para que eu possa organizar os jogadores por equipe.

#### Critérios de Aceitação

1. THE Admin_Panel SHALL exibir um formulário para criação de `CS2_Team` com os campos: nome do time e logo (URL opcional).
2. WHEN o admin submete o formulário de criação de time com nome válido, THE Api SHALL persistir o `CS2_Team` no banco de dados e retornar o recurso criado com status HTTP 201.
3. IF o nome do time submetido já existir no banco de dados, THEN THE Api SHALL retornar HTTP 409 com código de erro `TEAM_NAME_ALREADY_EXISTS`.
4. IF o nome do time submetido estiver vazio ou ausente, THEN THE Api SHALL retornar HTTP 400 com código de erro `INVALID_TEAM_NAME`.
5. THE Admin_Panel SHALL listar todos os `CS2_Team` cadastrados com nome e logo.

---

### Requisito 2: Cadastro de Jogadores

**User Story:** Como admin, quero cadastrar jogadores de CS2 e associá-los a um time, para que eu possa registrar estatísticas individuais por partida.

#### Critérios de Aceitação

1. THE Admin_Panel SHALL exibir um formulário para criação de `CS2_Player` com os campos: nickname, nome real (opcional), time (`CS2_Team`) e foto (URL opcional).
2. WHEN o admin submete o formulário de criação de jogador com dados válidos, THE Api SHALL persistir o `CS2_Player` associado ao `CS2_Team` informado e retornar HTTP 201.
3. IF o nickname do jogador submetido já existir no banco de dados, THEN THE Api SHALL retornar HTTP 409 com código de erro `PLAYER_NICKNAME_ALREADY_EXISTS`.
4. IF o `CS2_Team` informado não existir, THEN THE Api SHALL retornar HTTP 404 com código de erro `TEAM_NOT_FOUND`.
5. IF o nickname ou o `CS2_Team` estiverem ausentes, THEN THE Api SHALL retornar HTTP 400 com código de erro `INVALID_PLAYER_DATA`.
6. THE Admin_Panel SHALL listar todos os `CS2_Player` cadastrados com nickname, time e `Player_Score` atual.

---

### Requisito 3: Registro de Estatísticas de Partida

**User Story:** Como admin, quero registrar as estatísticas individuais de cada jogador após uma partida, para que o sistema possa calcular e atualizar o rating de cada um.

#### Critérios de Aceitação

1. THE Admin_Panel SHALL exibir um formulário de registro de `Match_Stats` que permita selecionar um `Game` existente e informar, para cada `CS2_Player` participante: kills, deaths, assists, dano total, total de rounds e KAST%.
2. WHEN o admin submete as `Match_Stats` de um jogador para um `Game`, THE Api SHALL persistir as estatísticas e acionar o `Rating_Calculator`.
3. THE Rating_Calculator SHALL calcular o `Player_Rating` usando a fórmula: `Rating = 0.0073 * KAST + 0.3591 * KPR + (-0.5329) * DPR + 0.2372 * Impact + 0.0032 * ADR + 0.1587`, onde `Impact = KPR + (assists / rounds * 0.4)`.
4. WHEN o `Rating_Calculator` produz um `Player_Rating`, THE Api SHALL atualizar o `Player_Score` do `CS2_Player` somando o `Player_Rating` calculado ao score acumulado anterior.
5. IF `Match_Stats` para o mesmo `CS2_Player` e `Game` já existirem, THEN THE Api SHALL retornar HTTP 409 com código de erro `STATS_ALREADY_REGISTERED`.
6. IF o total de rounds informado for menor ou igual a zero, THEN THE Api SHALL retornar HTTP 400 com código de erro `INVALID_ROUNDS_COUNT`.
7. IF o KAST% informado estiver fora do intervalo de 0 a 100, THEN THE Api SHALL retornar HTTP 400 com código de erro `INVALID_KAST_VALUE`.
8. IF o `CS2_Player` ou o `Game` informados não existirem, THEN THE Api SHALL retornar HTTP 404 com código de erro `RESOURCE_NOT_FOUND`.

---

### Requisito 4: Cálculo do Rating (Rating_Calculator)

**User Story:** Como plataforma, quero que o rating de cada jogador seja calculado de forma consistente e determinística, para que o ranking reflita fielmente o desempenho individual.

#### Critérios de Aceitação

1. THE Rating_Calculator SHALL calcular KPR como `kills / rounds`.
2. THE Rating_Calculator SHALL calcular DPR como `deaths / rounds`.
3. THE Rating_Calculator SHALL calcular ADR como `total_damage / rounds`.
4. THE Rating_Calculator SHALL calcular Impact como `KPR + (assists / rounds * 0.4)`.
5. THE Rating_Calculator SHALL calcular o `Player_Rating` final como `0.0073 * KAST + 0.3591 * KPR + (-0.5329) * DPR + 0.2372 * Impact + 0.0032 * ADR + 0.1587`.
6. FOR ALL conjuntos válidos de `Match_Stats`, THE Rating_Calculator SHALL produzir o mesmo `Player_Rating` quando chamado múltiplas vezes com os mesmos dados de entrada (propriedade de determinismo/idempotência).
7. THE Rating_Calculator SHALL retornar um valor numérico de ponto flutuante com precisão de pelo menos 4 casas decimais.

---

### Requisito 5: Ranking de Jogadores

**User Story:** Como usuário da plataforma, quero visualizar o ranking de jogadores de CS2 ordenado pelo score acumulado, para que eu possa acompanhar o desempenho dos jogadores.

#### Critérios de Aceitação

1. THE Api SHALL expor um endpoint público `GET /api/players/ranking` que retorne a lista de `CS2_Player` ordenada por `Player_Score` decrescente.
2. THE Player_Ranking SHALL incluir para cada jogador: posição, nickname, nome do time, `Player_Score` acumulado e número de partidas registradas.
3. WHEN um `Player_Score` é atualizado, THE Player_Ranking SHALL refletir a nova ordenação na próxima consulta ao endpoint de ranking.
4. WHERE nenhum `CS2_Player` estiver cadastrado, THE Api SHALL retornar uma lista vazia com HTTP 200.
5. THE Frontend SHALL exibir o `Player_Ranking` em uma página acessível a todos os usuários autenticados.

---

### Requisito 6: Segurança e Controle de Acesso

**User Story:** Como plataforma, quero que apenas administradores possam gerenciar times, jogadores e estatísticas, para que dados críticos de rating não sejam manipulados por usuários comuns.

#### Critérios de Aceitação

1. WHEN uma requisição para criar ou editar `CS2_Team`, `CS2_Player` ou `Match_Stats` é recebida sem token JWT válido, THE Api SHALL retornar HTTP 401.
2. WHEN uma requisição para criar ou editar `CS2_Team`, `CS2_Player` ou `Match_Stats` é recebida com token JWT de usuário sem role de admin, THE Api SHALL retornar HTTP 403.
3. THE Api SHALL permitir acesso público (sem autenticação) apenas ao endpoint `GET /api/players/ranking`.

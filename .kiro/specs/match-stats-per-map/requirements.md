# Requirements Document

## Introduction

Atualmente, `MatchStats` registra as estatísticas de um jogador por série (`Game`), carregando o campo `Rounds` diretamente na entidade. Numa série com múltiplos mapas, todos os 10 jogadores de um mesmo mapa têm o mesmo valor de `Rounds`, tornando o campo redundante e desnaturando a granularidade dos dados.

Esta feature introduz a entidade `MapResult`, que representa um mapa específico dentro de uma série (identificado por `GameId` + `MapNumber`), e passa a ser a referência de `MatchStats`. O campo `Rounds` sai de `MatchStats` e passa a viver em `MapResult`. O rating continua sendo calculado por mapa, usando os `Rounds` do `MapResult` correspondente.

## Glossary

- **Game**: A série completa entre dois times (ex: melhor de 3 mapas).
- **MapResult**: Entidade que representa um mapa específico dentro de uma série, identificado por `GameId` + `MapNumber`, contendo o número de rounds jogados naquele mapa.
- **MatchStats**: Estatísticas de desempenho de um jogador em um mapa específico (referencia `MapResult`).
- **MapNumber**: Número ordinal do mapa dentro da série (1, 2, 3...).
- **Rounds**: Número total de rounds jogados em um mapa específico.
- **Rating**: Pontuação de desempenho calculada por mapa usando a fórmula HLTV 2.0 adaptada.
- **RatingCalculator**: Componente responsável por calcular o rating a partir de kills, deaths, assists, damage, rounds e KAST%.
- **Admin**: Usuário com `IsAdmin = true`, responsável por registrar resultados e estatísticas.
- **CS2Player**: Jogador de CS2 com score acumulado (`PlayerScore`) e contagem de partidas (`MatchesCount`).

## Requirements

### Requirement 1: Criação de MapResult

**User Story:** Como admin, quero registrar um mapa específico de uma série com seu número de rounds, para que as estatísticas dos jogadores possam ser associadas ao mapa correto.

#### Acceptance Criteria

1. WHEN um admin envia `GameId`, `MapNumber` e `Rounds` válidos, THE System SHALL criar um `MapResult` e retornar os dados criados com status 201.
2. THE System SHALL garantir que a combinação `(GameId, MapNumber)` seja única — não é possível registrar o mesmo mapa duas vezes na mesma série.
3. IF o `GameId` fornecido não corresponder a um `Game` existente, THEN THE System SHALL retornar erro `MAP_GAME_NOT_FOUND`.
4. IF o `MapNumber` for menor que 1, THEN THE System SHALL retornar erro `INVALID_MAP_NUMBER`.
5. IF o `Rounds` for menor ou igual a zero, THEN THE System SHALL retornar erro `INVALID_ROUNDS_COUNT`.
6. IF a combinação `(GameId, MapNumber)` já existir, THEN THE System SHALL retornar erro `MAP_ALREADY_REGISTERED`.

### Requirement 2: Refatoração de MatchStats para referenciar MapResult

**User Story:** Como admin, quero registrar as estatísticas de um jogador referenciando um mapa específico (MapResult), para que o desempenho por mapa seja rastreado com precisão.

#### Acceptance Criteria

1. WHEN um admin registra estatísticas de um jogador, THE System SHALL exigir `MapResultId` em vez de `GameId` + `Rounds`.
2. THE System SHALL remover o campo `Rounds` de `MatchStats`, pois esse valor passa a ser obtido do `MapResult` referenciado.
3. THE System SHALL garantir que a combinação `(PlayerId, MapResultId)` seja única — um jogador não pode ter duas entradas de stats para o mesmo mapa.
4. IF o `MapResultId` fornecido não corresponder a um `MapResult` existente, THEN THE System SHALL retornar erro `MAP_RESULT_NOT_FOUND`.
5. IF já existir um `MatchStats` para o mesmo `(PlayerId, MapResultId)`, THEN THE System SHALL retornar erro `STATS_ALREADY_REGISTERED`.

### Requirement 3: Cálculo de Rating por Mapa

**User Story:** Como sistema, quero calcular o rating de um jogador usando os rounds do MapResult referenciado, para que o rating reflita o desempenho real no mapa.

#### Acceptance Criteria

1. WHEN as estatísticas de um jogador são registradas, THE RatingCalculator SHALL calcular o rating usando os `Rounds` do `MapResult` associado ao `MatchStats`.
2. THE RatingCalculator SHALL aplicar a fórmula: `Rating = 0.0073×KAST + 0.3591×KPR + (−0.5329)×DPR + 0.2372×Impact + 0.0032×ADR + 0.1587`, onde KPR, DPR, ADR e Impact são normalizados pelos `Rounds` do `MapResult`.
3. IF o `KastPercent` for menor que 0 ou maior que 100, THEN THE System SHALL retornar erro `INVALID_KAST_VALUE`.
4. THE System SHALL acumular o rating calculado em `CS2Player.PlayerScore` e incrementar `CS2Player.MatchesCount` a cada registro de `MatchStats`.

### Requirement 4: Consulta de Estatísticas por Mapa

**User Story:** Como usuário, quero visualizar as estatísticas de um jogador separadas por mapa, para que eu possa analisar o desempenho em cada mapa individualmente.

#### Acceptance Criteria

1. WHEN uma consulta de stats de um jogador é realizada, THE System SHALL retornar as estatísticas agrupadas por `MapResult`, incluindo `MapNumber` e `Rounds` de cada mapa.
2. THE System SHALL incluir no retorno de cada `MatchStats` os campos: `MapResultId`, `MapNumber`, `Rounds` (do MapResult), `Kills`, `Deaths`, `Assists`, `TotalDamage`, `KastPercent` e `Rating`.
3. IF o jogador não possuir estatísticas registradas, THE System SHALL retornar uma lista vazia.

### Requirement 5: Migração de Dados Existentes

**User Story:** Como desenvolvedor, quero que os dados existentes de `MatchStats` sejam migrados para o novo modelo, para que não haja perda de histórico.

#### Acceptance Criteria

1. THE System SHALL criar uma migração EF Core que adiciona a tabela `MapResults` com colunas `Id`, `GameId`, `MapNumber`, `Rounds` e `CreatedAt`.
2. THE System SHALL criar uma migração EF Core que adiciona a coluna `MapResultId` em `MatchStats` e remove a coluna `Rounds`.
3. THE System SHALL garantir que a constraint de unicidade em `MatchStats` passe de `(PlayerId, GameId)` para `(PlayerId, MapResultId)`.

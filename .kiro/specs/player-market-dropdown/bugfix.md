# Bugfix Requirements Document

## Introduction

Na tela de detalhes de um jogo (`GameDetailPage`), ao criar uma aposta em mercados de jogadores (`TopKills`, `MostDeaths`, `MostUtilityDamage`), o campo de seleção de opção renderiza um `<input type="text">` que exige digitação manual do nome do jogador. O comportamento correto é exibir um `<select>` (dropdown) com os jogadores que participarão daquela série, no formato "Nickname - Nome do Time", restrito apenas aos jogadores dos dois times envolvidos no jogo.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN o usuário acessa um mercado de jogador (TopKills, MostDeaths ou MostUtilityDamage) em um jogo agendado THEN o sistema exibe um campo de texto livre para digitar o nome do jogador

1.2 WHEN o usuário tenta criar uma aposta em mercado de jogador THEN o sistema aceita qualquer string digitada como opção, sem validar se o jogador pertence ao jogo

### Expected Behavior (Correct)

2.1 WHEN o usuário acessa um mercado de jogador (TopKills, MostDeaths ou MostUtilityDamage) em um jogo agendado THEN o sistema SHALL exibir um dropdown com os jogadores dos dois times do jogo, no formato "Nickname - Nome do Time"

2.2 WHEN o usuário tenta criar uma aposta em mercado de jogador THEN o sistema SHALL permitir selecionar apenas jogadores que pertencem aos times (TeamA ou TeamB) daquela série específica

### Unchanged Behavior (Regression Prevention)

3.1 WHEN o usuário acessa um mercado de time (MapWinner ou SeriesWinner) em um jogo agendado THEN o sistema SHALL CONTINUE TO exibir um dropdown com as opções TeamA e TeamB do jogo

3.2 WHEN não há jogadores cadastrados para os times do jogo THEN o sistema SHALL CONTINUE TO renderizar o formulário de aposta sem travar, exibindo o dropdown vazio ou com mensagem adequada

3.3 WHEN o usuário cria uma aposta em qualquer mercado THEN o sistema SHALL CONTINUE TO enviar o campo `creatorOption` com o valor selecionado para `POST /api/bets`

# Bugfix Requirements Document

## Introduction

Na tela de "Estatísticas de Partida" do painel admin (`MatchStatsSection` em `AdminPage.tsx`), o dropdown de "Mapa" exibe a mensagem "Nenhum mapa registrado para este jogo." mesmo quando existem mapas registrados via `MapResults` para o jogo selecionado.

O problema ocorre porque o carregamento dos mapas (`getMapResultsByGame`) e dos jogadores (`getGamePlayers`) é feito em paralelo via `Promise.all`. Quando qualquer uma das duas chamadas falha — em particular `getGamePlayers`, que retorna lista vazia quando nenhum usuário tem `TeamId` configurado para os times do jogo — o bloco `catch` do `Promise.all` descarta silenciosamente os resultados de ambas as chamadas, zerando `mapResults` mesmo que os mapas tenham sido carregados com sucesso.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN o admin seleciona um jogo na seção "Estatísticas de Partida" E `getGamePlayers` lança um erro THEN o sistema descarta os mapas retornados por `getMapResultsByGame` e exibe "Nenhum mapa registrado para este jogo."

1.2 WHEN o admin seleciona um jogo na seção "Estatísticas de Partida" E `getMapResultsByGame` retorna mapas com sucesso mas `getGamePlayers` falha THEN o sistema seta `mapResults = []` e `players = []` no bloco `catch`, ignorando os dados de mapas já obtidos

1.3 WHEN o admin seleciona um jogo na seção "Estatísticas de Partida" E ambas as chamadas do `Promise.all` falham THEN o sistema não exibe nenhuma mensagem de erro ao usuário, apenas mostra "Nenhum mapa registrado para este jogo."

### Expected Behavior (Correct)

2.1 WHEN o admin seleciona um jogo na seção "Estatísticas de Partida" E `getMapResultsByGame` retorna mapas com sucesso THEN o sistema SHALL exibir o dropdown de "Mapa" populado com os mapas retornados, independentemente do resultado de `getGamePlayers`

2.2 WHEN o admin seleciona um jogo na seção "Estatísticas de Partida" E `getGamePlayers` falha mas `getMapResultsByGame` retorna mapas THEN o sistema SHALL exibir o dropdown de "Mapa" normalmente e exibir uma mensagem de erro apenas para o campo de jogador

2.3 WHEN o admin seleciona um jogo na seção "Estatísticas de Partida" E `getMapResultsByGame` falha THEN o sistema SHALL exibir uma mensagem de erro indicando falha ao carregar os mapas

### Unchanged Behavior (Regression Prevention)

3.1 WHEN o admin seleciona um jogo que não possui nenhum mapa registrado via `MapResults` THEN o sistema SHALL CONTINUE TO exibir a mensagem "Nenhum mapa registrado para este jogo."

3.2 WHEN o admin seleciona um jogo e ambas as chamadas retornam dados com sucesso THEN o sistema SHALL CONTINUE TO exibir o dropdown de "Mapa" e o dropdown de "Jogador" normalmente

3.3 WHEN o admin seleciona um jogo e depois troca para outro jogo THEN o sistema SHALL CONTINUE TO limpar os estados de mapas, jogadores e seleções anteriores ao iniciar o novo carregamento

3.4 WHEN o admin seleciona um mapa e depois seleciona um jogador e preenche as estatísticas THEN o sistema SHALL CONTINUE TO submeter as estatísticas corretamente via `POST /api/players/{playerId}/stats`

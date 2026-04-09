# Bugfix Requirements Document

## Introdução

A plataforma FrogBets possui múltiplos bugs não identificados que impedem o funcionamento correto do fluxo completo de apostas. Este documento define os requisitos para criar um teste E2E abrangente que cubra todo o fluxo principal da plataforma, desde o registro de usuários até a liquidação de apostas, permitindo identificar e corrigir todos os bugs existentes.

O fluxo completo inclui: registro de 4 usuários, criação de 2 times, adição de 2 jogadores por time, criação de jogo entre os times, abertura de mercados, registro de apostas, verificação de dropdowns de jogadores, cobertura de apostas, início e encerramento do jogo, e verificação da liquidação correta das apostas.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN executando o fluxo completo E2E (registro de usuários → criação de times → adição de jogadores → criação de jogo → apostas → liquidação) THEN o sistema apresenta múltiplos bugs não identificados que impedem o fluxo de completar com sucesso

1.2 WHEN verificando os dropdowns de mercados de apostas após adicionar jogadores aos times THEN o sistema pode não exibir corretamente os 4 jogadores (2 de cada time recém-adicionado)

1.3 WHEN tentando cobrir apostas criadas por outro usuário THEN o sistema pode falhar na cobertura ou não atualizar corretamente o estado da aposta

1.4 WHEN o admin inicia e encerra um jogo marcando o time vencedor THEN o sistema pode não liquidar corretamente as apostas ou não atualizar os saldos dos usuários

1.5 WHEN registrando apostas em mercados de jogadores (TopKills, MostDeaths, MostUtilityDamage) THEN o sistema pode não exibir as opções corretas ou falhar ao processar a aposta

1.6 WHEN múltiplos usuários interagem simultaneamente com o sistema (criando apostas, cobrindo apostas) THEN o sistema pode apresentar race conditions ou inconsistências de estado

### Expected Behavior (Correct)

2.1 WHEN executando o fluxo completo E2E (registro de usuários → criação de times → adição de jogadores → criação de jogo → apostas → liquidação) THEN o sistema SHALL completar todas as etapas com sucesso sem erros

2.2 WHEN verificando os dropdowns de mercados de apostas após adicionar 2 jogadores ao TeamA e 2 jogadores ao TeamB THEN o sistema SHALL exibir exatamente os 4 jogadores corretos nas opções de apostas de mercados de jogador

2.3 WHEN um usuário cobre uma aposta criada por outro usuário THEN o sistema SHALL processar a cobertura com sucesso, atualizar o status da aposta para "Active", reservar o saldo do cobrador, e remover a aposta do marketplace

2.4 WHEN o admin inicia um jogo (status → InProgress), encerra o jogo (status → Finished) e marca os resultados dos mercados THEN o sistema SHALL liquidar todas as apostas corretamente, creditando 2× o valor para os vencedores e atualizando WinsCount/LossesCount

2.5 WHEN registrando apostas em mercados de jogadores (TopKills, MostDeaths, MostUtilityDamage) THEN o sistema SHALL exibir as opções no formato "<nickname>" e "NOT_<nickname>" para todos os jogadores dos dois times

2.6 WHEN múltiplos usuários interagem simultaneamente com o sistema THEN o sistema SHALL manter a consistência de dados usando transações Serializable e prevenir race conditions

### Unchanged Behavior (Regression Prevention)

3.1 WHEN executando testes E2E existentes (auth, bets, games, marketplace, teams, leaderboard, players-ranking, navbar, dashboard) THEN o sistema SHALL CONTINUE TO passar em todos os testes sem regressões

3.2 WHEN um usuário tenta cobrir sua própria aposta THEN o sistema SHALL CONTINUE TO retornar erro "CANNOT_COVER_OWN_BET"

3.3 WHEN um usuário tenta criar uma aposta em um mercado onde já possui aposta ativa THEN o sistema SHALL CONTINUE TO retornar erro "DUPLICATE_BET_ON_MARKET"

3.4 WHEN um usuário tenta criar uma aposta com saldo insuficiente THEN o sistema SHALL CONTINUE TO retornar erro "INSUFFICIENT_BALANCE"

3.5 WHEN um usuário tenta cancelar uma aposta que já foi coberta (status Active) THEN o sistema SHALL CONTINUE TO retornar erro "CANNOT_CANCEL_ACTIVE_BET"

3.6 WHEN o invariante de saldo (VirtualBalance + ReservedBalance = constante) é verificado após qualquer operação financeira THEN o sistema SHALL CONTINUE TO manter o invariante preservado

3.7 WHEN um usuário não autenticado tenta acessar rotas protegidas THEN o sistema SHALL CONTINUE TO redirecionar para /login

3.8 WHEN um usuário não-admin tenta acessar endpoints administrativos THEN o sistema SHALL CONTINUE TO retornar 403 Forbidden

# Bugfix Requirements Document

## Introduction

Dois bugs afetam funcionalidades centrais da plataforma FrogBets:

**Bug 1 — Marketplace tela branca:** Ao acessar `/marketplace`, a página renderiza completamente em branco. O `MarketplaceController` retorna `BetDto` com campos planos (`MarketType`, `MapNumber`, `GameId`), mas a interface `MarketplaceBet` no frontend espera um objeto aninhado `market: { type, mapNumber, gameId }`. Como `bet.market` é `undefined`, a chamada `marketLabel(bet.market)` lança uma exceção que quebra o render da página inteira.

**Bug 2 — Apostas por jogador não listam todos os jogadores:** Na `GameDetailPage`, os dropdowns de mercados de jogador (TopKills, MostDeaths, MostUtilityDamage) exibem apenas jogadores de um dos times. O endpoint `GET /api/games/{id}/players` busca `Users` onde `u.TeamId` está nos times do jogo, mas os jogadores de CS2 são entidades `CS2Player` separadas. Apenas usuários com `TeamId` vinculado aparecem; `CS2Player` sem `User` correspondente ficam de fora.

---

## Bug Analysis

### Current Behavior (Defect)

**Bug 1 — Marketplace:**

1.1 WHEN o usuário acessa a página `/marketplace` e a API retorna apostas pendentes THEN o sistema renderiza uma tela completamente branca sem nenhum conteúdo visível

1.2 WHEN `marketLabel(bet.market)` é chamada com `bet.market` sendo `undefined` (pois a API retorna campos planos, não o objeto aninhado `market`) THEN o sistema lança uma exceção de runtime que interrompe o render do componente React

**Bug 2 — Jogadores no dropdown:**

1.3 WHEN o usuário acessa a `GameDetailPage` de um jogo cujos dois times possuem `CS2Player` cadastrados THEN o sistema exibe no dropdown de apostas de jogador apenas os jogadores de um dos times (aqueles cujos `User` têm `TeamId` vinculado)

1.4 WHEN o endpoint `GET /api/games/{id}/players` é chamado THEN o sistema busca `Users` onde `u.TeamId` está nos times do jogo, ignorando entidades `CS2Player` que não possuem `User` correspondente

### Expected Behavior (Correct)

**Bug 1 — Marketplace:**

2.1 WHEN o usuário acessa a página `/marketplace` e a API retorna apostas pendentes THEN o sistema SHALL exibir a lista de apostas corretamente, com mercado, opção e valor de cada aposta

2.2 WHEN a API retorna `BetDto` com campos planos (`marketType`, `mapNumber`, `gameId`) THEN o sistema SHALL mapear esses campos para o formato aninhado `market: { type, mapNumber, gameId }` esperado pelo componente, sem lançar exceções

**Bug 2 — Jogadores no dropdown:**

2.3 WHEN o usuário acessa a `GameDetailPage` de um jogo THEN o sistema SHALL exibir no dropdown de apostas de jogador todos os `CS2Player` dos dois times participantes do jogo

2.4 WHEN o endpoint `GET /api/games/{id}/players` é chamado THEN o sistema SHALL buscar entidades `CS2Player` onde `p.TeamId` está nos times do jogo, retornando `id`, `nickname` e `teamName` de cada jogador

### Unchanged Behavior (Regression Prevention)

3.1 WHEN o usuário acessa `/marketplace` e não há apostas pendentes de outros usuários THEN o sistema SHALL CONTINUE TO exibir a mensagem "Nenhuma aposta disponível para cobertura"

3.2 WHEN o usuário cobre uma aposta no marketplace THEN o sistema SHALL CONTINUE TO remover a aposta da lista após cobertura bem-sucedida

3.3 WHEN o usuário acessa a `GameDetailPage` de um jogo com mercados de time (MapWinner, SeriesWinner) THEN o sistema SHALL CONTINUE TO exibir os nomes dos dois times como opções de aposta

3.4 WHEN o usuário acessa a `GameDetailPage` de um jogo com status diferente de `Scheduled` THEN o sistema SHALL CONTINUE TO exibir os mercados sem o formulário de aposta

3.5 WHEN o endpoint `GET /api/games/{id}/players` é chamado para um jogo inexistente THEN o sistema SHALL CONTINUE TO retornar 404 com código `GAME_NOT_FOUND`

3.6 WHEN a seção de Trocas de Jogadores no marketplace é carregada THEN o sistema SHALL CONTINUE TO exibir os jogadores disponíveis para troca agrupados por time

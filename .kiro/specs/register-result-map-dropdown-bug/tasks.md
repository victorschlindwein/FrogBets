                                                                                                                                  # Register Result Map Dropdown Bug — Tasks

## Tasks

- [x] 1. Escrever testes exploratórios (bug condition checking)
  - [x] 1.1 Criar arquivo de teste `AdminPage.matchstats.bugcondition.test.tsx`
  - [x] 1.2 Mockar `getMapResultsByGame` para retornar mapas e `getGamePlayers` para rejeitar
  - [x] 1.3 Verificar que no código ATUAL (não corrigido) `mapResults` é zerado — confirmar o bug
  - [x] 1.4 Rodar os testes e confirmar que falham da forma esperada (demonstrando o bug)

- [x] 2. Aplicar o fix em `MatchStatsSection`
  - [x] 2.1 Substituir `Promise.all` por `Promise.allSettled` no `useEffect` de `MatchStatsSection`
  - [x] 2.2 Tratar o resultado de `getMapResultsByGame` independentemente (fulfilled → setMapResults, rejected → setMapResults([]))
  - [x] 2.3 Tratar o resultado de `getGamePlayers` independentemente (fulfilled → setPlayers, rejected → setPlayers([]))
  - [x] 2.4 Verificar que os testes exploratórios do passo 1 agora passam com o código corrigido

- [x] 3. Escrever testes de preservation checking
  - [x] 3.1 Criar arquivo de teste `AdminPage.matchstats.preservation.test.tsx`
  - [x] 3.2 Testar Property 2: ambas as chamadas com sucesso → comportamento idêntico ao original (mapResults e players populados)
  - [x] 3.3 Testar Property 2: `getMapResultsByGame` retorna `[]` → mensagem "Nenhum mapa registrado para este jogo." continua aparecendo
  - [x] 3.4 Testar Property 2: `statsGameId` vazio → estados limpos (mapResults=[], players=[], mapResultId='')
  - [x] 3.5 Testar Property 2: ambas as chamadas falham → mapResults=[] e players=[] (igual ao original)
  - [x] 3.6 Rodar todos os testes e confirmar que passam

- [x] 4. Validação final
  - [x] 4.1 Rodar `npx tsc --noEmit` no frontend e confirmar zero erros de tipo
  - [x] 4.2 Rodar `npm run test` e confirmar que todos os testes passam

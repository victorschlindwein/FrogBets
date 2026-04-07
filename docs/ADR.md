# FrogBets — Architecture Decision Records (ADR)

Registro das decisões arquiteturais relevantes tomadas durante o desenvolvimento do FrogBets. Cada ADR documenta o contexto, a decisão e as consequências.

---

## ADR-001 — Plataforma fechada por convite

**Status:** Aceito

**Contexto:**
A plataforma é destinada a um grupo fechado de amigos. Registro aberto criaria risco de uso indevido e dificultaria o controle de quem participa.

**Decisão:**
O único caminho para criar uma conta é via `POST /api/auth/register` com um `InviteToken` válido gerado por um admin. Não existe e não deve existir endpoint de registro aberto.

**Consequências:**
- O admin precisa gerar convites manualmente para cada novo usuário
- Tokens de convite são de uso único e têm expiração configurável
- O primeiro admin deve ser inserido diretamente no banco via SQL

---

## ADR-002 — JWT com blocklist para logout real

**Status:** Aceito

**Contexto:**
JWT é stateless por natureza — uma vez emitido, é válido até expirar. Isso impede logout real sem alguma forma de revogação.

**Decisão:**
Ao fazer logout, o `jti` (JWT ID) do token é persistido na tabela `RevokedTokens` e em um cache em memória (`TokenBlocklist`). Cada request verifica o cache antes de processar.

**Consequências:**
- Logout é efetivo imediatamente, mesmo antes do token expirar
- O cache em memória evita consulta ao banco em cada request
- A tabela `RevokedTokens` cresce com o tempo — um `IHostedService` faz limpeza periódica dos tokens já expirados
- Em caso de restart da API, o cache é recarregado do banco no primeiro uso

---

## ADR-003 — Transações Serializable para operações de saldo

**Status:** Aceito

**Contexto:**
Apostas envolvem movimentação de saldo virtual. Dois usuários podem tentar cobrir a mesma aposta simultaneamente, ou um usuário pode tentar criar múltiplas apostas ao mesmo tempo, causando race conditions.

**Decisão:**
Toda operação que altera `VirtualBalance` ou `ReservedBalance` usa transação com `IsolationLevel.Serializable`. O invariante `VirtualBalance + ReservedBalance = constante` deve ser preservado em toda operação.

**Consequências:**
- Elimina race conditions em operações financeiras
- Pode causar serialization failures sob alta concorrência — aceitável dado o volume esperado (grupo pequeno de amigos)
- Testes com InMemory database ignoram o nível de isolamento (configurado via `ConfigureWarnings`)

---

## ADR-004 — IsTeamLeader não está no JWT

**Status:** Aceito

**Contexto:**
O papel de líder de time pode mudar sem que o usuário faça logout e login novamente. Se `IsTeamLeader` estivesse no JWT, um usuário removido do papel de líder continuaria tendo acesso às funcionalidades de líder até o token expirar.

**Decisão:**
`IsTeamLeader` não é incluído no JWT. Endpoints que requerem papel de líder consultam o banco diretamente para verificar o status atual.

**Consequências:**
- Mudanças de papel de líder têm efeito imediato
- Uma consulta extra ao banco por request que requer verificação de líder
- Contrasta com `isAdmin`, que está no JWT — admins raramente mudam de papel e a expiração de 60 min é aceitável

---

## ADR-005 — Mercados gerados automaticamente ao criar um jogo

**Status:** Aceito

**Contexto:**
Cada jogo de CS2 tem um conjunto previsível de mercados de apostas baseado no número de mapas. Criar mercados manualmente seria trabalhoso e propenso a erros.

**Decisão:**
Ao criar um `Game` com N mapas, o sistema gera automaticamente:
- N × {MapWinner, TopKills, MostDeaths, MostUtilityDamage} (mercados por mapa)
- 1 × SeriesWinner (mercado de série)

**Consequências:**
- Admin só precisa informar os times, data e número de mapas
- Todos os mercados são criados com status `Open` e ficam disponíveis para apostas imediatamente
- Não é possível criar mercados customizados fora desse conjunto predefinido

---

## ADR-006 — Opção do cobrador atribuída automaticamente

**Status:** Aceito

**Contexto:**
Em apostas P2P, a opção do cobrador é sempre o oposto da opção do criador. Permitir que o cobrador escolha qualquer opção abriria espaço para apostas inválidas (dois usuários apostando na mesma opção).

**Decisão:**
A `CovererOption` é calculada automaticamente pelo sistema ao cobrir uma aposta:
- Mercados de time: `TeamA` ↔ `TeamB`
- Mercados de jogador: `"nickname"` ↔ `"NOT_nickname"`

**Consequências:**
- Impossível criar apostas onde ambos os lados apostam na mesma opção
- Interface simplificada — o cobrador só precisa confirmar a cobertura, não escolher opção

---

## ADR-007 — Rating HLTV 2.0 adaptado por mapa

**Status:** Aceito

**Contexto:**
O sistema precisa de uma métrica de performance individual para jogadores de CS2. O HLTV Rating 2.0 é o padrão da indústria para avaliar performance em CS2.

**Decisão:**
Usar a fórmula HLTV Rating 2.0 adaptada:
```
Rating = 0.0073×KAST + 0.3591×KPR + (−0.5329)×DPR + 0.2372×Impact + 0.0032×ADR + 0.1587
```
O rating é calculado por mapa (não por série), pois o desempenho de um jogador pode variar significativamente entre mapas. O `PlayerScore` acumulado em `CS2Player` é a soma dos ratings de todos os mapas jogados.

**Consequências:**
- Necessidade de registrar estatísticas por mapa, não por série
- Introdução da entidade `MapResult` para separar os rounds (que são por mapa) das stats do jogador
- `MatchStats` referencia `MapResultId` em vez de `GameId` diretamente

---

## ADR-008 — MapResult como entidade separada para rounds

**Status:** Aceito

**Contexto:**
O número de rounds de um mapa é uma propriedade do mapa, não do jogador. Na modelagem original, `Rounds` estava em `MatchStats`, o que causava redundância: todos os 10 jogadores de um mesmo mapa teriam o mesmo valor de `Rounds`.

**Decisão:**
Criar a entidade `MapResult` com `(GameId, MapNumber, Rounds)`. `MatchStats` passa a referenciar `MapResultId`. O campo `Rounds` é removido de `MatchStats` e obtido via join com `MapResult`.

**Consequências:**
- Eliminação de redundância de dados
- Fluxo de inserção de stats muda para: criar MapResult → registrar stats por jogador referenciando o MapResult
- Constraint de unicidade em `MatchStats` muda de `(PlayerId, GameId)` para `(PlayerId, MapResultId)`, permitindo múltiplas entradas por jogador por série (uma por mapa)
- Migração necessária para dados existentes

---

## ADR-009 — Auditoria automática via Middleware

**Status:** Aceito

**Contexto:**
Operações administrativas e financeiras precisam ser rastreáveis. Adicionar logging manualmente em cada endpoint seria trabalhoso e fácil de esquecer.

**Decisão:**
`AuditMiddleware` intercepta automaticamente todas as requisições de escrita (POST, PATCH, PUT, DELETE) e persiste um `AuditLog` com actor, ação, recurso, IP e status code. Endpoints marcados com `[AllowAnonymous]` também são auditados (actor = "anonymous").

**Consequências:**
- Cobertura automática de todas as operações de escrita sem código adicional nos controllers
- Logs crescem com o tempo — `AuditLogCleanupService` faz limpeza diária baseada em `AUDIT_LOG_RETENTION_DAYS`
- Pequeno overhead por request de escrita (INSERT no banco)

---

## ADR-010 — Marketplace de trocas controlado por líderes

**Status:** Aceito

**Contexto:**
Times precisam de um mecanismo para trocar membros entre si de forma organizada, sem que qualquer usuário possa mover outros usuários arbitrariamente.

**Decisão:**
Apenas líderes de time podem marcar membros como disponíveis para troca (`TradeListing`) e criar/aceitar ofertas (`TradeOffer`). Admins podem fazer trocas diretas sem necessidade de oferta. Aceitar uma oferta troca os `TeamId`s dos dois membros, remove os listings e cancela outras ofertas pendentes envolvendo esses membros.

**Consequências:**
- Processo de troca requer consentimento de ambos os líderes (exceto troca direta por admin)
- Um usuário só pode ter um `TradeListing` ativo por vez
- Ao mover um usuário de time (por qualquer meio), `IsTeamLeader` é automaticamente removido e o `TradeListing` é cancelado

---

## ADR-011 — Frontend armazena JWT em sessionStorage

**Status:** Aceito

**Contexto:**
JWTs podem ser armazenados em `localStorage` ou `sessionStorage`. `localStorage` persiste entre abas e sessões do browser; `sessionStorage` é isolado por aba e limpo ao fechar.

**Decisão:**
O token é armazenado em `sessionStorage` com a chave `frogbets_token`.

**Consequências:**
- Ao fechar o browser/aba, o usuário é deslogado automaticamente
- Múltiplas abas não compartilham sessão — cada aba precisa fazer login separadamente
- Reduz (mas não elimina) o risco de XSS roubar o token persistido

---

## ADR-012 — Nginx unprivileged para o container do frontend

**Status:** Aceito

**Contexto:**
O AWS ECS Fargate não permite containers rodando como root. A imagem padrão `nginx:alpine` escuta na porta 80, que requer root.

**Decisão:**
Usar `nginxinc/nginx-unprivileged:alpine` para o container do frontend. Esta imagem escuta na porta 8080 por padrão e não requer root.

**Consequências:**
- Container do frontend escuta na porta 8080 (não 80)
- Target group do ALB aponta para porta 8080
- Compatível com ECS Fargate sem configurações especiais de segurança

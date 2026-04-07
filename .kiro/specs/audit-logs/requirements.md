# Documento de Requisitos — Logs de Auditoria

## Introdução

O FrogBets está crescendo para um modelo com múltiplos administradores e líderes de time. Para garantir rastreabilidade e responsabilização, é necessário registrar automaticamente todas as ações relevantes realizadas por usuários autenticados na API — especialmente operações de escrita (criação, modificação, exclusão). Os logs devem identificar quem fez o quê, quando, em qual recurso e com qual resultado, permitindo que administradores consultem o histórico de ações da plataforma.

## Glossário

- **AuditLog**: Registro imutável de uma ação realizada por um usuário autenticado na API.
- **AuditLog_Service**: Serviço responsável por persistir entradas de log de auditoria.
- **Audit_Middleware**: Componente ASP.NET Core que intercepta requisições HTTP e grava logs de auditoria após a execução da action.
- **Actor**: Usuário autenticado que realizou a ação. Pode ser `null` para requisições anônimas.
- **Action**: Identificador semântico da operação realizada (ex: `invites.create`, `games.start`, `bets.cancel`).
- **Resource**: Entidade afetada pela ação, composta por tipo e identificador (ex: `invite:3fa85f64`, `game:abc123`).
- **Outcome**: Resultado da operação — `Success` (2xx), `Failure` (4xx/5xx).
- **Admin**: Usuário com `IsAdmin = true` no banco de dados.
- **Team_Leader**: Usuário com `IsTeamLeader = true` no banco de dados.

---

## Requisitos

### Requisito 1: Registro automático de ações de escrita

**User Story:** Como administrador, quero que todas as operações de escrita (POST, PATCH, PUT, DELETE) realizadas por usuários autenticados sejam registradas automaticamente, para que eu possa rastrear quem foi responsável por cada mudança na plataforma.

#### Critérios de Aceitação

1. WHEN uma requisição HTTP com método POST, PATCH, PUT ou DELETE é processada pela API e o usuário está autenticado, THE Audit_Middleware SHALL persistir uma entrada de AuditLog contendo: identificador do actor, nome de usuário do actor, action, resource type, resource id (quando disponível), HTTP method, rota, HTTP status code resultante, timestamp UTC e endereço IP do cliente.
2. WHEN uma requisição HTTP com método GET é processada pela API, THE Audit_Middleware SHALL NOT registrar uma entrada de AuditLog (leituras não são auditadas).
3. WHEN uma requisição é processada por um endpoint marcado como `[AllowAnonymous]` e o usuário não está autenticado, THE Audit_Middleware SHALL registrar a entrada de AuditLog com `ActorId = null` e `ActorUsername = "anonymous"`.
4. IF a persistência do AuditLog falhar, THEN THE Audit_Middleware SHALL registrar o erro no logger de aplicação e continuar o processamento normal da requisição sem retornar erro ao cliente.
5. THE AuditLog_Service SHALL persistir entradas de AuditLog de forma assíncrona, sem bloquear a resposta HTTP ao cliente.

---

### Requisito 2: Estrutura e campos do AuditLog

**User Story:** Como administrador, quero que cada log de auditoria contenha informações suficientes para entender o contexto completo da ação, para que eu possa investigar incidentes sem precisar cruzar múltiplas fontes de dados.

#### Critérios de Aceitação

1. THE AuditLog_Service SHALL armazenar cada entrada com os campos: `Id` (Guid, PK), `ActorId` (Guid?, FK para User nullable), `ActorUsername` (string, máx 100), `Action` (string, máx 100), `ResourceType` (string?, máx 50), `ResourceId` (string?, máx 100), `HttpMethod` (string, máx 10), `Route` (string, máx 200), `StatusCode` (int), `IpAddress` (string?, máx 45), `OccurredAt` (DateTime UTC), `Details` (string?, máx 1000).
2. THE AuditLog_Service SHALL garantir que entradas de AuditLog sejam imutáveis após a inserção — nenhuma operação de UPDATE ou DELETE é permitida sobre a tabela `AuditLogs`.
3. WHEN o campo `Details` é fornecido, THE AuditLog_Service SHALL truncar o valor para no máximo 1000 caracteres antes de persistir.
4. THE AuditLog_Service SHALL indexar a tabela `AuditLogs` por `ActorId`, `Action` e `OccurredAt` para suportar consultas eficientes.

---

### Requisito 3: Mapeamento semântico de actions

**User Story:** Como administrador, quero que cada ação registrada tenha um identificador semântico legível (ex: `invites.create`), para que eu possa filtrar e entender os logs sem precisar interpretar métodos HTTP e rotas.

#### Critérios de Aceitação

1. THE Audit_Middleware SHALL mapear cada combinação de método HTTP e rota para um `Action` semântico no formato `<recurso>.<operação>` (ex: `invites.create`, `games.start`, `bets.cancel`, `users.promote`, `trades.direct_swap`).
2. WHEN uma rota não possui mapeamento semântico definido, THE Audit_Middleware SHALL usar o valor `"<METHOD> <route_template>"` como fallback para o campo `Action` (ex: `"POST /api/auth/register"`).
3. THE Audit_Middleware SHALL extrair o `ResourceId` a partir dos route values da requisição quando um parâmetro `{id}` ou `{userId}` ou `{teamId}` estiver presente na rota.

---

### Requisito 4: Endpoint de consulta de logs (admin)

**User Story:** Como administrador, quero consultar os logs de auditoria com filtros, para que eu possa investigar ações específicas de um usuário ou em um período de tempo.

#### Critérios de Aceitação

1. WHEN um admin realiza GET `/api/audit-logs`, THE Audit_Middleware SHALL NOT registrar essa consulta como uma entrada de AuditLog (leituras não são auditadas, conforme Requisito 1.2).
2. WHEN um admin realiza GET `/api/audit-logs` com parâmetros de filtro opcionais (`actorId`, `action`, `from`, `to`, `page`, `pageSize`), THE AuditLog_Service SHALL retornar uma lista paginada de entradas de AuditLog ordenadas por `OccurredAt` decrescente.
3. IF um usuário não-admin realiza GET `/api/audit-logs`, THEN THE System SHALL retornar HTTP 403 com código de erro `FORBIDDEN`.
4. THE AuditLog_Service SHALL limitar o `pageSize` máximo a 100 entradas por página, usando 20 como valor padrão quando não especificado.
5. WHERE o parâmetro `from` é fornecido sem `to`, THE AuditLog_Service SHALL retornar todos os logs a partir de `from` até o momento atual.

---

### Requisito 5: Cobertura de ações auditadas

**User Story:** Como administrador, quero que todas as ações de modificação da plataforma sejam cobertas pelos logs, para que nenhuma mudança relevante passe sem rastreamento.

#### Critérios de Aceitação

1. THE Audit_Middleware SHALL registrar ações de auditoria para todos os endpoints de escrita dos controllers: `AuthController`, `BetsController`, `GamesController`, `InvitesController`, `PlayersController`, `TeamsController`, `TradesController` e `UsersController`.
2. WHEN um admin cria um convite via POST `/api/invites`, THE Audit_Middleware SHALL registrar `Action = "invites.create"` com `ResourceType = "invite"`.
3. WHEN um admin finaliza um jogo via POST `/api/games/{id}/results`, THE Audit_Middleware SHALL registrar `Action = "games.register_result"` com `ResourceType = "game"` e `ResourceId` igual ao `{id}` da rota.
4. WHEN um admin ou líder move um usuário de time via PATCH `/api/users/{id}/team`, THE Audit_Middleware SHALL registrar `Action = "users.move_team"` com `ResourceType = "user"` e `ResourceId` igual ao `{id}` da rota.
5. WHEN um usuário cria uma aposta via POST `/api/bets`, THE Audit_Middleware SHALL registrar `Action = "bets.create"` com `ResourceType = "bet"`.
6. WHEN um usuário cobre uma aposta via POST `/api/bets/{id}/cover`, THE Audit_Middleware SHALL registrar `Action = "bets.cover"` com `ResourceType = "bet"` e `ResourceId` igual ao `{id}` da rota.
7. WHEN um usuário cancela uma aposta via DELETE `/api/bets/{id}`, THE Audit_Middleware SHALL registrar `Action = "bets.cancel"` com `ResourceType = "bet"` e `ResourceId` igual ao `{id}` da rota.
8. WHEN um admin promove um usuário via POST `/api/users/{id}/promote`, THE Audit_Middleware SHALL registrar `Action = "users.promote"` com `ResourceType = "user"` e `ResourceId` igual ao `{id}` da rota.
9. WHEN um admin rebaixa um usuário via POST `/api/users/{id}/demote`, THE Audit_Middleware SHALL registrar `Action = "users.demote"` com `ResourceType = "user"` e `ResourceId` igual ao `{id}` da rota.
10. WHEN um admin realiza uma troca direta via POST `/api/trades/direct`, THE Audit_Middleware SHALL registrar `Action = "trades.direct_swap"` com `ResourceType = "trade"`.

---

### Requisito 6: Retenção e limpeza de logs

**User Story:** Como administrador, quero que logs antigos sejam removidos automaticamente após um período configurável, para que o banco de dados não cresça indefinidamente.

#### Critérios de Aceitação

1. THE AuditLog_Service SHALL suportar uma política de retenção configurável via variável de ambiente `AUDIT_LOG_RETENTION_DAYS`, com valor padrão de 90 dias.
2. WHEN o serviço de limpeza é executado, THE AuditLog_Service SHALL remover todas as entradas de AuditLog com `OccurredAt` anterior ao limite de retenção calculado como `DateTime.UtcNow - AUDIT_LOG_RETENTION_DAYS`.
3. THE System SHALL executar a limpeza de logs expirados uma vez por dia via `IHostedService` em background, sem impactar a disponibilidade da API.
4. IF a limpeza falhar, THEN THE System SHALL registrar o erro no logger de aplicação e tentar novamente na próxima execução agendada.

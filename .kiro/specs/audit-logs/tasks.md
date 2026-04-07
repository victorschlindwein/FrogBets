# Plano de Implementação: Audit Logs

## Overview

Implementação incremental do sistema de audit logs para o FrogBets usando ASP.NET Core 8, EF Core 8 e PostgreSQL. Cada tarefa constrói sobre a anterior, terminando com a integração completa no pipeline HTTP.

## Tasks

- [x] 1. Criar entidade de domínio AuditLog
  - Criar `src/FrogBets.Domain/Entities/AuditLog.cs` com todos os campos definidos no design
  - Incluir navigation property `Actor` (nullable) para `User`
  - Campos: `Id`, `ActorId`, `ActorUsername`, `Action`, `ResourceType`, `ResourceId`, `HttpMethod`, `Route`, `StatusCode`, `IpAddress`, `OccurredAt`, `Details`
  - _Requirements: 2.1_

- [x] 2. Configurar DbContext e criar migration
  - [x] 2.1 Adicionar `DbSet<AuditLog>` e configuração EF Core em `FrogBetsDbContext.OnModelCreating`
    - Adicionar `DbSet<AuditLog> AuditLogs` ao `FrogBetsDbContext`
    - Configurar constraints de tamanho, índices em `ActorId`, `Action`, `OccurredAt` e FK nullable para `User` com `OnDelete: SetNull`
    - _Requirements: 2.1, 2.2, 2.4_

  - [x] 2.2 Gerar migration EF Core `AddAuditLogs`
    - Executar: `dotnet ef migrations add AddAuditLogs --project src/FrogBets.Infrastructure --startup-project src/FrogBets.Api`
    - Verificar que o arquivo de migration gerado contém a tabela `AuditLogs` com todos os campos e índices corretos
    - _Requirements: 2.1, 2.4_

- [x] 3. Implementar IAuditLogService e AuditLogService
  - [x] 3.1 Criar `src/FrogBets.Api/Services/AuditLogService.cs` com interface e implementação
    - Definir `IAuditLogService` com `LogAsync`, `QueryAsync` e `DeleteExpiredAsync`
    - Definir records `AuditLogEntry`, `AuditLogQuery`, `AuditLogPage`, `AuditLogDto`
    - Implementar `LogAsync`: truncar `Details` para 1000 chars, persistir via `DbContext`, nunca lançar exceção (capturar e logar via `ILogger`)
    - Implementar `QueryAsync`: filtros opcionais por `ActorId`, `Action`, `from`, `to`; ordenação por `OccurredAt` DESC; paginação com `pageSize` máximo de 100 (padrão 20)
    - Implementar `DeleteExpiredAsync`: remover todos os logs com `OccurredAt < cutoff`
    - _Requirements: 1.5, 2.1, 2.2, 2.3, 4.2, 4.4, 4.5, 6.2_

  - [x]* 3.2 Escrever testes unitários para AuditLogService
    - Criar `tests/FrogBets.Tests/AuditLogServiceTests.cs` usando InMemory database
    - Testar truncamento de `Details` > 1000 chars
    - Testar `QueryAsync` com filtros combinados (`actorId` + `from` + `to`)
    - Testar `QueryAsync` com `from` sem `to` retorna até o momento atual
    - Testar `pageSize` > 100 é truncado para 100
    - Testar `DeleteExpiredAsync` remove apenas logs com `OccurredAt < cutoff`
    - _Requirements: 2.3, 4.2, 4.4, 4.5, 6.2_

  - [x]* 3.3 Escrever property test — Property 7: Details truncado para 1000 chars
    - Criar `tests/FrogBets.Tests/AuditLogPropertyTests.cs`
    - `// Feature: audit-logs, Property 7: Campo Details é truncado para no máximo 1000 caracteres`
    - Gerar strings de tamanho aleatório (0–2000); verificar que valor persistido tem `length <= 1000`
    - _Requirements: 2.3_

  - [x]* 3.4 Escrever property test — Property 5: Limpeza remove exatamente os expirados
    - `// Feature: audit-logs, Property 5: Limpeza remove exatamente os logs expirados e preserva os válidos`
    - Gerar logs com `OccurredAt` aleatórios em torno do cutoff; executar `DeleteExpiredAsync`; verificar que apenas logs com `OccurredAt >= cutoff` permanecem
    - _Requirements: 6.2_

  - [x]* 3.5 Escrever property test — Property 8: Resultados ordenados por OccurredAt DESC
    - `// Feature: audit-logs, Property 8: Resultados de consulta são ordenados por OccurredAt decrescente`
    - Gerar N logs com timestamps aleatórios; chamar `QueryAsync`; verificar ordenação decrescente
    - _Requirements: 4.2_

  - [x]* 3.6 Escrever property test — Property 4: Paginação respeita limite máximo de pageSize
    - `// Feature: audit-logs, Property 4: Paginação respeita o limite máximo de pageSize`
    - Gerar N logs (N variável) e `pageSize` aleatório (1–500); verificar `Items.Count <= min(pageSize, 100)`
    - _Requirements: 4.4_

- [x] 4. Checkpoint — Garantir que todos os testes passam
  - Garantir que todos os testes passam. Perguntar ao usuário se houver dúvidas.

- [x] 5. Implementar AuditMiddleware
  - [x] 5.1 Criar `src/FrogBets.Api/Middleware/AuditMiddleware.cs`
    - Implementar `ShouldAudit`: retornar `true` apenas para métodos POST, PATCH, PUT, DELETE
    - Implementar dicionário estático `(HttpMethod, RouteTemplate) → (Action, ResourceType?)` com os 26 endpoints mapeados conforme tabela do design
    - Implementar `ResolveAction`: buscar no dicionário; fallback para `"<METHOD> <route_template>"`
    - Implementar `ResolveResource`: extrair `ResourceId` dos route values `{id}`, `{userId}`, `{teamId}`
    - Capturar `ActorId` e `ActorUsername` de `HttpContext.User`; usar `null` e `"anonymous"` para requisições sem JWT válido
    - Capturar `StatusCode` via callback `OnStarting` no `HttpResponse`
    - Persistir via `FireAndForgetLog` usando `IServiceScopeFactory` (não bloquear a resposta)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 3.1, 3.2, 3.3, 5.1_

  - [x]* 5.2 Escrever property test — Property 1: Requisição de escrita gera exatamente 1 AuditLog
    - `// Feature: audit-logs, Property 1: Requisição de escrita gera exatamente um AuditLog com campos corretos`
    - Gerar entradas com método POST/PATCH/DELETE e rota aleatória; invocar `LogAsync`; verificar que exatamente 1 AuditLog foi inserido com todos os campos obrigatórios preenchidos
    - _Requirements: 1.1, 2.1, 5.1_

  - [x]* 5.3 Escrever property test — Property 2: GET nunca gera AuditLog
    - `// Feature: audit-logs, Property 2: Requisições GET nunca geram AuditLog`
    - Simular chamada com método GET; verificar que nenhum log é inserido
    - _Requirements: 1.2, 4.1_

  - [x]* 5.4 Escrever property test — Property 3: StatusCode reflete a resposta real
    - `// Feature: audit-logs, Property 3: StatusCode do AuditLog reflete o status code real da resposta`
    - Gerar status codes aleatórios (200, 201, 400, 403, 404, 500); verificar que `StatusCode` do log persistido é igual ao gerado
    - _Requirements: 1.1, 2.1_

  - [x]* 5.5 Escrever property test — Property 6: Anônimo tem ActorId null e username "anonymous"
    - `// Feature: audit-logs, Property 6: Requisição anônima gera AuditLog com ActorId nulo e username "anonymous"`
    - Simular entrada sem `ActorId`; verificar que `ActorId = null` e `ActorUsername = "anonymous"`
    - _Requirements: 1.3_

  - [x]* 5.6 Escrever property test — Property 9: Fallback de action para rotas não mapeadas
    - `// Feature: audit-logs, Property 9: Fallback de action para rotas não mapeadas`
    - Gerar rotas aleatórias não presentes no dicionário; verificar que `Action = "<METHOD> <route>"`
    - _Requirements: 3.2_

  - [x]* 5.7 Escrever property test — Property 10: ResourceId extraído dos route values
    - `// Feature: audit-logs, Property 10: ResourceId extraído corretamente dos route values`
    - Gerar GUIDs aleatórios para `{id}`, `{userId}`, `{teamId}`; verificar que `ResourceId` do log é igual ao route value correspondente
    - _Requirements: 3.3, 5.3, 5.4, 5.6, 5.7, 5.8, 5.9_

- [x] 6. Implementar AuditLogsController
  - Criar `src/FrogBets.Api/Controllers/AuditLogsController.cs`
  - Rota: `GET /api/audit-logs` com `[Authorize]`
  - Verificar `User.FindFirstValue("isAdmin") == "true"`; retornar `403 Forbid()` com `{ "error": { "code": "FORBIDDEN", "message": "Acesso negado." } }` se não for admin
  - Aceitar query params: `actorId`, `action`, `from`, `to`, `page`, `pageSize`
  - Delegar para `IAuditLogService.QueryAsync` e retornar `200 Ok(result)`
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [x] 7. Implementar AuditLogCleanupService
  - Criar `src/FrogBets.Api/Services/AuditLogCleanupService.cs` como `BackgroundService`
  - Ler `AUDIT_LOG_RETENTION_DAYS` de `IConfiguration` (padrão: 90)
  - Calcular `cutoff = DateTime.UtcNow - TimeSpan.FromDays(retentionDays)` e chamar `IAuditLogService.DeleteExpiredAsync(cutoff)`
  - Aguardar 24 horas entre execuções via `Task.Delay(TimeSpan.FromDays(1), stoppingToken)`
  - Em caso de falha, logar via `ILogger` e continuar para o próximo ciclo
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 8. Registrar serviços e middleware em Program.cs
  - Adicionar `builder.Services.AddScoped<IAuditLogService, AuditLogService>()`
  - Adicionar `builder.Services.AddHostedService<AuditLogCleanupService>()`
  - Inserir `app.UseMiddleware<AuditMiddleware>()` após `app.UseAuthorization()` e antes de `app.MapControllers()`
  - _Requirements: 1.1, 6.3_

- [x] 9. Atualizar configuração de ambiente
  - Adicionar `AUDIT_LOG_RETENTION_DAYS=90` ao `.env.example`
  - _Requirements: 6.1_

- [x] 10. Checkpoint — Garantir que todos os testes passam
  - Garantir que todos os testes passam. Perguntar ao usuário se houver dúvidas.

- [x] 11. Atualizar documentação
  - [x] 11.1 Atualizar `docs/TECHNICAL.md` com novo endpoint `GET /api/audit-logs`, entidade `AuditLog` e variável `AUDIT_LOG_RETENTION_DAYS`
    - _Requirements: 4.2, 4.3_

  - [x] 11.2 Atualizar `migrations.sql` com o script SQL gerado pela migration `AddAuditLogs`
    - _Requirements: 2.1, 2.4_

- [x] 12. Checkpoint final — Garantir que todos os testes passam
  - Garantir que todos os testes passam. Perguntar ao usuário se houver dúvidas.

## Notes

- Tasks marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- Cada task referencia os requisitos específicos para rastreabilidade
- Os property tests usam `[Property(MaxTest = 100)]` com InMemory database (`Guid.NewGuid().ToString()` por teste)
- O middleware deve ser registrado **após** `UseAuthorization()` para que `HttpContext.User` já esteja populado
- `AuditLogService.LogAsync` nunca lança exceção — erros são capturados e logados silenciosamente

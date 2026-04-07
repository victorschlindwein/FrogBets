---
inclusion: manual
---

# FrogBets — Checklist para Novas Features

Use este guia ao criar uma nova spec ou implementar uma nova funcionalidade.

## 1. Antes de Implementar

- [ ] A feature precisa de nova entidade? → criar em `FrogBets.Domain/Entities/`, adicionar ao `DbContext`, criar migração
- [ ] A feature altera saldo virtual? → usar `BalanceService` com transação `Serializable`
- [ ] A feature tem restrição de acesso? → definir se é público, autenticado, líder ou admin
- [ ] A feature expõe dados sensíveis? → garantir que `PasswordHash` nunca sai na resposta

## 2. Backend

- [ ] Criar interface `IXxxService` com DTOs/records no mesmo arquivo
- [ ] Implementar `XxxService` seguindo o padrão de erros (`InvalidOperationException("CODIGO")`)
- [ ] Registrar o serviço em `Program.cs` (`builder.Services.AddScoped<IXxxService, XxxService>()`)
- [ ] Criar ou atualizar controller com verificações de autorização corretas
- [ ] Usar formato de erro padrão: `{ "error": { "code": "...", "message": "..." } }`

## 3. Banco de Dados

- [ ] Configurar mapeamento EF Core em `OnModelCreating` (índices, constraints, relacionamentos)
- [ ] Criar migração: `dotnet ef migrations add NomeDaMigracao --startup-project ../FrogBets.Api`
- [ ] Atualizar `migrations.sql` se necessário para referência

## 4. Frontend

- [ ] Adicionar funções de API em `frontend/src/api/` (ou arquivo existente relevante)
- [ ] Criar página em `frontend/src/pages/` se necessário
- [ ] Adicionar rota em `frontend/src/App.tsx` (dentro de `<ProtectedRoute>` se autenticada)
- [ ] Adicionar link no `Navbar.tsx` se for uma página principal

## 5. Testes

- [ ] Criar arquivo de testes unitários `XxxTests.cs` em `tests/FrogBets.Tests/`
- [ ] Implementar property-based tests (FsCheck) para cada propriedade de corretude da spec
- [ ] Tag obrigatória nos property-tests: `// Feature: nome-da-spec, Property N: descrição`
- [ ] Rodar `dotnet test` e garantir que todos os testes passam

## 6. Documentação

- [ ] Atualizar `docs/TECHNICAL.md` com novos endpoints e serviços
- [ ] Atualizar `README.md` se a feature for visível para o usuário final
- [ ] Atualizar steerings relevantes se houver novos padrões ou regras de negócio

## 7. Commit

- [ ] Commits descritivos em português ou inglês
- [ ] Não commitar `.env`, `appsettings.Development.json` ou qualquer secret
- [ ] Rodar `dotnet test` antes do commit final

## Padrões de Nomenclatura

| Tipo | Convenção | Exemplo |
|---|---|---|
| Entidade | PascalCase | `GameResult` |
| Enum | PascalCase | `BetStatus.Pending` |
| Interface de serviço | `IXxxService` | `IBetService` |
| DTO/Record | `XxxDto`, `XxxRequest`, `XxxResult` | `BetDto`, `CreateBetBody` |
| Controller | `XxxController` | `BetsController` |
| Teste | `XxxTests` | `BetServiceCreateTests` |
| Código de erro | SCREAMING_SNAKE_CASE | `INSUFFICIENT_BALANCE` |
| Rota de API | kebab-case | `/api/bets/{id}/cover` |
| Variável TS/React | camelCase | `virtualBalance` |
| Componente React | PascalCase | `ProtectedRoute` |

---
inclusion: always
---

# FrogBets — Padrões de Arquitetura e Convenções

## Backend (ASP.NET Core 8)

### Estrutura de um Controller

- Controllers ficam em `src/FrogBets.Api/Controllers/`
- Cada controller injeta apenas interfaces de serviço (nunca `DbContext` diretamente, exceto `LeaderboardController` e `UsersController` que são simples queries)
- Verificação de admin via claim: `User.FindFirstValue("isAdmin") == "true"`
- Verificação de líder de time: consulta ao banco (não via claim, pois `IsTeamLeader` pode mudar sem novo login)
- Extração do userId autenticado: `User.FindFirstValue(ClaimTypes.NameIdentifier)`

### Estrutura de um Service

- Interfaces ficam no mesmo arquivo que os DTOs/records de request/response
- Implementações ficam em arquivos separados
- Erros de negócio: `throw new InvalidOperationException("CODIGO_DO_ERRO")`
- Erros de recurso não encontrado: `throw new KeyNotFoundException("mensagem")`
- Operações que alteram saldo usam transações `IsolationLevel.Serializable`

### Formato de erro padrão (sempre usar este formato)

```json
{ "error": { "code": "CODIGO_DO_ERRO", "message": "Mensagem legível." } }
```

### Códigos de erro existentes

| Código | Contexto |
|---|---|
| `INVALID_CREDENTIALS` | Login com credenciais erradas |
| `INSUFFICIENT_BALANCE` | Saldo insuficiente para aposta |
| `MARKET_NOT_OPEN` | Mercado não está aberto |
| `GAME_ALREADY_STARTED` | Jogo já iniciado |
| `DUPLICATE_BET_ON_MARKET` | Usuário já tem aposta neste mercado |
| `CANNOT_COVER_OWN_BET` | Criador tentando cobrir própria aposta |
| `BET_NOT_AVAILABLE` | Aposta não está mais pendente |
| `NOT_BET_OWNER` | Usuário não é o criador da aposta |
| `CANNOT_CANCEL_ACTIVE_BET` | Aposta ativa não pode ser cancelada |
| `GAME_ALREADY_FINISHED` | Jogo já finalizado |
| `INVALID_INVITE` | Token de convite inválido ou expirado |
| `INVITE_ALREADY_USED` | Convite já utilizado |
| `USERNAME_TAKEN` | Nome de usuário já em uso |
| `PASSWORD_TOO_SHORT` | Senha com menos de 8 caracteres |
| `TEAM_NOT_FOUND` | Time não encontrado |
| `USER_NOT_FOUND` | Usuário não encontrado |
| `USER_NOT_IN_TEAM` | Usuário não pertence ao time |
| `ALREADY_LEADER_OF_OTHER_TEAM` | Usuário já é líder de outro time |
| `FORBIDDEN` | Sem permissão para a operação |
| `TARGET_NOT_AVAILABLE` | Membro alvo não disponível para troca |
| `SAME_TEAM_TRADE` | Membros do mesmo time |
| `OFFER_NOT_PENDING` | Oferta não está pendente |
| `ALREADY_LISTED` | Membro já está na lista de trocas |

### Adicionando uma nova entidade

1. Criar a entidade em `src/FrogBets.Domain/Entities/`
2. Adicionar `DbSet<T>` em `FrogBetsDbContext`
3. Configurar o mapeamento em `OnModelCreating`
4. Criar migração: `dotnet ef migrations add NomeDaMigracao --startup-project ../FrogBets.Api`

## Frontend (React + TypeScript)

### Estrutura de uma página

- Páginas ficam em `frontend/src/pages/`
- Componentes reutilizáveis em `frontend/src/components/`
- Chamadas de API via `apiClient` (com JWT) ou `publicClient` (sem JWT) de `frontend/src/api/client.ts`
- Rotas protegidas usam `<ProtectedRoute />` que verifica o token em `sessionStorage`

### Cliente HTTP

```typescript
import apiClient from '../api/client'          // para endpoints autenticados
import { publicClient } from '../api/client'   // para endpoints públicos
```

### Autenticação no frontend

- Token armazenado em `sessionStorage` com chave `frogbets_token`
- `setToken(token)` / `getToken()` de `client.ts`
- Interceptor automático: 401 → limpa token e redireciona para `/login`

## Testes

### Padrão de teste unitário (xUnit)

```csharp
// Usar InMemory database com Guid único por teste
var options = new DbContextOptionsBuilder<FrogBetsDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
    .Options;
```

### Padrão de property-based test (FsCheck)

```csharp
// Feature: nome-da-spec, Property N: descrição da propriedade
[Property(MaxTest = 100)]
public Property NomeDaPropriedade()
{
    return Prop.ForAll(gerador, valor => {
        // setup, ação, verificação
        return condicao;
    });
}
```

Cada spec deve ter seus property-tests cobrindo todas as propriedades de corretude definidas no `design.md`.

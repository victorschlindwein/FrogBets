---
inclusion: always
---

# FrogBets — Diretrizes de Segurança

## Regras Inegociáveis

1. **Nunca criar endpoint de registro sem validação de convite.** O único caminho para criar usuários é `POST /api/auth/register` com `InviteToken` válido. Não existe e não deve existir endpoint de registro aberto.

2. **Nunca expor qual campo está errado no login.** A mensagem de erro deve ser sempre genérica: `"Credenciais inválidas"` — nunca revelar se o username ou a senha está errado.

3. **Nunca commitar secrets.** O `.env` está no `.gitignore`. Valores reais de `POSTGRES_PASSWORD`, `JWT_KEY` e `ALLOWED_ORIGINS` nunca devem aparecer em código ou commits. Use `.env.example` para documentar as variáveis necessárias.

4. **Nunca incluir `appsettings.Development.json` no repositório.** Está no `.gitignore`. Configurações de desenvolvimento ficam apenas localmente.

## Autenticação e Autorização

- **JWT:** tokens com expiração de 60 minutos (configurável). Claims: `sub` (userId), `unique_name` (username), `jti` (para revogação), `isAdmin`.
- **Logout real:** o `jti` é adicionado à `TokenBlocklist` (persistida no banco + cache em memória). Tokens revogados são rejeitados mesmo antes de expirar.
- **`IsTeamLeader` não está no JWT** — verificar sempre consultando o banco para evitar tokens desatualizados após mudança de papel.
- **Rate limiting:** endpoints de auth têm limite de 5 requisições por 15 minutos por IP.
- **Master Admin:** configurado via `MasterAdminUsername` no `appsettings.json`. Não está no JWT — verificar sempre via config. Único que pode promover/revogar outros admins e não pode ser rebaixado.

## Operações Financeiras (Saldo Virtual)

- Toda operação que altera `VirtualBalance` ou `ReservedBalance` usa transação com `IsolationLevel.Serializable`.
- A cobertura de aposta usa transação Serializable para evitar race condition (dois usuários cobrindo simultaneamente).
- O invariante `VirtualBalance + ReservedBalance = constante` deve ser preservado em toda operação.

## Validação de Input

- Todos os endpoints usam `[Required]`, `[StringLength]`, `[Range]` nos records de request.
- Body limit: 1 MB (configurado no Kestrel).
- CORS: apenas a origem em `ALLOWED_ORIGINS` é permitida.
- `AllowedHosts` configurado para `localhost` (não `*`).

## Histórico de Commits — Nota

O commit `fae0fad` (commit inicial) expôs valores placeholder no `appsettings.json`:
- `Password=postgres` (senha padrão de dev, não é credencial real de produção)
- `Key: CHANGE_ME_TO_A_SECURE_SECRET_KEY_AT_LEAST_32_CHARS` (placeholder explícito)

Esses valores foram removidos no commit `0273b23`. Como o repositório é público, qualquer novo desenvolvedor deve saber que esses valores **nunca foram usados em produção** — são apenas defaults de desenvolvimento local.

## Geração de Tokens

- Tokens de convite usam `RandomNumberGenerator.GetBytes(16)` convertido para hex — criptograficamente seguro
- Nunca usar `Guid.NewGuid().ToString()` para tokens de segurança

## Ao Adicionar Novos Endpoints

- Endpoints admin: verificar `User.FindFirstValue("isAdmin") == "true"` e retornar `403 Forbid()` se não for admin.
- Endpoints de líder: consultar `db.Users.Where(u => u.Id == requesterId).Select(u => u.IsTeamLeader)`.
- Endpoints públicos: marcar explicitamente com `[AllowAnonymous]`.
- Nunca expor `PasswordHash` em respostas da API.
- Nunca expor dados de outros usuários além do necessário (username, saldo público no leaderboard).

## Auditoria

- Operações administrativas sensíveis devem ser registradas via `IAuditLogService`.
- Logs incluem: `ActorId`, `Username`, `Action`, `ResourceType`, `ResourceId`, `StatusCode`, `Details`.
- Retenção configurável via `AUDIT_LOG_RETENTION_DAYS` (padrão: 90 dias).
- Limpeza automática via `AuditLogCleanupService` (background service).

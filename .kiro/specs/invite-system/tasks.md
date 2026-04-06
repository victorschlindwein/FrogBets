# Plano de Implementação: Invite System

## Visão Geral

Implementação incremental do sistema de convites do FrogBets: entidade de domínio → migration → serviço de convites → endpoint de cadastro → painel admin → frontend.

## Tasks

- [x] 1. Criar entidade `Invite` e enum `InviteStatus` no domínio
  - Criar `src/FrogBets.Domain/Entities/Invite.cs` com os campos: `Id`, `Token`, `Description`, `ExpiresAt`, `CreatedAt`, `UsedAt`, `UsedByUserId` e navigation property `UsedByUser`
  - Criar `src/FrogBets.Domain/Enums/InviteStatus.cs` com valores `Pending`, `Used`, `Expired`
  - _Requirements: 1.1, 1.2, 1.3, 1.5_

- [x] 2. Configurar EF Core e migration
  - Adicionar `DbSet<Invite> Invites` ao `FrogBetsDbContext`
  - Configurar a entidade no `OnModelCreating`: índice único em `Token`, FK para `Users`
  - Gerar migration `AddInvites` e aplicar ao banco
  - _Requirements: 1.1, 1.4_

- [x] 3. Implementar `IInviteService` e `InviteService`
  - [x] 3.1 Criar interface `IInviteService` em `src/FrogBets.Api/Services/IInviteService.cs`
    - Declarar métodos: `GenerateAsync`, `GetAllAsync`, `RevokeAsync`, `ValidateAndConsumeAsync`, `MarkUsedAsync`
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 4.1_

  - [x] 3.2 Implementar `InviteService` em `src/FrogBets.Api/Services/InviteService.cs`
    - `GenerateAsync`: gerar token via `Guid.NewGuid().ToString("N")`, persistir com `ExpiresAt` e `Description` opcionais
    - `GetAllAsync`: retornar todos os convites com status calculado (Used/Expired/Pending)
    - `RevokeAsync`: verificar status pendente, setar `ExpiresAt = UtcNow`; lançar exceção se já usado ou expirado
    - `ValidateAndConsumeAsync`: verificar existência, uso e expiração; retornar `Invite` válido ou lançar exceção tipada
    - `MarkUsedAsync`: setar `UsedAt = UtcNow` e `UsedByUserId`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 3.4, 3.8, 4.1, 4.2, 4.3, 4.4_

  - [ ]* 3.3 Escrever testes unitários para `InviteService`
    - Testar geração de token único
    - Testar `ValidateAndConsumeAsync` com token válido, inexistente, já usado e expirado
    - Testar `RevokeAsync` com convite pendente, já usado e já expirado
    - Testar que `MarkUsedAsync` preserva token em caso de falha posterior
    - _Requirements: 1.3, 2.1, 2.2, 2.3, 2.4, 3.8, 4.1, 4.2, 4.3, 4.4_

  - [ ]* 3.4 Escrever property test — Property 2: Validação rejeita tokens inválidos
    - Gerar strings aleatórias como token e verificar que `ValidateAndConsumeAsync` sempre rejeita sem criar usuário
    - **Property 2: Validação de token rejeita inválidos**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4**

  - [ ]* 3.5 Escrever property test — Property 5: Status calculado é consistente
    - Gerar convites com combinações aleatórias de `UsedAt`/`ExpiresAt` e verificar que o status é sempre um dos três valores e mutuamente exclusivo
    - **Property 5: Status calculado é consistente**
    - **Validates: Requirements 1.4**

- [x] 4. Implementar `InvitesController` (endpoints admin)
  - [x] 4.1 Criar `src/FrogBets.Api/Controllers/InvitesController.cs`
    - `POST /api/invites` — gerar convite; requer `isAdmin`; body: `CreateInviteRequest(ExpiresAt, Description?)`; retorna `InviteResponse`
    - `GET /api/invites` — listar todos; requer `isAdmin`; retorna `IEnumerable<InviteResponse>`
    - `DELETE /api/invites/{id}` — revogar convite pendente; requer `isAdmin`
    - Verificar claim `isAdmin` manualmente (igual ao padrão existente no projeto)
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 4.1, 4.2, 4.3, 4.4_

  - [ ]* 4.2 Escrever testes unitários para `InvitesController`
    - Testar autorização: sem token → 401, token de não-admin → 403
    - Testar respostas HTTP corretas para cada endpoint
    - _Requirements: 1.1, 1.4, 4.1, 4.3, 4.4_

- [x] 5. Implementar endpoint de cadastro no backend
  - [x] 5.1 Adicionar `RegisterAsync` ao `IAuthService` e `AuthService`
    - Validar senha (mínimo 8 caracteres)
    - Verificar unicidade do username
    - Criar usuário com `VirtualBalance = 1000`, hash de senha via BCrypt
    - Chamar `InviteService.MarkUsedAsync` dentro da mesma transação
    - Retornar `AuthResult` (JWT) igual ao login
    - _Requirements: 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

  - [x] 5.2 Adicionar `POST /api/auth/register` ao `AuthController`
    - `[AllowAnonymous]`; body: `RegisterRequest(InviteToken, Username, Password)`
    - Chamar `InviteService.ValidateAndConsumeAsync` antes de criar o usuário
    - Mapear exceções para os códigos de erro definidos no design (`INVALID_INVITE`, `INVITE_ALREADY_USED`, `INVITE_EXPIRED`, `USERNAME_TAKEN`, `PASSWORD_TOO_SHORT`)
    - Em caso de falha, garantir que o token permanece `Pending`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

  - [ ]* 5.3 Escrever testes unitários para `AuthController.Register` e `AuthService.RegisterAsync`
    - Fluxo de sucesso: usuário criado com saldo 1000, JWT retornado
    - Falha por token inválido, expirado, já usado
    - Falha por username duplicado e senha curta
    - _Requirements: 3.3, 3.5, 3.6, 3.7, 3.8_

  - [ ]* 5.4 Escrever property test — Property 1: Token é de uso único
    - Usar token válido para cadastro, tentar usar novamente → segunda tentativa sempre rejeitada
    - **Property 1: Token de convite é de uso único**
    - **Validates: Requirements 1.3, 3.4**

  - [ ]* 5.5 Escrever property test — Property 3: Falha no cadastro preserva o token
    - Gerar dados inválidos (username duplicado, senha curta) com token válido → token permanece `Pending`
    - **Property 3: Falha no cadastro preserva o token**
    - **Validates: Requirements 3.8**

  - [ ]* 5.6 Escrever property test — Property 4: Revogação torna token inválido
    - Gerar convite pendente, revogar, tentar cadastro → sempre rejeitado
    - **Property 4: Revogação torna token inválido**
    - **Validates: Requirements 4.1, 4.2**

- [ ] 6. Checkpoint — Garantir que todos os testes do backend passam
  - Garantir que todos os testes passam; perguntar ao usuário se houver dúvidas.

- [x] 7. Registrar serviços e atualizar `Program.cs`
  - Adicionar `builder.Services.AddScoped<IInviteService, InviteService>()` em `Program.cs`
  - _Requirements: 1.1_

- [x] 8. Implementar `RegisterPage.tsx` no frontend
  - [x] 8.1 Criar `frontend/src/pages/RegisterPage.tsx`
    - Formulário com campos: Token de Convite, Nome de usuário, Senha (mínimo 8 chars)
    - Chamar `POST /api/auth/register` via `apiClient`
    - Em caso de sucesso: salvar token JWT via `setToken` e redirecionar para `/`
    - Exibir mensagens de erro mapeadas para cada código de erro da API
    - _Requirements: 3.1, 3.2, 3.3, 3.5, 3.6, 3.7_

  - [ ]* 8.2 Escrever testes para `RegisterPage`
    - Renderização do formulário com os três campos
    - Submissão com sucesso (mock da API) → redirecionamento
    - Exibição de erros para cada cenário de falha
    - _Requirements: 3.1, 3.2, 3.5, 3.6, 3.7_

- [x] 9. Adicionar rota `/register` e link "Criar conta" no frontend
  - Adicionar `<Route path="/register" element={<RegisterPage />} />` em `App.tsx` fora do `ProtectedRoute`
  - Adicionar `<Link to="/register">Criar conta</Link>` na `LoginPage.tsx`
  - _Requirements: 3.1_

  - [ ]* 9.1 Escrever teste para `LoginPage` — presença do link "Criar conta"
    - Verificar que o link aponta para `/register`
    - _Requirements: 3.1_

- [x] 10. Implementar seção de convites no `AdminPage.tsx`
  - Adicionar seção "Convites" à página admin existente
  - Formulário para gerar convite: campo de data de expiração e descrição opcional; exibir token gerado após criação
  - Tabela listando todos os convites com colunas: Token, Descrição, Status, Criado em, Expira em
  - Botão "Revogar" visível apenas para convites com status `Pending`
  - _Requirements: 1.1, 1.2, 1.4, 1.5, 4.1_

- [ ] 11. Checkpoint final — Garantir que todos os testes passam
  - Garantir que todos os testes passam; perguntar ao usuário se houver dúvidas.

## Notas

- Tasks marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- Cada task referencia os requisitos correspondentes para rastreabilidade
- Os property tests usam FsCheck (backend) e fast-check (frontend)
- A revogação reutiliza a lógica de expiração (setar `ExpiresAt = UtcNow`) sem campo extra

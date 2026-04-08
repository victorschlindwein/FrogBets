# Plano de Implementação: invite-improvements

## Visão Geral

Implementar validade fixa de 24h nos convites (removendo `expiresAt` como parâmetro externo) e geração em massa de 1 a 50 convites por requisição, com ajustes no backend (C#) e no frontend (React/TypeScript).

## Tasks

- [x] 1. Atualizar `IInviteService` e `InviteService` — remover `expiresAt` do `GenerateAsync`
  - Alterar a assinatura de `GenerateAsync(DateTime expiresAt, string? description)` para `GenerateAsync(string? description)` em `IInviteService`
  - Atualizar a implementação em `InviteService.cs` para calcular `ExpiresAt = DateTime.UtcNow.AddHours(24)` internamente
  - _Requirements: 1.1_

  - [ ]* 1.1 Escrever teste de propriedade — Property 1: round-trip de geração com validade fixa
    - **Property 1: Round-trip de geração com validade fixa**
    - Verificar que `ExpiresAt` está dentro de ±5s de `DateTime.UtcNow.AddHours(24)` e que o token é validável via `ValidateAsync` imediatamente após a criação
    - **Validates: Requirements 1.1, 3.1**

  - [ ]* 1.2 Escrever testes unitários para `InviteService.GenerateAsync`
    - Testar que `ExpiresAt ≈ UtcNow+24h` (sem parâmetro externo)
    - Testar que `description` é preservada quando fornecida
    - _Requirements: 1.1_

- [x] 2. Atualizar `InvitesController` — novo request/response para geração em lote
  - Substituir `CreateInviteRequest` por `CreateInvitesRequest(int Quantity = 1, string? Description = null)` com `[Range(1, 50)]`
  - Substituir `CreateInviteResponse` por `CreateInvitesResponse(IReadOnlyList<string> Tokens)`
  - Reescrever o handler `POST /api/invites` com loop de `GenerateAsync`, validação explícita de `Quantity` e retorno dos códigos `INVALID_QUANTITY` e `QUANTITY_LIMIT_EXCEEDED`
  - _Requirements: 1.2, 2.1, 2.6, 2.7_

  - [ ]* 2.1 Escrever teste de propriedade — Property 2: geração em lote produz exatamente N tokens únicos
    - **Property 2: Geração em lote produz exatamente N tokens únicos**
    - Para qualquer N em [1, 50], verificar que são gerados exatamente N tokens distintos com 32 caracteres hexadecimais
    - **Validates: Requirements 2.1, 2.2**

  - [ ]* 2.2 Escrever teste de propriedade — Property 3: quantidade inválida é rejeitada com código correto
    - **Property 3: Quantidade inválida é rejeitada com código correto**
    - Para `quantity < 1` → HTTP 400 com `INVALID_QUANTITY`; para `quantity > 50` → HTTP 400 com `QUANTITY_LIMIT_EXCEEDED`; nenhum convite deve ser criado no banco
    - **Validates: Requirements 2.6, 2.7**

- [x] 3. Checkpoint — garantir que todos os testes .NET passam
  - Garantir que todos os testes passam. Rodar `dotnet test --configuration Release --verbosity quiet`. Perguntar ao usuário se houver dúvidas.

- [x] 4. Atualizar `InvitesSection` no `AdminPage.tsx` — remover campo "Expira em" e adicionar campo de quantidade
  - Remover estado `expiresAt` e o campo `inviteExpiresAt` do formulário
  - Adicionar estado `quantity: number` (padrão `1`) e campo numérico `inviteQuantity` (min=1, max=50)
  - Substituir estado `newToken: string | null` por `newTokens: string[]`
  - Atualizar o payload do `apiClient.post` para `{ quantity, description: quantity === 1 ? (description || null) : undefined }`
  - _Requirements: 1.3, 2.3, 2.8_

  - [ ]* 4.1 Escrever teste de propriedade — Property 4: formulário oculta "Destinatário" quando quantity > 1
    - **Property 4: Formulário oculta "Destinatário" quando quantity > 1**
    - Para qualquer `quantity > 1`, o campo "Destinatário" não deve estar no DOM
    - **Validates: Requirements 2.4**

- [x] 5. Exibir campo "Destinatário" condicionalmente e lista de tokens gerados
  - Renderizar o campo `inviteDescription` apenas quando `quantity === 1`
  - Após geração bem-sucedida, exibir lista de todos os tokens com botão de cópia individual por token (substituindo a exibição de token único)
  - Manter a coluna "Expira em" na tabela de listagem de convites existente
  - _Requirements: 1.4, 2.3, 2.4, 2.5_

  - [ ]* 5.1 Escrever teste de propriedade — Property 5: todos os tokens gerados são exibidos na UI
    - **Property 5: Todos os tokens gerados são exibidos na UI**
    - Para qualquer lista de N tokens retornada pela API, todos os N tokens devem estar presentes no DOM do componente
    - **Validates: Requirements 2.5**

  - [ ]* 5.2 Escrever testes unitários para `InvitesSection`
    - Testar que o campo "Expira em" não está no formulário
    - Testar que o campo "Destinatário" aparece com `quantity = 1` e some com `quantity > 1`
    - Testar que todos os tokens retornados são renderizados
    - _Requirements: 1.3, 2.3, 2.4, 2.5_

- [x] 6. Atualizar `docs/TECHNICAL.md`
  - Atualizar a assinatura do endpoint `POST /api/invites`: novo request (`quantity`, `description?`) e nova resposta (`{ tokens: string[] }`)
  - Documentar os novos códigos de erro `INVALID_QUANTITY` e `QUANTITY_LIMIT_EXCEEDED`
  - _Requirements: 1.2, 2.1, 2.6, 2.7_

- [x] 7. Checkpoint final — garantir que todos os testes passam
  - Rodar `dotnet test --configuration Release --verbosity quiet` e `npm run test -- --run` no frontend. Perguntar ao usuário se houver dúvidas.

## Notas

- Tasks marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- Cada task referencia os requisitos específicos para rastreabilidade
- A lógica de `RevokeAsync`, `ValidateAsync` e `MarkUsedAsync` não é alterada (Requirement 3)
- Não há alteração no schema do banco — apenas a origem do valor `ExpiresAt` muda

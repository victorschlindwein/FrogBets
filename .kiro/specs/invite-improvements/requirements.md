# Requirements Document

## Introduction

Este documento descreve duas melhorias no sistema de convites do painel admin do FrogBets:

1. **Validade fixa de 24h**: a validade de todo convite passa a ser sempre 24 horas a partir da criação, calculada automaticamente no backend. O campo "Expira em" é removido do formulário de criação.

2. **Geração em massa**: o admin pode gerar múltiplos convites de uma só vez informando uma quantidade. Quando a quantidade é maior que 1, o campo "Destinatário" (description) não está disponível. Quando a quantidade é 1, o campo "Destinatário" permanece disponível.

A lógica de expiração, revogação e validação de convites não muda.

## Glossary

- **InviteService**: serviço de domínio responsável por gerar, listar, revogar e validar convites.
- **InvitesController**: controller REST que expõe os endpoints de convites para o painel admin.
- **AdminPage**: página React do painel administrativo, seção de convites.
- **Convite**: token criptograficamente seguro de uso único com validade de 24h, gerado pelo admin para permitir o registro de um novo usuário.
- **Geração individual**: criação de um único convite, com campo "Destinatário" opcional.
- **Geração em massa**: criação de múltiplos convites em uma única requisição, sem campo "Destinatário".

## Requirements

### Requirement 1: Validade fixa de 24 horas

**User Story:** Como admin, quero que todo convite gerado expire automaticamente em 24 horas, para não precisar informar manualmente a data de expiração a cada geração.

#### Acceptance Criteria

1. WHEN um convite é gerado, THE InviteService SHALL definir `ExpiresAt` como `DateTime.UtcNow + 24 horas`, sem aceitar esse valor como parâmetro externo.
2. THE InvitesController SHALL aceitar requisições de criação de convite sem o campo `ExpiresAt` no body.
3. THE AdminPage SHALL exibir o formulário de criação de convite sem o campo "Expira em".
4. WHEN um convite é exibido na listagem, THE AdminPage SHALL continuar exibindo a coluna "Expira em" com o valor calculado pelo backend.

### Requirement 2: Geração em massa de convites

**User Story:** Como admin, quero gerar múltiplos convites de uma vez informando uma quantidade, para agilizar o processo de onboarding de vários usuários simultaneamente.

#### Acceptance Criteria

1. WHEN o admin informa uma quantidade maior que 0, THE InvitesController SHALL gerar exatamente essa quantidade de convites em uma única requisição e retornar todos os tokens gerados.
2. THE InviteService SHALL gerar cada convite com um token criptograficamente único usando `RandomNumberGenerator.GetBytes(16)`.
3. WHEN a quantidade informada é 1, THE AdminPage SHALL exibir o campo "Destinatário" (description) como opcional no formulário.
4. WHEN a quantidade informada é maior que 1, THE AdminPage SHALL ocultar o campo "Destinatário" do formulário.
5. WHEN a geração em massa é concluída, THE AdminPage SHALL exibir todos os tokens gerados para que o admin possa copiá-los.
6. IF a quantidade informada for menor que 1 ou não for um número inteiro, THEN THE InvitesController SHALL retornar erro com código `INVALID_QUANTITY` e mensagem descritiva.
7. IF a quantidade informada for maior que 50, THEN THE InvitesController SHALL retornar erro com código `QUANTITY_LIMIT_EXCEEDED` e mensagem descritiva.
8. THE AdminPage SHALL exibir um campo numérico para a quantidade, com valor padrão 1 e mínimo 1.

### Requirement 3: Compatibilidade com lógica existente

**User Story:** Como admin, quero que as melhorias não alterem o comportamento de revogação e validação de convites, para garantir que o sistema continue funcionando corretamente.

#### Acceptance Criteria

1. WHEN um convite gerado individualmente ou em massa é utilizado no registro, THE InviteService SHALL validar o token, verificar se não foi usado e se não está expirado, sem alterações no fluxo existente.
2. WHEN um convite pendente é revogado, THE InviteService SHALL definir `ExpiresAt` como `DateTime.UtcNow`, independentemente de ter sido gerado individualmente ou em massa.
3. THE InviteService SHALL rejeitar convites com `UsedAt` preenchido com código `INVITE_ALREADY_USED`.
4. THE InviteService SHALL rejeitar convites com `ExpiresAt <= DateTime.UtcNow` com código `INVITE_EXPIRED`.

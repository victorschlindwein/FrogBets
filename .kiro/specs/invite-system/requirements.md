# Requirements Document

## Introduction

O FrogBets é um projeto fechado de apostas virtuais. Atualmente não existe tela de cadastro — usuários ficam presos na tela de login sem conseguir criar conta. O sistema de convites resolve isso: o Administrador gera tokens de convite com validade e uso único, e o usuário utiliza esse token para criar sua conta na plataforma. Isso garante que apenas pessoas autorizadas pelo Administrador possam se cadastrar, mantendo o grupo fechado.

## Glossary

- **FrogBets**: A plataforma web de apostas P2P descrita neste documento.
- **Administrador**: Usuário com permissão elevada capaz de gerenciar convites e a plataforma.
- **Convite**: Token alfanumérico gerado pelo Administrador que autoriza o cadastro de um novo Usuário.
- **Token de Convite**: Identificador único e secreto associado a um Convite, utilizado pelo destinatário para criar sua conta.
- **Usuário**: Membro autenticado da plataforma FrogBets.
- **Cadastro**: Processo de criação de uma nova conta de Usuário mediante apresentação de um Token de Convite válido.
---

## Requirements

### Requirement 1: Geração de Tokens de Convite pelo Administrador

**User Story:** Como Administrador, quero gerar tokens de convite, para que eu possa controlar quem tem acesso à plataforma e manter o grupo fechado.

#### Acceptance Criteria

1. WHEN o Administrador solicita a criação de um Convite, THE InviteSystem SHALL gerar um Token de Convite único e criptograficamente seguro.
2. WHEN o Administrador cria um Convite, THE InviteSystem SHALL associar ao Token de Convite uma data de expiração configurável pelo Administrador.
3. THE InviteSystem SHALL garantir que cada Token de Convite seja de uso único — após ser utilizado para cadastro, o Token não poderá ser reutilizado.
4. THE Administrador SHALL visualizar a lista de todos os Convites gerados, incluindo status (pendente, utilizado ou expirado), data de criação e data de expiração de cada Convite.
5. WHEN o Administrador solicita a criação de um Convite, THE InviteSystem SHALL permitir que o Administrador informe opcionalmente uma descrição ou identificação do destinatário pretendido.

---

### Requirement 2: Validação do Token de Convite

**User Story:** Como sistema, quero validar o token de convite antes de permitir o cadastro, para que apenas usuários com convite válido possam criar uma conta.

#### Acceptance Criteria

1. WHEN um usuário apresenta um Token de Convite no formulário de cadastro, THE InviteSystem SHALL verificar se o Token existe na base de dados.
2. IF o Token de Convite não existir na base de dados, THEN THE InviteSystem SHALL rejeitar a operação e retornar uma mensagem de erro genérica sem revelar se o token existe ou não.
3. IF o Token de Convite já tiver sido utilizado, THEN THE InviteSystem SHALL rejeitar a operação e retornar uma mensagem informando que o convite já foi utilizado.
4. IF o Token de Convite estiver expirado (data de expiração anterior à data atual), THEN THE InviteSystem SHALL rejeitar a operação e retornar uma mensagem informando que o convite expirou.
5. WHEN um Token de Convite válido é apresentado, THE InviteSystem SHALL permitir que o usuário prossiga com o preenchimento dos dados de cadastro.

---

### Requirement 3: Cadastro de Novo Usuário com Token de Convite

**User Story:** Como convidado, quero criar minha conta utilizando um token de convite, para que eu possa acessar a plataforma FrogBets.

#### Acceptance Criteria

1. THE FrogBets SHALL disponibilizar uma tela de cadastro acessível a partir da tela de login.
2. WHEN um convidado acessa a tela de cadastro, THE FrogBets SHALL exibir um formulário solicitando: Token de Convite, nome de usuário desejado e senha.
3. WHEN um convidado submete o formulário de cadastro com dados válidos e Token de Convite válido, THE InviteSystem SHALL criar a conta do Usuário com Saldo Virtual inicial de 1000 unidades.
4. WHEN o cadastro é concluído com sucesso, THE InviteSystem SHALL marcar o Token de Convite como utilizado, impedindo seu reuso.
5. WHEN o cadastro é concluído com sucesso, THE FrogBets SHALL autenticar o novo Usuário automaticamente e redirecioná-lo para a página inicial.
6. IF o nome de usuário escolhido já estiver em uso, THEN THE InviteSystem SHALL rejeitar a operação e exibir uma mensagem informando que o nome de usuário já está em uso.
7. THE InviteSystem SHALL exigir que a senha tenha no mínimo 8 caracteres.
8. WHEN o cadastro falha por qualquer motivo, THE InviteSystem SHALL preservar o Token de Convite como não utilizado.

---

### Requirement 4: Revogação de Convites pelo Administrador

**User Story:** Como Administrador, quero revogar convites pendentes, para que eu possa cancelar um convite enviado por engano ou que não deva mais ser utilizado.

#### Acceptance Criteria

1. WHEN um Convite está com status pendente (não utilizado e não expirado), THE Administrador SHALL revogar o Convite.
2. WHEN um Convite é revogado, THE InviteSystem SHALL marcar o Token de Convite como expirado imediatamente, impedindo seu uso para cadastro.
3. IF um Administrador tenta revogar um Convite já utilizado, THEN THE InviteSystem SHALL rejeitar a operação e exibir uma mensagem informando que o Convite já foi utilizado.
4. IF um Administrador tenta revogar um Convite já expirado, THEN THE InviteSystem SHALL rejeitar a operação e exibir uma mensagem informando que o Convite já está expirado.

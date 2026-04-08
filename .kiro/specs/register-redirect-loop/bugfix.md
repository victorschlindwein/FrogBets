# Bugfix Requirements Document

## Introduction

Ao clicar em "Criar conta" na página de login, o usuário é redirecionado para `/register` e imediatamente redirecionado de volta para `/login`. O bug ocorre porque `RegisterPage` usa `apiClient` (que possui interceptor de 401 → redirect para `/login`) para buscar a lista de times via `getTeams()`. Como o usuário não está autenticado em `/register`, a chamada retorna 401, o interceptor dispara e o usuário é enviado de volta para `/login` antes mesmo de ver o formulário de registro.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN um usuário não autenticado acessa `/register` THEN o sistema dispara uma chamada autenticada (`apiClient`) para `GET /api/teams` no `useEffect`

1.2 WHEN a chamada `GET /api/teams` retorna 401 (usuário não autenticado) THEN o interceptor de resposta do `apiClient` limpa o token e redireciona para `/login`

1.3 WHEN o redirecionamento para `/login` ocorre durante o carregamento de `/register` THEN o usuário nunca consegue visualizar ou interagir com o formulário de registro

### Expected Behavior (Correct)

2.1 WHEN um usuário não autenticado acessa `/register` THEN o sistema SHALL buscar a lista de times usando `publicClient` (sem JWT, sem interceptor de 401)

2.2 WHEN a chamada `GET /api/teams` via `publicClient` retorna 401 ou falha THEN o sistema SHALL permanecer em `/register` sem redirecionar para `/login`

2.3 WHEN a chamada `GET /api/teams` via `publicClient` é bem-sucedida THEN o sistema SHALL exibir o formulário de registro com o seletor de times populado normalmente

### Unchanged Behavior (Regression Prevention)

3.1 WHEN um usuário não autenticado tenta acessar uma rota protegida THEN o sistema SHALL CONTINUE TO redirecionar para `/login` via `ProtectedRoute`

3.2 WHEN qualquer chamada autenticada via `apiClient` retorna 401 THEN o sistema SHALL CONTINUE TO limpar o token e redirecionar para `/login`

3.3 WHEN o usuário preenche e submete o formulário de registro com dados válidos THEN o sistema SHALL CONTINUE TO criar a conta via `POST /api/auth/register` e redirecionar para `/`

3.4 WHEN o usuário preenche e submete o formulário de registro com dados inválidos THEN o sistema SHALL CONTINUE TO exibir as mensagens de erro correspondentes sem redirecionar

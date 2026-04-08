---
inclusion: always
---

# FrogBets — Visão Geral do Projeto

FrogBets é uma plataforma web **fechada** de apostas virtuais P2P para membros do grupo FrogEventos apostarem em partidas de Counter-Strike. Não há dinheiro real envolvido — o saldo é puramente fictício e serve como métrica de desempenho.

## Stack

- **Backend:** ASP.NET Core 8 (C#), REST API, JWT Bearer
- **Frontend:** React 18 + TypeScript + Vite + Axios
- **Banco:** PostgreSQL 16 via Entity Framework Core 8
- **Testes:** xUnit + FsCheck (property-based testing) + Vitest + Cypress
- **Deploy:** Docker Compose / AWS ECS Fargate

## Estrutura do Repositório

```
src/
  FrogBets.Api/          # Controllers + Services (lógica de negócio)
  FrogBets.Domain/       # Entidades e enums (sem dependências externas)
  FrogBets.Infrastructure/ # DbContext + Migrations EF Core
tests/
  FrogBets.Tests/        # Testes unitários e property-based (xUnit + FsCheck)
  FrogBets.IntegrationTests/ # Testes de integração com WebApplicationFactory
frontend/src/
  api/                   # Cliente Axios + endpoints
  components/            # Navbar, ProtectedRoute, CoinIcon
  pages/                 # Páginas da aplicação (15 páginas)
  test/                  # Setup de testes (Vitest + MSW)
frontend/cypress/        # Testes E2E (Cypress)
docs/                    # Documentação técnica
```

## Acesso à Plataforma

A plataforma é **fechada por convite**. O único ponto de entrada para novos usuários é `POST /api/auth/register` com um token de convite válido gerado pelo admin. Não existe endpoint de registro aberto.

## Primeiro Admin

O primeiro usuário admin deve ser inserido diretamente no banco via SQL (ver README.md). Após isso, o admin usa o painel para gerar convites.

## Ambiente do Desenvolvedor

- **Shell:** PowerShell (win32)
- **NUNCA** usar `\` para quebrar linhas em comandos de terminal. PowerShell não suporta isso.
- Sempre escrever comandos em uma única linha, sem quebras com `\` ou `` ` ``.

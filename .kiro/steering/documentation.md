---
inclusion: always
---

# FrogBets — Diretrizes de Documentação

## Regra Principal

**Toda alteração de funcionalidade deve ser acompanhada de atualização na documentação.**

Nunca entregar código novo sem atualizar os documentos afetados. Documentação desatualizada é tão problemática quanto código quebrado.

## Documentos a Manter

### README.md
Atualizar quando:
- Novas funcionalidades forem adicionadas ou removidas
- A stack mudar (nova tecnologia, novo serviço)
- O processo de setup ou execução local mudar
- Novos tipos de testes forem adicionados

### docs/TECHNICAL.md
Atualizar quando:
- Novos endpoints forem criados ou modificados
- Novos serviços de domínio forem adicionados
- Entidades de domínio mudarem (campos, relacionamentos)
- Novas migrações forem criadas
- A arquitetura de deploy mudar
- Novos arquivos de teste forem adicionados
- Configurações de ambiente mudarem

### DEPLOY.md
Atualizar quando:
- O processo de deploy mudar
- Novos recursos de infraestrutura forem criados
- Variáveis de ambiente forem adicionadas ou removidas
- Scripts de setup forem modificados

### Steering files (`.kiro/steering/`)
Atualizar quando:
- Novos padrões de arquitetura forem estabelecidos
- Novas convenções de código forem adotadas
- Regras de segurança forem adicionadas
- Padrões de deploy ou infraestrutura mudarem

## Checklist Antes de Commitar

- [ ] `README.md` reflete as funcionalidades atuais?
- [ ] `docs/TECHNICAL.md` tem os endpoints e serviços atualizados?
- [ ] Novos campos de entidade estão documentados?
- [ ] Novas variáveis de ambiente estão em `.env.example` e documentadas?
- [ ] `npx tsc --noEmit` no frontend passa sem erros?
- [ ] Todos os testes passam (`dotnet test` + `npm run test`)?

## O Que Não Documentar

- Detalhes de implementação interna que mudam frequentemente
- Comentários óbvios no código
- Histórico de decisões (use commits descritivos para isso)

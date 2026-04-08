---
inclusion: always
---

# FrogBets — Modelo de Domínio

## Entidades Principais

### User
Usuário da plataforma. Acesso apenas via convite.

```csharp
Id, Username, PasswordHash, IsAdmin, VirtualBalance, ReservedBalance,
WinsCount, LossesCount, TeamId (nullable), IsTeamLeader, CreatedAt
```

**Invariante de saldo:** `VirtualBalance + ReservedBalance` = saldo total (constante exceto em liquidações).

**Papéis:**
- Usuário comum: pode criar e cobrir apostas
- Admin (`IsAdmin = true`): gerencia jogos, convites, times, jogadores, logs de auditoria
- Líder de time (`IsTeamLeader = true`): gerencia marketplace de trocas do seu time
- Master Admin (`MasterAdminUsername` no config): único que pode promover/revogar outros admins; não pode ser rebaixado

### Game → Market → Bet (hierarquia central)

```
Game (Scheduled → InProgress → Finished)
  └── Market[] (Open → Closed → Settled/Voided)
        └── Bet[] (Pending → Active → Settled/Cancelled/Voided)
```

**Criação de mercados:** ao criar um Game com N mapas, o sistema gera automaticamente:
- N × {MapWinner, TopKills, MostDeaths, MostUtilityDamage} (mercados por mapa)
- 1 × SeriesWinner (mercado de série)

**Opções de aposta:**
- Mercados de time: `"TeamA"` ou `"TeamB"`
- Mercados de jogador: `"<nickname>"` ou `"NOT_<nickname>"`
- A opção do cobrador é sempre o oposto da opção do criador (atribuída automaticamente)

### CS2Team → CS2Player → MatchStats (sistema de rating)

```
CS2Team
  └── CS2Player (PlayerScore acumulado, MatchesCount)
        └── MatchStats[] (por partida: kills, deaths, assists, damage, rounds, KAST%)
```

**Fórmula de rating (HLTV 2.0 adaptado):**
```
Rating = 0.0073×KAST + 0.3591×KPR + (−0.5329)×DPR + 0.2372×Impact + 0.0032×ADR + 0.1587
```

### Invite
Token de convite gerado pelo admin. Uso único, com expiração.

```csharp
Id, Token (32 chars, único), Description, ExpiresAt, CreatedAt, UsedAt, UsedByUserId
```

### TradeListing / TradeOffer (marketplace de trocas)

```
TradeListing: UserId (único), TeamId — membro disponível para troca
TradeOffer: OfferedUserId, TargetUserId, ProposerTeamId, ReceiverTeamId, Status (Pending/Accepted/Rejected/Cancelled)
```

**Regras de troca:**
- Apenas líderes de time podem criar ofertas e marcar membros como disponíveis
- Aceitar uma oferta: troca os `TeamId`s, remove listings, cancela outras ofertas pendentes dos membros envolvidos
- Transferência direta (admin): mesmo efeito, sem necessidade de oferta

### RevokedToken
JTI de tokens JWT revogados (logout). Limpeza periódica por `ExpiresAt`.

### AuditLog
Registro de ações administrativas e operações sensíveis.

```csharp
Id, ActorId (nullable), Username, Action, ResourceType, ResourceId,
StatusCode, Details (truncado a 1000 chars), OccurredAt
```

- Limpeza automática via `AuditLogCleanupService` (background service)
- Retenção configurável via `AUDIT_LOG_RETENTION_DAYS` (padrão: 90 dias)

### MapResult
Resultado de um mapa específico de uma partida.

```csharp
Id, GameId, MapNumber, Rounds, CreatedAt
```

- Relacionado a `MatchStats[]` dos jogadores naquele mapa
- Validações: número do mapa deve ser válido para o jogo, rounds > 0, mapa não pode ser registrado duas vezes

## Enums

```csharp
GameStatus:   Scheduled, InProgress, Finished
MarketStatus: Open, Closed, Settled, Voided
MarketType:   MapWinner, SeriesWinner, TopKills, MostDeaths, MostUtilityDamage
BetStatus:    Pending, Active, Settled, Cancelled, Voided
BetResult:    CreatorWon, CovererWon, Voided
InviteStatus: Pending, Used, Expired  (calculado, não persistido)
TradeOfferStatus: Pending, Accepted, Rejected, Cancelled
```

## Regras de Negócio Críticas

1. **Saldo reservado:** ao criar ou cobrir uma aposta, o valor é movido de `VirtualBalance` para `ReservedBalance`. A soma nunca muda.
2. **Liquidação:** vencedor recebe `VirtualBalance += 2×amount, ReservedBalance -= amount`. Perdedor: `ReservedBalance -= amount`.
3. **Apostas bloqueadas:** não é possível criar apostas em jogos com status `InProgress` ou `Finished`.
4. **Unicidade:** um usuário só pode ter uma aposta ativa (Pending ou Active) por mercado.
5. **Auto-cobertura proibida:** o criador não pode cobrir a própria aposta.
6. **Cancelamento:** apenas apostas `Pending` podem ser canceladas, apenas pelo criador.
7. **Convite:** o token é marcado como usado apenas após o usuário ser criado com sucesso.
8. **Líder único:** cada time pode ter no máximo um líder ativo. Designar novo líder remove o anterior.
9. **Remoção de time:** ao mover um usuário de time, `IsTeamLeader` é automaticamente removido e o `TradeListing` é cancelado.
10. **Master Admin:** configurado via `MasterAdminUsername` no `appsettings.json`. Não pode ser rebaixado por outros admins. Apenas o master admin pode promover/revogar outros admins.
11. **Resultado de mapa:** cada mapa de um jogo só pode ter um resultado registrado. O número do mapa deve ser válido (entre 1 e `NumberOfMaps` do jogo).

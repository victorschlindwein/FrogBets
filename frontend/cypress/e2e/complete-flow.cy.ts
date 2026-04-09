/// <reference types="cypress" />

/**
 * Bug Condition Exploration Test - E2E Complete Flow
 *
 * Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6
 *
 * Fluxo:
 * 1. Admin cria 4 convites e registra 4 usuários
 * 2. Admin cria 2 times (E2E_TeamA, E2E_TeamB)
 * 3. Admin adiciona 2 jogadores em cada time via POST /api/players
 * 4. Admin cria jogo entre os 2 times
 * 5. Verifica dropdowns de jogadores (GET /api/games/{id}/players)
 * 6. user1 cria aposta no MapWinner (Map 1) escolhendo E2E_TeamA
 * 7. user2 cobre a aposta
 * 8. Verifica que aposta sumiu do marketplace
 * 9. Admin inicia o jogo
 * 10. Admin registra resultado do MapWinner (Map 1) = E2E_TeamA
 * 11. Verifica liquidação: saldos e WinsCount/LossesCount
 * 12. Verifica invariante de saldo
 */

describe('E2E Complete Flow - Bug Condition Exploration', () => {
  const apiUrl = Cypress.env('apiUrl') || 'http://localhost:8080'

  const adminCredentials = {
    username: Cypress.env('adminUsername') || 'admin',
    password: Cypress.env('adminPassword') || 'admin123456',
  }

  const users = [
    { username: `e2e_u1_${Date.now()}`, password: 'password123' },
    { username: `e2e_u2_${Date.now()}`, password: 'password123' },
    { username: `e2e_u3_${Date.now()}`, password: 'password123' },
    { username: `e2e_u4_${Date.now()}`, password: 'password123' },
  ]

  // Usamos um único it() para manter estado entre steps
  it('executa o fluxo E2E completo', () => {
    const state: {
      adminToken: string
      userTokens: string[]
      userIds: string[]
      teamAId: string
      teamBId: string
      gameId: string
      betId: string
      mapWinnerMarketId: string
      initialBalances: { virtual: number; reserved: number }[]
    } = {
      adminToken: '',
      userTokens: [],
      userIds: [],
      teamAId: '',
      teamBId: '',
      gameId: '',
      betId: '',
      mapWinnerMarketId: '',
      initialBalances: [],
    }

    // ── Step 1: Admin login ──────────────────────────────────────────────────
    cy.request('POST', `${apiUrl}/api/auth/login`, adminCredentials).then((res) => {
      expect(res.status).to.eq(200)
      state.adminToken = res.body.token
      cy.log('✅ Step 1: Admin logado')
    })

    // ── Step 2: Criar 4 convites e registrar 4 usuários ─────────────────────
    .then(() => {
      const registerUser = (index: number): Cypress.Chainable => {
        return cy.request({
          method: 'POST',
          url: `${apiUrl}/api/invites`,
          headers: { Authorization: `Bearer ${state.adminToken}` },
          body: { description: `E2E User ${index + 1}` },
        }).then((invRes) => {
          expect(invRes.status).to.eq(201)
          // A resposta é { tokens: ["..."] }
          const inviteToken = invRes.body.tokens[0]
          cy.log(`✅ Step 2.${index + 1}: Convite criado: ${inviteToken}`)

          return cy.request({
            method: 'POST',
            url: `${apiUrl}/api/auth/register`,
            body: {
              InviteToken: inviteToken,
              Username: users[index].username,
              Password: users[index].password,
            },
          }).then((regRes) => {
            expect(regRes.status).to.eq(200)
            state.userTokens[index] = regRes.body.token
            cy.log(`✅ Step 2.${index + 1}: Usuário ${users[index].username} registrado`)

            return cy.request({
              method: 'GET',
              url: `${apiUrl}/api/users/me`,
              headers: { Authorization: `Bearer ${regRes.body.token}` },
            }).then((meRes) => {
              state.userIds[index] = meRes.body.id
              cy.log(`   ID: ${meRes.body.id}`)
            })
          })
        })
      }

      return registerUser(0)
        .then(() => registerUser(1))
        .then(() => registerUser(2))
        .then(() => registerUser(3))
    })

    // ── Step 3: Criar 2 times ────────────────────────────────────────────────
    .then(() => {
      const ts = Date.now()
      return cy.request({
        method: 'POST',
        url: `${apiUrl}/api/teams`,
        headers: { Authorization: `Bearer ${state.adminToken}` },
        body: { name: `E2E_TeamA_${ts}`, logoUrl: null },
      }).then((res) => {
        expect(res.status).to.eq(201)
        state.teamAId = res.body.id
        cy.log(`✅ Step 3: TeamA criado (ID: ${state.teamAId})`)

        return cy.request({
          method: 'POST',
          url: `${apiUrl}/api/teams`,
          headers: { Authorization: `Bearer ${state.adminToken}` },
          body: { name: `E2E_TeamB_${ts}`, logoUrl: null },
        }).then((res2) => {
          expect(res2.status).to.eq(201)
          state.teamBId = res2.body.id
          cy.log(`✅ Step 3: TeamB criado (ID: ${state.teamBId})`)
        })
      })
    })

    // ── Step 4: Adicionar 2 jogadores em cada time via POST /api/players ─────
    .then(() => {
      const addPlayer = (userId: string, teamId: string, label: string): Cypress.Chainable => {
        return cy.request({
          method: 'POST',
          url: `${apiUrl}/api/players`,
          headers: { Authorization: `Bearer ${state.adminToken}` },
          body: { userId, teamId },
        }).then((res) => {
          expect(res.status).to.eq(201)
          cy.log(`✅ Step 4: ${label} adicionado ao time`)
        })
      }

      return addPlayer(state.userIds[0], state.teamAId, 'User1 → TeamA')
        .then(() => addPlayer(state.userIds[1], state.teamAId, 'User2 → TeamA'))
        .then(() => addPlayer(state.userIds[2], state.teamBId, 'User3 → TeamB'))
        .then(() => addPlayer(state.userIds[3], state.teamBId, 'User4 → TeamB'))
    })

    // ── Step 5: Criar jogo entre os 2 times ─────────────────────────────────
    .then(() => {
      // Buscar os nomes dos times para usar na criação do jogo
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/teams`,
        headers: { Authorization: `Bearer ${state.adminToken}` },
      }).then((res) => {
        const teamA = res.body.find((t: any) => t.id === state.teamAId)
        const teamB = res.body.find((t: any) => t.id === state.teamBId)

        const scheduledAt = new Date(Date.now() + 86400000).toISOString()

        return cy.request({
          method: 'POST',
          url: `${apiUrl}/api/games`,
          headers: { Authorization: `Bearer ${state.adminToken}` },
          body: {
            teamA: teamA.name,
            teamB: teamB.name,
            scheduledAt,
            numberOfMaps: 3,
          },
        }).then((gameRes) => {
          expect(gameRes.status).to.eq(201)
          state.gameId = gameRes.body.id
          cy.log(`✅ Step 5: Jogo criado (ID: ${state.gameId}) — ${teamA.name} vs ${teamB.name}`)
        })
      })
    })

    // ── Step 6: Verificar dropdowns de jogadores ─────────────────────────────
    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/games/${state.gameId}/players`,
        headers: { Authorization: `Bearer ${state.userTokens[0]}` },
        failOnStatusCode: false,
      }).then((res) => {
        if (res.status === 404) {
          cy.log('❌ BUG: Endpoint /api/games/{id}/players retornou 404')
          throw new Error('BUG: Endpoint /api/games/{id}/players não existe')
        }

        expect(res.status).to.eq(200)
        const players = res.body

        cy.log(`📊 Step 6: ${players.length} jogadores retornados`)
        players.forEach((p: any) => cy.log(`   - ${p.nickname} (${p.teamName})`))

        expect(players.length, 'Deve retornar exatamente 4 jogadores').to.eq(4)
        cy.log('✅ Step 6: Dropdowns de jogadores OK')
      })
    })

    // ── Step 7: Capturar saldos iniciais ─────────────────────────────────────
    .then(() => {
      const getBalance = (index: number): Cypress.Chainable => {
        return cy.request({
          method: 'GET',
          url: `${apiUrl}/api/users/me/balance`,
          headers: { Authorization: `Bearer ${state.userTokens[index]}` },
        }).then((res) => {
          state.initialBalances[index] = {
            virtual: res.body.virtualBalance,
            reserved: res.body.reservedBalance,
          }
          cy.log(`💰 User${index + 1} saldo inicial: V=${res.body.virtualBalance} R=${res.body.reservedBalance}`)
        })
      }

      return getBalance(0).then(() => getBalance(1))
    })

    // ── Step 8: user1 cria aposta no MapWinner (Map 1) ───────────────────────
    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/games/${state.gameId}`,
        headers: { Authorization: `Bearer ${state.userTokens[0]}` },
      }).then((res) => {
        const game = res.body
        const market = game.markets.find((m: any) => m.type === 'MapWinner' && m.mapNumber === 1)
        expect(market, 'Mercado MapWinner Map 1 deve existir').to.exist
        state.mapWinnerMarketId = market.id

        // Buscar o nome do TeamA para usar como opção
        return cy.request({
          method: 'GET',
          url: `${apiUrl}/api/teams`,
          headers: { Authorization: `Bearer ${state.adminToken}` },
        }).then((teamsRes) => {
          const teamA = teamsRes.body.find((t: any) => t.id === state.teamAId)

          return cy.request({
            method: 'POST',
            url: `${apiUrl}/api/bets`,
            headers: { Authorization: `Bearer ${state.userTokens[0]}` },
            body: {
              marketId: market.id,
              creatorOption: teamA.name,
              amount: 100,
            },
          }).then((betRes) => {
            expect(betRes.status).to.eq(201)
            state.betId = betRes.body.id
            cy.log(`✅ Step 8: Aposta criada (ID: ${state.betId}) — ${teamA.name} por 100`)
          })
        })
      })
    })

    // ── Step 9: Verificar aposta no marketplace ──────────────────────────────
    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/marketplace`,
        headers: { Authorization: `Bearer ${state.userTokens[1]}` },
      }).then((res) => {
        expect(res.status).to.eq(200)
        const bet = res.body.find((b: any) => b.id === state.betId)
        expect(bet, 'Aposta deve aparecer no marketplace').to.exist
        expect(bet.status).to.eq('Pending')
        cy.log('✅ Step 9: Aposta visível no marketplace com status Pending')
      })
    })

    // ── Step 10: user2 cobre a aposta ────────────────────────────────────────
    .then(() => {
      return cy.request({
        method: 'POST',
        url: `${apiUrl}/api/bets/${state.betId}/cover`,
        headers: { Authorization: `Bearer ${state.userTokens[1]}` },
      }).then((res) => {
        expect(res.status).to.eq(200)
        cy.log('✅ Step 10: user2 cobriu a aposta')
      })
    })

    // ── Step 11: Verificar que aposta sumiu do marketplace ───────────────────
    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/marketplace`,
        headers: { Authorization: `Bearer ${state.userTokens[1]}` },
      }).then((res) => {
        expect(res.status).to.eq(200)
        const bet = res.body.find((b: any) => b.id === state.betId)
        if (bet) {
          cy.log('❌ BUG: Aposta ainda aparece no marketplace após cobertura')
          throw new Error('BUG: Aposta coberta ainda visível no marketplace')
        }
        cy.log('✅ Step 11: Aposta removida do marketplace após cobertura')
      })
    })

    // ── Step 12: Verificar saldos após cobertura ─────────────────────────────
    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/users/me/balance`,
        headers: { Authorization: `Bearer ${state.userTokens[0]}` },
      }).then((res) => {
        const b = res.body
        expect(b.virtualBalance, 'User1: virtual deve ter diminuído 100').to.eq(state.initialBalances[0].virtual - 100)
        expect(b.reservedBalance, 'User1: reservado deve ter aumentado 100').to.eq(state.initialBalances[0].reserved + 100)
        cy.log(`✅ Step 12: User1 saldo OK — V=${b.virtualBalance} R=${b.reservedBalance}`)
      })
    })

    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/users/me/balance`,
        headers: { Authorization: `Bearer ${state.userTokens[1]}` },
      }).then((res) => {
        const b = res.body
        expect(b.virtualBalance, 'User2: virtual deve ter diminuído 100').to.eq(state.initialBalances[1].virtual - 100)
        expect(b.reservedBalance, 'User2: reservado deve ter aumentado 100').to.eq(state.initialBalances[1].reserved + 100)
        cy.log(`✅ Step 12: User2 saldo OK — V=${b.virtualBalance} R=${b.reservedBalance}`)
      })
    })

    // ── Step 13: Admin inicia o jogo ─────────────────────────────────────────
    .then(() => {
      return cy.request({
        method: 'PATCH',
        url: `${apiUrl}/api/games/${state.gameId}/start`,
        headers: { Authorization: `Bearer ${state.adminToken}` },
      }).then((res) => {
        expect(res.status).to.eq(204)
        cy.log('✅ Step 13: Jogo iniciado (InProgress)')
      })
    })

    // ── Step 14: Admin registra resultado MapWinner Map 1 = TeamA ────────────
    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/teams`,
        headers: { Authorization: `Bearer ${state.adminToken}` },
      }).then((teamsRes) => {
        const teamA = teamsRes.body.find((t: any) => t.id === state.teamAId)

        return cy.request({
          method: 'POST',
          url: `${apiUrl}/api/games/${state.gameId}/results`,
          headers: { Authorization: `Bearer ${state.adminToken}` },
          body: {
            marketId: state.mapWinnerMarketId,
            winningOption: teamA.name,
            mapNumber: 1,
          },
        }).then((res) => {
          expect(res.status).to.eq(204)
          cy.log(`✅ Step 14: Resultado registrado — ${teamA.name} venceu o Map 1`)
        })
      })
    })

    // ── Step 15: Verificar liquidação — saldos ───────────────────────────────
    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/users/me/balance`,
        headers: { Authorization: `Bearer ${state.userTokens[0]}` },
      }).then((res) => {
        const b = res.body
        const expectedVirtual = state.initialBalances[0].virtual + 100 // ganhou 2×100, já tinha -100
        expect(b.virtualBalance, 'User1 (vencedor): deve ter recebido 2× o valor').to.eq(expectedVirtual)
        expect(b.reservedBalance, 'User1: reservado deve voltar ao inicial').to.eq(state.initialBalances[0].reserved)
        cy.log(`✅ Step 15: User1 (vencedor) saldo OK — V=${b.virtualBalance}`)
      })
    })

    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/users/me/balance`,
        headers: { Authorization: `Bearer ${state.userTokens[1]}` },
      }).then((res) => {
        const b = res.body
        const expectedVirtual = state.initialBalances[1].virtual - 100 // perdeu 100
        expect(b.virtualBalance, 'User2 (perdedor): deve ter perdido o valor').to.eq(expectedVirtual)
        expect(b.reservedBalance, 'User2: reservado deve voltar ao inicial').to.eq(state.initialBalances[1].reserved)
        cy.log(`✅ Step 15: User2 (perdedor) saldo OK — V=${b.virtualBalance}`)
      })
    })

    // ── Step 16: Verificar WinsCount/LossesCount ─────────────────────────────
    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/users/me`,
        headers: { Authorization: `Bearer ${state.userTokens[0]}` },
      }).then((res) => {
        const u = res.body
        if (u.winsCount === 0) {
          cy.log('❌ BUG: WinsCount do user1 não foi incrementado após vitória')
          throw new Error('BUG: WinsCount não atualizado após liquidação')
        }
        expect(u.winsCount).to.be.greaterThan(0)
        cy.log(`✅ Step 16: User1 WinsCount=${u.winsCount}`)
      })
    })

    .then(() => {
      return cy.request({
        method: 'GET',
        url: `${apiUrl}/api/users/me`,
        headers: { Authorization: `Bearer ${state.userTokens[1]}` },
      }).then((res) => {
        const u = res.body
        if (u.lossesCount === 0) {
          cy.log('❌ BUG: LossesCount do user2 não foi incrementado após derrota')
          throw new Error('BUG: LossesCount não atualizado após liquidação')
        }
        expect(u.lossesCount).to.be.greaterThan(0)
        cy.log(`✅ Step 16: User2 LossesCount=${u.lossesCount}`)
      })
    })

    // ── Step 17: Verificar invariante de saldo ───────────────────────────────
    .then(() => {
      const checkInvariant = (index: number, expectedDelta: number): Cypress.Chainable => {
        return cy.request({
          method: 'GET',
          url: `${apiUrl}/api/users/me/balance`,
          headers: { Authorization: `Bearer ${state.userTokens[index]}` },
        }).then((res) => {
          const b = res.body
          const total = b.virtualBalance + b.reservedBalance
          const initialTotal = state.initialBalances[index].virtual + state.initialBalances[index].reserved
          const expectedTotal = initialTotal + expectedDelta
          expect(total, `User${index + 1}: total deve ser ${expectedTotal}`).to.eq(expectedTotal)
          cy.log(`✅ Step 17: User${index + 1} invariante OK — total=${total}`)
        })
      }

      return checkInvariant(0, 100)  // user1 ganhou 100
        .then(() => checkInvariant(1, -100)) // user2 perdeu 100
    })

    .then(() => {
      cy.log('🎉 Fluxo E2E completo executado com sucesso!')
    })
  })
})

/// <reference types="cypress" />

const mockBets = [
  {
    id: 'bet-1',
    marketId: 'mkt-1',
    creatorOption: 'TeamA',
    covererOption: null,
    amount: 100,
    status: 'Pending',
    coveredById: null,
    market: { type: 'MapWinner', mapNumber: 1, gameId: 'game-1' },
  },
  {
    id: 'bet-2',
    marketId: 'mkt-2',
    creatorOption: 'TeamB',
    covererOption: 'TeamA',
    amount: 200,
    status: 'Active',
    coveredById: 'user-99',
    market: { type: 'SeriesWinner', mapNumber: null, gameId: 'game-1' },
  },
  {
    id: 'bet-3',
    marketId: 'mkt-3',
    creatorOption: 'PlayerX',
    covererOption: 'NOT_PlayerX',
    amount: 50,
    status: 'Settled',
    coveredById: 'user-88',
    market: { type: 'TopKills', mapNumber: 2, gameId: 'game-2' },
  },
]

describe('Minhas Apostas', () => {
  beforeEach(() => {
    cy.window().then((win) => {
      win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
    })
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
    })
    cy.intercept('GET', '/api/bets', { statusCode: 200, body: mockBets }).as('getBets')
  })

  it('exibe as três seções de apostas', () => {
    cy.visit('/bets')
    cy.wait('@getBets')
    cy.contains('Pendentes').should('be.visible')
    cy.contains('Ativas').should('be.visible')
    cy.contains(/Liquidadas/).should('be.visible')
  })

  it('exibe detalhes de cada aposta', () => {
    cy.visit('/bets')
    cy.wait('@getBets')
    cy.contains('Vencedor do Mapa').should('be.visible')
    cy.contains('TeamA').should('be.visible')
    cy.contains('100').should('be.visible')
  })

  it('exibe botão cancelar apenas para apostas pendentes', () => {
    cy.visit('/bets')
    cy.wait('@getBets')
    cy.get('button').contains('Cancelar').should('have.length', 1)
  })

  it('cancela aposta pendente e remove da lista', () => {
    cy.intercept('DELETE', '/api/bets/bet-1', { statusCode: 204 }).as('cancel')

    cy.visit('/bets')
    cy.wait('@getBets')
    cy.get('button').contains('Cancelar').click()
    cy.wait('@cancel')
    cy.contains('Nenhuma aposta pendente.').should('be.visible')
  })

  it('exibe mensagem de erro ao cancelar quando API falha', () => {
    cy.intercept('DELETE', '/api/bets/bet-1', { statusCode: 500 })

    cy.visit('/bets')
    cy.wait('@getBets')
    cy.get('button').contains('Cancelar').click()
    cy.get('[role="alert"]').should('be.visible')
  })

  it('exibe mensagem quando não há apostas', () => {
    cy.intercept('GET', '/api/bets', { statusCode: 200, body: [] })

    cy.visit('/bets')
    cy.contains('Nenhuma aposta pendente.').should('be.visible')
    cy.contains('Nenhuma aposta ativa.').should('be.visible')
  })
})

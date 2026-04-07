/// <reference types="cypress" />

const mockBets = [
  {
    id: 'mp-bet-1',
    marketId: 'mkt-1',
    creatorOption: 'TeamA',
    amount: 150,
    creatorId: 'other-user',
    market: { type: 'MapWinner', mapNumber: 1, gameId: 'game-1' },
  },
  {
    id: 'mp-bet-2',
    marketId: 'mkt-2',
    creatorOption: 'PlayerX',
    amount: 75,
    creatorId: 'other-user-2',
    market: { type: 'TopKills', mapNumber: 2, gameId: 'game-1' },
  },
]

describe('Marketplace', () => {
  beforeEach(() => {
    cy.window().then((win) => {
      win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
    })
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
    })
    cy.intercept('GET', '/api/trades/listings', { statusCode: 200, body: [] })
    cy.intercept('GET', '/api/marketplace', { statusCode: 200, body: mockBets }).as('getMarketplace')
  })

  it('lista apostas disponíveis para cobertura', () => {
    cy.visit('/marketplace')
    cy.wait('@getMarketplace')
    cy.contains('Vencedor do Mapa').should('be.visible')
    cy.contains('150').should('be.visible')
    cy.contains('Top Kills').should('be.visible')
    cy.contains('75').should('be.visible')
  })

  it('exibe botão Cobrir para cada aposta', () => {
    cy.visit('/marketplace')
    cy.wait('@getMarketplace')
    cy.get('button').contains('Cobrir').should('have.length', 2)
  })

  it('exibe confirmação antes de cobrir', () => {
    cy.visit('/marketplace')
    cy.wait('@getMarketplace')
    cy.get('button').contains('Cobrir').first().click()
    cy.contains('Confirmar').should('be.visible')
    cy.contains('Cancelar').should('be.visible')
  })

  it('cancela cobertura ao clicar em Cancelar', () => {
    cy.visit('/marketplace')
    cy.wait('@getMarketplace')
    cy.get('button').contains('Cobrir').first().click()
    cy.contains('Cancelar').click()
    cy.get('button').contains('Cobrir').should('have.length', 2)
  })

  it('cobre aposta com sucesso e remove da lista', () => {
    cy.intercept('POST', '/api/bets/mp-bet-1/cover', { statusCode: 200 }).as('cover')

    cy.visit('/marketplace')
    cy.wait('@getMarketplace')
    cy.get('button').contains('Cobrir').first().click()
    cy.contains('Confirmar').click()
    cy.wait('@cover')
    cy.get('button').contains('Cobrir').should('have.length', 1)
  })

  it('exibe erro ao cobrir quando API falha', () => {
    cy.intercept('POST', '/api/bets/mp-bet-1/cover', { statusCode: 400, body: { error: { code: 'INSUFFICIENT_BALANCE', message: 'Saldo insuficiente.' } } })

    cy.visit('/marketplace')
    cy.wait('@getMarketplace')
    cy.get('button').contains('Cobrir').first().click()
    cy.contains('Confirmar').click()
    cy.get('[role="alert"]').should('be.visible')
  })

  it('exibe mensagem quando não há apostas disponíveis', () => {
    cy.intercept('GET', '/api/marketplace', { statusCode: 200, body: [] })
    cy.visit('/marketplace')
    cy.contains('Nenhuma aposta disponível').should('be.visible')
  })
})

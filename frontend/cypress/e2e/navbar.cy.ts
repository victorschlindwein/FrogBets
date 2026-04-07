/// <reference types="cypress" />

describe('Navbar', () => {
  beforeEach(() => {
    cy.window().then((win) => {
      win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
    })
    cy.intercept('GET', '/api/users/me/balance', {
      statusCode: 200,
      body: { virtualBalance: 1000, reservedBalance: 0 },
    })
  })

  it('exibe links de navegação para usuário comum', () => {
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
    })

    cy.visit('/')
    cy.contains('Dashboard').should('be.visible')
    cy.contains('Jogos').should('be.visible')
    cy.contains('Minhas Apostas').should('be.visible')
    cy.contains('Marketplace').should('be.visible')
    cy.contains('Ranking Apostas').should('be.visible')
    cy.contains('Ranking CS2').should('be.visible')
    cy.contains('Admin').should('not.exist')
  })

  it('exibe link Admin para usuário admin', () => {
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'admin', isAdmin: true, isTeamLeader: false, createdAt: new Date().toISOString() },
    })

    cy.visit('/')
    cy.contains('Admin').should('be.visible')
  })

  it('exibe o username do usuário logado', () => {
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'vitao', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
    })

    cy.visit('/')
    cy.contains('vitao').should('be.visible')
  })

  it('navega para a página correta ao clicar nos links', () => {
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
    })
    cy.intercept('GET', '/api/bets', { statusCode: 200, body: [] })

    cy.visit('/')
    cy.contains('Minhas Apostas').click()
    cy.url().should('include', '/bets')
  })
})

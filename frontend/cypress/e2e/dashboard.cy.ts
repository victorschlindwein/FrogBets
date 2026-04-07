/// <reference types="cypress" />

describe('Dashboard', () => {
  beforeEach(() => {
    cy.window().then((win) => {
      win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
    })
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
    })
  })

  it('exibe saldo disponível e reservado', () => {
    cy.intercept('GET', '/api/users/me/balance', {
      statusCode: 200,
      body: { virtualBalance: 850, reservedBalance: 150 },
    })

    cy.visit('/')
    cy.contains('Saldo Disponível').should('be.visible')
    cy.contains('850').should('be.visible')
    cy.contains('Saldo Reservado').should('be.visible')
    cy.contains('150').should('be.visible')
  })

  it('exibe mensagem de erro quando API falha', () => {
    cy.intercept('GET', '/api/users/me/balance', { statusCode: 500 })

    cy.visit('/')
    cy.get('[role="alert"]').should('be.visible')
  })

  it('exibe estado de carregamento antes dos dados chegarem', () => {
    cy.intercept('GET', '/api/users/me/balance', (req) => {
      req.reply({ delay: 500, statusCode: 200, body: { virtualBalance: 1000, reservedBalance: 0 } })
    })

    cy.visit('/')
    cy.contains('Carregando saldo').should('be.visible')
    cy.contains('1000').should('be.visible')
  })
})

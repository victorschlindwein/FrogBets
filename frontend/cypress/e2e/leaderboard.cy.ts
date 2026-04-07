/// <reference types="cypress" />

const mockEntries = [
  { username: 'alice', virtualBalance: 1500, winsCount: 5, lossesCount: 2 },
  { username: 'bob', virtualBalance: 1200, winsCount: 3, lossesCount: 3 },
  { username: 'carol', virtualBalance: 800, winsCount: 1, lossesCount: 4 },
]

describe('Leaderboard', () => {
  beforeEach(() => {
    cy.window().then((win) => {
      win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
    })
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
    })
    cy.intercept('GET', '/api/leaderboard', { statusCode: 200, body: mockEntries }).as('getLeaderboard')
  })

  it('exibe tabela com colunas corretas', () => {
    cy.visit('/leaderboard')
    cy.wait('@getLeaderboard')
    cy.contains('Posição').should('be.visible')
    cy.contains('Usuário').should('be.visible')
    cy.contains('Saldo Virtual').should('be.visible')
    cy.contains('Vitórias').should('be.visible')
    cy.contains('Derrotas').should('be.visible')
  })

  it('exibe usuários em ordem de saldo decrescente', () => {
    cy.visit('/leaderboard')
    cy.wait('@getLeaderboard')

    cy.get('tbody tr').eq(0).should('contain', 'alice').and('contain', '1500')
    cy.get('tbody tr').eq(1).should('contain', 'bob').and('contain', '1200')
    cy.get('tbody tr').eq(2).should('contain', 'carol').and('contain', '800')
  })

  it('exibe medalhas para os três primeiros', () => {
    cy.visit('/leaderboard')
    cy.wait('@getLeaderboard')
    cy.get('tbody tr').eq(0).should('contain', '🥇')
    cy.get('tbody tr').eq(1).should('contain', '🥈')
    cy.get('tbody tr').eq(2).should('contain', '🥉')
  })

  it('exibe mensagem quando não há usuários', () => {
    cy.intercept('GET', '/api/leaderboard', { statusCode: 200, body: [] })
    cy.visit('/leaderboard')
    cy.contains('Nenhum usuário encontrado').should('be.visible')
  })

  it('exibe erro quando API falha', () => {
    cy.intercept('GET', '/api/leaderboard', { statusCode: 500 })
    cy.visit('/leaderboard')
    cy.get('[role="alert"]').should('be.visible')
  })
})

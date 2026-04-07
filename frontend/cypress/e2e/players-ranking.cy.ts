/// <reference types="cypress" />

const mockRanking = [
  { position: 1, playerId: 'p1', nickname: 's1mple', teamName: 'FrogTeam', playerScore: 1.35, matchesCount: 10 },
  { position: 2, playerId: 'p2', nickname: 'NiKo', teamName: 'RivalTeam', playerScore: 1.28, matchesCount: 8 },
  { position: 3, playerId: 'p3', nickname: 'device', teamName: 'FrogTeam', playerScore: 1.15, matchesCount: 12 },
]

describe('Ranking CS2', () => {
  beforeEach(() => {
    cy.window().then((win) => {
      win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
    })
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
    })
    cy.intercept('GET', '/api/players/ranking', { statusCode: 200, body: mockRanking }).as('getRanking')
  })

  it('exibe tabela com colunas corretas', () => {
    cy.visit('/players/ranking')
    cy.wait('@getRanking')
    cy.contains('#').should('be.visible')
    cy.contains('Nickname').should('be.visible')
    cy.contains('Time').should('be.visible')
    cy.contains('Score').should('be.visible')
    cy.contains('Partidas').should('be.visible')
  })

  it('exibe jogadores em ordem de score', () => {
    cy.visit('/players/ranking')
    cy.wait('@getRanking')
    cy.get('tbody tr').eq(0).should('contain', 's1mple').and('contain', '1.3500')
    cy.get('tbody tr').eq(1).should('contain', 'NiKo')
    cy.get('tbody tr').eq(2).should('contain', 'device')
  })

  it('exibe medalhas para os três primeiros', () => {
    cy.visit('/players/ranking')
    cy.wait('@getRanking')
    cy.get('tbody tr').eq(0).should('contain', '🥇')
    cy.get('tbody tr').eq(1).should('contain', '🥈')
    cy.get('tbody tr').eq(2).should('contain', '🥉')
  })

  it('exibe mensagem quando não há jogadores', () => {
    cy.intercept('GET', '/api/players/ranking', { statusCode: 200, body: [] })
    cy.visit('/players/ranking')
    cy.contains('Nenhum jogador encontrado').should('be.visible')
  })

  it('exibe erro quando API falha', () => {
    cy.intercept('GET', '/api/players/ranking', { statusCode: 500 })
    cy.visit('/players/ranking')
    cy.get('[role="alert"]').should('be.visible')
  })
})

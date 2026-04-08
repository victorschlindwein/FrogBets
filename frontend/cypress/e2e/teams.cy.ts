/// <reference types="cypress" />

const teams = [
  { id: 'team-1', name: 'Frog Alpha', logoUrl: 'https://example.com/logo.png', createdAt: '2024-01-01' },
  { id: 'team-2', name: 'Frog Beta', logoUrl: null, createdAt: '2024-01-01' },
]

const members = [
  { id: 'u-1', username: 'xXx_Frog', isTeamLeader: false },
]

describe('Times', () => {
  beforeEach(() => {
    cy.window().then((win) => win.sessionStorage.clear())
  })

  // ── Proteção de rota ──────────────────────────────────────────────────────

  it('/teams sem autenticação redireciona para /login', () => {
    cy.visit('/teams')
    cy.url().should('include', '/login')
  })

  // ── Fluxo autenticado ─────────────────────────────────────────────────────

  it('página /teams carrega com times quando autenticado', () => {
    cy.window().then((win) => {
      win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
    })

    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: 'u-1', username: 'player', isAdmin: false, isTeamLeader: false, teamId: null },
    }).as('getMe')

    cy.intercept('GET', '/api/teams', {
      statusCode: 200,
      body: teams,
    }).as('getTeams')

    cy.intercept('GET', '/api/teams/*/members', {
      statusCode: 200,
      body: members,
    }).as('getMembers')

    cy.visit('/teams')

    cy.wait('@getTeams')
    cy.get('[role="article"]').should('have.length', 2)
    cy.contains('Frog Alpha').should('be.visible')
    cy.contains('Frog Beta').should('be.visible')
  })

  // ── Navbar ────────────────────────────────────────────────────────────────

  it('link "Times" visível na navbar quando autenticado', () => {
    cy.window().then((win) => {
      win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
    })

    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: 'u-1', username: 'player', isAdmin: false, isTeamLeader: false, teamId: null },
    })

    cy.intercept('GET', '/api/users/me/balance', {
      statusCode: 200,
      body: { virtualBalance: 1000, reservedBalance: 0 },
    })

    cy.visit('/')
    cy.contains('a', 'Times').should('be.visible')
  })
})

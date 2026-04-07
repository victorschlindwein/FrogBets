/// <reference types="cypress" />

const mockGames = [
  {
    id: 'game-1',
    teamA: 'FrogTeam',
    teamB: 'RivalTeam',
    scheduledAt: new Date(Date.now() + 86400000).toISOString(),
    numberOfMaps: 3,
    status: 'Scheduled',
    markets: [
      { id: 'mkt-1', type: 'MapWinner', mapNumber: 1, status: 'Open', winningOption: null },
      { id: 'mkt-2', type: 'SeriesWinner', mapNumber: null, status: 'Open', winningOption: null },
    ],
  },
  {
    id: 'game-2',
    teamA: 'TeamAlpha',
    teamB: 'TeamBeta',
    scheduledAt: new Date(Date.now() - 86400000).toISOString(),
    numberOfMaps: 1,
    status: 'Finished',
    markets: [],
  },
]

describe('Jogos', () => {
  beforeEach(() => {
    cy.window().then((win) => {
      win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
    })
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
    })
    cy.intercept('GET', '/api/games', { statusCode: 200, body: mockGames }).as('getGames')
  })

  it('lista todos os jogos', () => {
    cy.visit('/games')
    cy.wait('@getGames')
    cy.contains('FrogTeam vs RivalTeam').should('be.visible')
    cy.contains('TeamAlpha vs TeamBeta').should('be.visible')
  })

  it('exibe status de cada jogo', () => {
    cy.visit('/games')
    cy.wait('@getGames')
    cy.contains('Agendado').should('be.visible')
    cy.contains('Finalizado').should('be.visible')
  })

  it('exibe mensagem quando não há jogos', () => {
    cy.intercept('GET', '/api/games', { statusCode: 200, body: [] })
    cy.visit('/games')
    cy.contains('Nenhum jogo disponível').should('be.visible')
  })

  it('navega para detalhe do jogo ao clicar', () => {
    cy.intercept('GET', '/api/games/game-1', { statusCode: 200, body: mockGames[0] })

    cy.visit('/games')
    cy.wait('@getGames')
    cy.contains('FrogTeam vs RivalTeam').click()
    cy.url().should('include', '/games/game-1')
  })
})

describe('Detalhe do Jogo', () => {
  beforeEach(() => {
    cy.window().then((win) => {
      win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
    })
    cy.intercept('GET', '/api/users/me', {
      statusCode: 200,
      body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
    })
    cy.intercept('GET', '/api/games/game-1', { statusCode: 200, body: mockGames[0] }).as('getGame')
  })

  it('exibe mercados abertos do jogo', () => {
    cy.visit('/games/game-1')
    cy.wait('@getGame')
    cy.contains('Vencedor do Mapa').should('be.visible')
    cy.contains('Vencedor da Série').should('be.visible')
  })

  it('exibe formulário de aposta para jogo agendado', () => {
    cy.visit('/games/game-1')
    cy.wait('@getGame')
    cy.get('button').contains('Apostar').should('exist')
  })

  it('cria aposta com sucesso', () => {
    cy.intercept('POST', '/api/bets', {
      statusCode: 201,
      body: { id: 'new-bet', marketId: 'mkt-1', creatorOption: 'FrogTeam', amount: 100, status: 'Pending' },
    }).as('createBet')

    cy.visit('/games/game-1')
    cy.wait('@getGame')

    // Select option and amount for first market
    cy.get('select').first().select('FrogTeam')
    cy.get('input[type="number"]').first().clear().type('100')
    cy.get('button').contains('Apostar').first().click()

    cy.wait('@createBet')
    cy.get('[role="status"]').should('contain', 'Aposta criada')
  })

  it('exibe erro ao criar aposta com saldo insuficiente', () => {
    cy.intercept('POST', '/api/bets', {
      statusCode: 400,
      body: { error: { code: 'INSUFFICIENT_BALANCE', message: 'Saldo Virtual insuficiente.' } },
    })

    cy.visit('/games/game-1')
    cy.wait('@getGame')
    cy.get('select').first().select('FrogTeam')
    cy.get('input[type="number"]').first().clear().type('99999')
    cy.get('button').contains('Apostar').first().click()

    cy.get('[role="alert"]').should('be.visible')
  })

  it('exibe 404 para jogo inexistente', () => {
    cy.intercept('GET', '/api/games/nonexistent', { statusCode: 404 })
    cy.visit('/games/nonexistent')
    cy.get('[role="alert"]').should('be.visible')
  })
})

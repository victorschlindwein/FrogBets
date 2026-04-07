/// <reference types="cypress" />

describe('Autenticação', () => {
  beforeEach(() => {
    cy.window().then((win) => win.sessionStorage.clear())
  })

  // ── Login ─────────────────────────────────────────────────────────────────

  describe('Login', () => {
    it('exibe o formulário de login', () => {
      cy.visit('/login')
      cy.get('#username').should('exist')
      cy.get('#password').should('exist')
      cy.get('button[type="submit"]').should('contain', 'Entrar')
    })

    it('redireciona para / após login bem-sucedido', () => {
      cy.intercept('POST', '/api/auth/login', {
        statusCode: 200,
        body: { token: 'fake.jwt.token', expiresAt: new Date(Date.now() + 3600000).toISOString() },
      }).as('login')

      cy.visit('/login')
      cy.get('#username').type('testuser')
      cy.get('#password').type('password123')
      cy.get('button[type="submit"]').click()

      cy.wait('@login')
      cy.url().should('eq', Cypress.config('baseUrl') + '/')
    })

    it('exibe erro com credenciais inválidas', () => {
      cy.intercept('POST', '/api/auth/login', {
        statusCode: 401,
        body: { error: { code: 'INVALID_CREDENTIALS', message: 'Credenciais inválidas' } },
      }).as('loginFail')

      cy.visit('/login')
      cy.get('#username').type('wronguser')
      cy.get('#password').type('wrongpass')
      cy.get('button[type="submit"]').click()

      cy.wait('@loginFail')
      cy.get('[role="alert"]').should('be.visible')
    })

    it('não armazena token no localStorage', () => {
      cy.intercept('POST', '/api/auth/login', {
        statusCode: 200,
        body: { token: 'fake.jwt.token', expiresAt: new Date(Date.now() + 3600000).toISOString() },
      })

      cy.visit('/login')
      cy.get('#username').type('testuser')
      cy.get('#password').type('password123')
      cy.get('button[type="submit"]').click()

      cy.window().then((win) => {
        expect(win.localStorage.getItem('token')).to.be.null
        expect(win.localStorage.getItem('frogbets_token')).to.be.null
      })
    })
  })

  // ── Registro ──────────────────────────────────────────────────────────────

  describe('Registro', () => {
    it('exibe o formulário de registro', () => {
      cy.intercept('GET', '/api/teams', { statusCode: 200, body: [] })
      cy.visit('/register')
      cy.get('#inviteToken').should('exist')
      cy.get('#username').should('exist')
      cy.get('#password').should('exist')
    })

    it('exibe erro com convite inválido', () => {
      cy.intercept('GET', '/api/teams', { statusCode: 200, body: [] })
      cy.intercept('POST', '/api/auth/register', {
        statusCode: 400,
        body: { error: { code: 'INVALID_INVITE', message: 'Convite inválido ou expirado.' } },
      }).as('register')

      cy.visit('/register')
      cy.get('#inviteToken').type('invalidtoken')
      cy.get('#username').type('newuser')
      cy.get('#password').type('password123')
      cy.get('button[type="submit"]').click()

      cy.wait('@register')
      cy.get('[role="alert"]').should('contain', 'Convite inválido')
    })

    it('exibe erro com username já em uso', () => {
      cy.intercept('GET', '/api/teams', { statusCode: 200, body: [] })
      cy.intercept('POST', '/api/auth/register', {
        statusCode: 409,
        body: { error: { code: 'USERNAME_TAKEN', message: 'Nome de usuário já está em uso.' } },
      })

      cy.visit('/register')
      cy.get('#inviteToken').type('sometoken')
      cy.get('#username').type('existinguser')
      cy.get('#password').type('password123')
      cy.get('button[type="submit"]').click()

      cy.get('[role="alert"]').should('contain', 'já está em uso')
    })

    it('redireciona para / após registro bem-sucedido', () => {
      cy.intercept('GET', '/api/teams', { statusCode: 200, body: [] })
      cy.intercept('POST', '/api/auth/register', {
        statusCode: 200,
        body: { token: 'fake.jwt.token', expiresAt: new Date(Date.now() + 3600000).toISOString() },
      })

      cy.visit('/register')
      cy.get('#inviteToken').type('validtoken')
      cy.get('#username').type('newuser')
      cy.get('#password').type('password123')
      cy.get('button[type="submit"]').click()

      cy.url().should('eq', Cypress.config('baseUrl') + '/')
    })
  })

  // ── Proteção de rotas ─────────────────────────────────────────────────────

  describe('Proteção de rotas', () => {
    it('redireciona para /login quando não autenticado', () => {
      cy.visit('/')
      cy.url().should('include', '/login')
    })

    it('redireciona para /login ao acessar /bets sem token', () => {
      cy.visit('/bets')
      cy.url().should('include', '/login')
    })

    it('permite acesso a rotas protegidas com token válido', () => {
      cy.window().then((win) => {
        win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
      })
      cy.intercept('GET', '/api/users/me', {
        statusCode: 200,
        body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
      })
      cy.intercept('GET', '/api/users/me/balance', {
        statusCode: 200,
        body: { virtualBalance: 1000, reservedBalance: 0 },
      })

      cy.visit('/')
      cy.url().should('eq', Cypress.config('baseUrl') + '/')
    })
  })

  // ── Logout ────────────────────────────────────────────────────────────────

  describe('Logout', () => {
    it('limpa o token e redireciona para /login', () => {
      cy.window().then((win) => {
        win.sessionStorage.setItem('frogbets_token', 'fake.jwt.token')
      })
      cy.intercept('GET', '/api/users/me', {
        statusCode: 200,
        body: { id: '1', username: 'testuser', isAdmin: false, isTeamLeader: false, createdAt: new Date().toISOString() },
      })
      cy.intercept('GET', '/api/users/me/balance', {
        statusCode: 200,
        body: { virtualBalance: 1000, reservedBalance: 0 },
      })
      cy.intercept('POST', '/api/auth/logout', { statusCode: 204 }).as('logout')

      cy.visit('/')
      cy.get('button').contains('Sair').click()

      cy.wait('@logout')
      cy.url().should('include', '/login')
      cy.window().then((win) => {
        expect(win.sessionStorage.getItem('frogbets_token')).to.be.null
      })
    })
  })
})

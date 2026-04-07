/// <reference types="cypress" />

declare global {
  namespace Cypress {
    interface Chainable {
      login(username: string, password: string): Chainable<void>
      loginAsAdmin(): Chainable<void>
      setAuthToken(token: string): Chainable<void>
    }
  }
}

// Login via UI
Cypress.Commands.add('login', (username: string, password: string) => {
  cy.visit('/login')
  cy.get('#username').type(username)
  cy.get('#password').type(password)
  cy.get('button[type="submit"]').click()
  cy.url().should('eq', Cypress.config('baseUrl') + '/')
})

// Login via API (faster — bypasses UI)
Cypress.Commands.add('loginAsAdmin', () => {
  cy.request('POST', `${Cypress.env('apiUrl')}/api/auth/login`, {
    username: Cypress.env('adminUsername') ?? 'admin',
    password: Cypress.env('adminPassword') ?? 'admin123456',
  }).then((res) => {
    sessionStorage.setItem('frogbets_token', res.body.token)
    cy.setAuthToken(res.body.token)
  })
})

Cypress.Commands.add('setAuthToken', (token: string) => {
  cy.window().then((win) => {
    win.sessionStorage.setItem('frogbets_token', token)
  })
})

export {}

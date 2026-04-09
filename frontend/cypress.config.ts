import { defineConfig } from 'cypress'

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:5173',
    specPattern: 'cypress/e2e/**/*.cy.ts',
    supportFile: 'cypress/support/e2e.ts',
    video: false,
    screenshotOnRunFailure: true,
    env: {
      apiUrl: 'http://localhost:8080',
      adminUsername: 'admin',
      adminPassword: 'admin123456',
    },
  },
})

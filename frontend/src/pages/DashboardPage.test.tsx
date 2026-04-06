import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import DashboardPage from './DashboardPage'

const server = setupServer(
  http.get('/api/users/me/balance', () => {
    return HttpResponse.json({ virtualBalance: 850, reservedBalance: 150 })
  })
)

beforeAll(() => server.listen())
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

describe('DashboardPage', () => {
  it('exibe saldo disponível e reservado após carregar', async () => {
    render(<DashboardPage />)

    await waitFor(() => {
      expect(screen.getByText(/850/)).toBeInTheDocument()
      expect(screen.getByText(/150/)).toBeInTheDocument()
    })

    expect(screen.getByText(/Saldo disponível/i)).toBeInTheDocument()
    expect(screen.getByText(/Saldo reservado/i)).toBeInTheDocument()
  })

  it('exibe mensagem de erro quando a API falha', async () => {
    server.use(
      http.get('/api/users/me/balance', () => HttpResponse.error())
    )

    render(<DashboardPage />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })
  })
})

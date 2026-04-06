import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import LeaderboardPage from './LeaderboardPage'

const mockEntries = [
  { username: 'alice', virtualBalance: 1500, winsCount: 5, lossesCount: 2 },
  { username: 'bob', virtualBalance: 1200, winsCount: 3, lossesCount: 3 },
  { username: 'carol', virtualBalance: 800, winsCount: 1, lossesCount: 4 },
]

const server = setupServer(
  http.get('/api/leaderboard', () => {
    return HttpResponse.json(mockEntries)
  })
)

beforeAll(() => server.listen())
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

describe('LeaderboardPage', () => {
  it('exibe tabela com colunas corretas', async () => {
    render(<LeaderboardPage />)

    await waitFor(() => {
      expect(screen.getByText('Posição')).toBeInTheDocument()
      expect(screen.getByText('Usuário')).toBeInTheDocument()
      expect(screen.getByText('Saldo Virtual')).toBeInTheDocument()
      expect(screen.getByText('Vitórias')).toBeInTheDocument()
      expect(screen.getByText('Derrotas')).toBeInTheDocument()
    })
  })

  it('exibe entradas do leaderboard com posição, username, saldo, vitórias e derrotas', async () => {
    render(<LeaderboardPage />)

    await waitFor(() => {
      expect(screen.getByText('alice')).toBeInTheDocument()
      expect(screen.getByText('1500')).toBeInTheDocument()
      expect(screen.getByText('bob')).toBeInTheDocument()
      expect(screen.getByText('1200')).toBeInTheDocument()
    })

    // Verifica posições
    const rows = screen.getAllByRole('row')
    // rows[0] = header, rows[1] = alice (pos 1), rows[2] = bob (pos 2)
    expect(rows[1]).toHaveTextContent('1')
    expect(rows[2]).toHaveTextContent('2')
    expect(rows[3]).toHaveTextContent('3')
  })

  it('exibe mensagem de erro quando a API falha', async () => {
    server.use(
      http.get('/api/leaderboard', () => HttpResponse.error())
    )

    render(<LeaderboardPage />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })
  })

  it('exibe mensagem quando não há usuários', async () => {
    server.use(
      http.get('/api/leaderboard', () => HttpResponse.json([]))
    )

    render(<LeaderboardPage />)

    await waitFor(() => {
      expect(screen.getByText(/Nenhum usuário encontrado/i)).toBeInTheDocument()
    })
  })
})

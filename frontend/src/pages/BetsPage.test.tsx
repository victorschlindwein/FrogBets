import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import BetsPage from './BetsPage'

const pendingBet = {
  id: 'bet-1',
  marketId: 'mkt-1',
  creatorOption: 'TeamA',
  covererOption: null,
  amount: 100,
  status: 'Pending',
  coveredById: null,
  market: { type: 'MapWinner', mapNumber: 1, gameId: 'game-1' },
}

const activeBet = {
  id: 'bet-2',
  marketId: 'mkt-2',
  creatorOption: 'TeamB',
  covererOption: 'TeamA',
  amount: 200,
  status: 'Active',
  coveredById: 'user-99',
  market: { type: 'SeriesWinner', mapNumber: null, gameId: 'game-1' },
}

const settledBet = {
  id: 'bet-3',
  marketId: 'mkt-3',
  creatorOption: 'PlayerX',
  covererOption: 'PlayerY',
  amount: 50,
  status: 'Settled',
  coveredById: 'user-88',
  market: { type: 'TopKills', mapNumber: 2, gameId: 'game-2' },
}

const server = setupServer(
  http.get('/api/bets', () => {
    return HttpResponse.json([pendingBet, activeBet, settledBet])
  })
)

beforeAll(() => server.listen())
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

describe('BetsPage', () => {
  it('exibe seções Pendentes, Ativas e Liquidadas', async () => {
    render(<BetsPage />)

    await waitFor(() => {
      expect(screen.getByText('Pendentes')).toBeInTheDocument()
      expect(screen.getByText('Ativas')).toBeInTheDocument()
      expect(screen.getByText(/Liquidadas/)).toBeInTheDocument()
    })
  })

  it('exibe mercado, opção, valor e status de cada aposta', async () => {
    render(<BetsPage />)

    await waitFor(() => {
      // pending bet - check market label and key fields are present
      expect(screen.getAllByText(/Vencedor do Mapa/).length).toBeGreaterThan(0)
      expect(screen.getByText(/TeamA/)).toBeInTheDocument()
      expect(screen.getByText(/100/)).toBeInTheDocument()

      // active bet
      expect(screen.getAllByText(/Vencedor da Série/).length).toBeGreaterThan(0)
      expect(screen.getByText(/TeamB/)).toBeInTheDocument()
      expect(screen.getByText(/200/)).toBeInTheDocument()

      // settled bet
      expect(screen.getAllByText(/Top Kills/).length).toBeGreaterThan(0)

      // status labels appear somewhere in the document
      expect(document.body.textContent).toContain('Pendente')
      expect(document.body.textContent).toContain('Ativa')
      expect(document.body.textContent).toContain('Liquidada')
    })
  })

  it('exibe contraparte para apostas com coveredById', async () => {
    render(<BetsPage />)

    await waitFor(() => {
      expect(screen.getByText(/user-99/)).toBeInTheDocument()
      expect(screen.getByText(/user-88/)).toBeInTheDocument()
    })
  })

  it('exibe botão cancelar apenas para apostas Pendentes', async () => {
    render(<BetsPage />)

    await waitFor(() => {
      const cancelButtons = screen.getAllByRole('button', { name: /Cancelar/i })
      expect(cancelButtons).toHaveLength(1)
    })
  })

  it('remove aposta da lista após cancelamento', async () => {
    server.use(
      http.delete('/api/bets/bet-1', () => HttpResponse.json({}))
    )

    render(<BetsPage />)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Cancelar/i })).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /Cancelar/i }))

    await waitFor(() => {
      // After cancel, the status "Pendente" should be gone (only heading "Pendentes" remains)
      expect(screen.queryByText('Nenhuma aposta pendente.')).toBeInTheDocument()
    })
  })

  it('exibe mensagem de erro quando a API falha', async () => {
    server.use(
      http.get('/api/bets', () => HttpResponse.error())
    )

    render(<BetsPage />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })
  })

  it('exibe mensagem quando não há apostas em uma seção', async () => {
    server.use(
      http.get('/api/bets', () => HttpResponse.json([activeBet]))
    )

    render(<BetsPage />)

    await waitFor(() => {
      expect(screen.getByText('Nenhuma aposta pendente.')).toBeInTheDocument()
    })
  })
})

import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { MemoryRouter } from 'react-router-dom'
import MarketplacePage from './MarketplacePage'

/**
 * Preservation Tests — Bug 1: Comportamentos baseline do Marketplace
 *
 * Validates: Requirements 3.1, 3.2
 *
 * Estes testes confirmam comportamentos que devem ser PRESERVADOS após o fix.
 * Usam o formato ANINHADO (market: { type, mapNumber, gameId }) que funciona
 * no código atual — não dependem do fix do Bug 1.
 *
 * EXPECTED OUTCOME: Ambos os testes PASSAM no código atual (não corrigido).
 */

const server = setupServer(
  http.get('/api/users/me', () =>
    HttpResponse.json({
      id: 'user-1',
      username: 'testuser',
      isAdmin: false,
      createdAt: '2024-01-01',
      isTeamLeader: false,
    })
  ),
  http.get('/api/trades/listings', () => HttpResponse.json([])),
)

beforeAll(() => server.listen({ onUnhandledRequest: 'bypass' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

function renderMarketplace() {
  return render(
    <MemoryRouter>
      <MarketplacePage />
    </MemoryRouter>
  )
}

describe('Preservation: MarketplacePage — comportamentos baseline', () => {

  /**
   * Preservation 1: marketplace vazio exibe mensagem correta.
   * Validates: Requirement 3.1
   */
  it('exibe "Nenhuma aposta disponível para cobertura" quando API retorna []', async () => {
    server.use(
      http.get('/api/marketplace', () => HttpResponse.json([]))
    )

    renderMarketplace()

    await waitFor(() => {
      expect(
        screen.getByText('Nenhuma aposta disponível para cobertura.')
      ).toBeInTheDocument()
    }, { timeout: 3000 })
  })

  /**
   * Preservation 2: cobrir aposta remove-a da lista.
   * Usa formato aninhado compatível com o código atual.
   * Validates: Requirement 3.2
   */
  it('remove a aposta da lista após cobertura bem-sucedida', async () => {
    const betId = 'bet-preserve-1'

    server.use(
      http.get('/api/marketplace', () =>
        HttpResponse.json([
          {
            id: betId,
            marketId: 'mkt-1',
            creatorOption: 'TeamA',
            amount: 150,
            creatorId: 'other-user',
            market: {
              type: 'MapWinner',
              mapNumber: 1,
              gameId: 'game-1',
            },
          },
        ])
      ),
      http.post(`/api/bets/${betId}/cover`, () => HttpResponse.json({ success: true }))
    )

    renderMarketplace()

    await waitFor(() => {
      expect(screen.getByText(/TeamA/)).toBeInTheDocument()
    }, { timeout: 3000 })

    const coverBtn = screen.getByRole('button', { name: /Cobrir/i })
    await userEvent.click(coverBtn)

    const confirmBtn = await screen.findByRole('button', { name: /Confirmar/i })
    await userEvent.click(confirmBtn)

    await waitFor(() => {
      expect(screen.queryByText(/TeamA/)).not.toBeInTheDocument()
    }, { timeout: 3000 })

    await waitFor(() => {
      expect(
        screen.getByText('Nenhuma aposta disponível para cobertura.')
      ).toBeInTheDocument()
    }, { timeout: 3000 })
  })
})

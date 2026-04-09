import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { MemoryRouter } from 'react-router-dom'
import * as fc from 'fast-check'
import MarketplacePage from './MarketplacePage'

/**
 * Bug Condition Exploration Tests — Bug 1: Marketplace tela branca
 *
 * Validates: Requirements 1.1, 1.2
 *
 * Estes testes confirmam o bug: quando a API retorna BetDto com campos planos
 * (marketType, mapNumber, gameId), o componente tenta acessar bet.market.type
 * onde bet.market é undefined, lançando TypeError e quebrando o render.
 *
 * EXPECTED OUTCOME: Testes FALHAM no código atual com
 * TypeError: Cannot read properties of undefined (reading 'type')
 */

// ── Tipos ─────────────────────────────────────────────────────────────────────

interface BetDtoFlat {
  id: string
  marketId: string
  marketType: string
  mapNumber: number | null
  gameId: string
  creatorOption: string
  amount: number
  creatorId: string
}

// ── MSW Server ────────────────────────────────────────────────────────────────

const server = setupServer(
  // Mock /api/users/me para evitar erros no TradeSection
  http.get('/api/users/me', () =>
    HttpResponse.json({ id: 'user-1', username: 'testuser', isAdmin: false, createdAt: '2024-01-01', isTeamLeader: false })
  ),
  // Mock /api/trades/listings para evitar erros no TradeSection
  http.get('/api/trades/listings', () => HttpResponse.json([])),
)

beforeAll(() => server.listen({ onUnhandledRequest: 'bypass' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeFlatBetDto(overrides: Partial<BetDtoFlat> = {}): BetDtoFlat {
  return {
    id: 'bet-1',
    marketId: 'mkt-1',
    marketType: 'MapWinner',
    mapNumber: 1,
    gameId: 'game-1',
    creatorOption: 'TeamA',
    amount: 100,
    creatorId: 'user-other',
    ...overrides,
  }
}

function renderMarketplace() {
  return render(
    <MemoryRouter>
      <MarketplacePage />
    </MemoryRouter>
  )
}

// ── Bug Condition Tests ───────────────────────────────────────────────────────

describe('Bug Condition: MarketplacePage com BetDto de campos planos', () => {

  it('deve renderizar sem lançar TypeError quando API retorna BetDto com campos planos (MapWinner)', async () => {
    // Bug: bet.market é undefined → marketLabel(undefined) lança TypeError
    server.use(
      http.get('/api/marketplace', () =>
        HttpResponse.json([makeFlatBetDto({ marketType: 'MapWinner', mapNumber: 1 })])
      )
    )

    renderMarketplace()

    // Deve renderizar o mercado sem exceção
    await waitFor(() => {
      expect(screen.getByText(/Vencedor do Mapa/)).toBeInTheDocument()
    }, { timeout: 3000 })
  })

  it('deve renderizar sem lançar TypeError quando API retorna BetDto com campos planos (SeriesWinner)', async () => {
    server.use(
      http.get('/api/marketplace', () =>
        HttpResponse.json([makeFlatBetDto({ marketType: 'SeriesWinner', mapNumber: null })])
      )
    )

    renderMarketplace()

    await waitFor(() => {
      expect(screen.getByText(/Vencedor da Série/)).toBeInTheDocument()
    }, { timeout: 3000 })
  })

  it('deve exibir a opção e o valor da aposta quando API retorna campos planos', async () => {
    server.use(
      http.get('/api/marketplace', () =>
        HttpResponse.json([makeFlatBetDto({ creatorOption: 'TeamA', amount: 250 })])
      )
    )

    renderMarketplace()

    await waitFor(() => {
      expect(screen.getByText(/TeamA/)).toBeInTheDocument()
      expect(screen.getByText(/250/)).toBeInTheDocument()
    }, { timeout: 3000 })
  })

  // ── Property-based test ───────────────────────────────────────────────────

  it('Property 1 (Bug Condition): para qualquer BetDto[] com campos planos, o componente renderiza sem TypeError', async () => {
    /**
     * Validates: Requirements 1.1, 1.2
     *
     * Para qualquer array não-vazio de BetDto com campos planos (sem market aninhado),
     * o componente MarketplacePage deve renderizar sem lançar exceções.
     *
     * EXPECTED OUTCOME no código atual: FALHA com
     * TypeError: Cannot read properties of undefined (reading 'type')
     */
    const arbMarketType = fc.constantFrom('MapWinner', 'SeriesWinner', 'TopKills', 'MostDeaths', 'MostUtilityDamage')
    const arbMapNumber = fc.option(fc.integer({ min: 1, max: 5 }), { nil: null })
    const arbBetDto = fc.record({
      id: fc.uuid(),
      marketId: fc.uuid(),
      marketType: arbMarketType,
      mapNumber: arbMapNumber,
      gameId: fc.uuid(),
      creatorOption: fc.constantFrom('TeamA', 'TeamB'),
      amount: fc.integer({ min: 1, max: 1000 }),
      creatorId: fc.uuid(),
    })

    await fc.assert(
      fc.asyncProperty(fc.array(arbBetDto, { minLength: 1, maxLength: 3 }), async (bets) => {
        server.use(
          http.get('/api/marketplace', () => HttpResponse.json(bets))
        )

        let threw = false
        try {
          renderMarketplace()
          // Aguarda o componente tentar renderizar as apostas
          await waitFor(() => {
            // Se chegou aqui sem exceção, o componente renderizou
            const list = document.querySelector('.bet-list')
            expect(list).not.toBeNull()
          }, { timeout: 2000 })
        } catch (e) {
          threw = true
        }

        // Cleanup para o próximo run
        server.resetHandlers()
        server.use(
          http.get('/api/users/me', () =>
            HttpResponse.json({ id: 'user-1', username: 'testuser', isAdmin: false, createdAt: '2024-01-01', isTeamLeader: false })
          ),
          http.get('/api/trades/listings', () => HttpResponse.json([])),
        )

        // O componente NÃO deve lançar exceção
        return !threw
      }),
      { numRuns: 5 }
    )
  }, 30000)
})

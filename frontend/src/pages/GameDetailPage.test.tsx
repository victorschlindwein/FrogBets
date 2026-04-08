import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest'
import { render, screen, waitFor, cleanup } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import GameDetailPage from './GameDetailPage'

/**
 * Bug Condition Exploration Tests
 *
 * Validates: Requirements 2.1, 2.2
 *
 * Estes testes confirmam o bug: mercados de jogador (TopKills, MostDeaths,
 * MostUtilityDamage) renderizam <input type="text"> em vez de <select>.
 *
 * EXPECTED OUTCOME: Todos os testes FALHAM no código atual (isso confirma o bug).
 */

const makeGame = (marketType: string) => ({
  id: 'game-1',
  teamA: 'FURIA',
  teamB: 'NAVI',
  scheduledAt: new Date(Date.now() + 86400000).toISOString(), // amanhã
  numberOfMaps: 3,
  status: 'Scheduled',
  markets: [
    {
      id: 'mkt-1',
      type: marketType,
      mapNumber: 1,
      status: 'Open',
    },
  ],
})

const server = setupServer(
  http.get('/api/games/game-1', ({ request }) => {
    const url = new URL(request.url)
    const marketType = url.searchParams.get('_marketType') ?? 'TopKills'
    return HttpResponse.json(makeGame(marketType))
  })
)

beforeAll(() => server.listen())
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

function renderGameDetail(marketType: string) {
  server.use(
    http.get('/api/games/game-1', () => HttpResponse.json(makeGame(marketType)))
  )

  return render(
    <MemoryRouter initialEntries={['/games/game-1']}>
      <Routes>
        <Route path="/games/:id" element={<GameDetailPage />} />
      </Routes>
    </MemoryRouter>
  )
}

describe('Bug Condition: mercados de jogador devem renderizar <select>', () => {
  it('TopKills: deve renderizar <select> em vez de <input type="text">', async () => {
    renderGameDetail('TopKills')

    await waitFor(() => {
      expect(screen.getByText('Top Kills — Mapa 1')).toBeInTheDocument()
    })

    // Bug: atualmente renderiza <input type="text"> — deve ser <select>
    const selectEl = screen.queryByRole('combobox')
    expect(selectEl).not.toBeNull()

    const textInput = screen.queryByPlaceholderText('Nome do jogador')
    expect(textInput).toBeNull()
  })

  it('MostDeaths: deve renderizar <select> em vez de <input type="text">', async () => {
    renderGameDetail('MostDeaths')

    await waitFor(() => {
      expect(screen.getByText('Mais Mortes — Mapa 1')).toBeInTheDocument()
    })

    // Bug: atualmente renderiza <input type="text"> — deve ser <select>
    const selectEl = screen.queryByRole('combobox')
    expect(selectEl).not.toBeNull()

    const textInput = screen.queryByPlaceholderText('Nome do jogador')
    expect(textInput).toBeNull()
  })

  it('MostUtilityDamage: deve renderizar <select> em vez de <input type="text">', async () => {
    renderGameDetail('MostUtilityDamage')

    await waitFor(() => {
      expect(screen.getByText('Maior Dano por Utilitários — Mapa 1')).toBeInTheDocument()
    })

    // Bug: atualmente renderiza <input type="text"> — deve ser <select>
    const selectEl = screen.queryByRole('combobox')
    expect(selectEl).not.toBeNull()

    const textInput = screen.queryByPlaceholderText('Nome do jogador')
    expect(textInput).toBeNull()
  })
})

/**
 * Preservation Property Tests
 *
 * Validates: Requirements 3.1, 3.3
 *
 * Estes testes confirmam o baseline: mercados de time (MapWinner, SeriesWinner)
 * continuam renderizando <select> com game.teamA e game.teamB corretamente.
 *
 * EXPECTED OUTCOME: Todos os testes PASSAM no código atual (confirma baseline a preservar).
 */

// Combinações arbitrárias de times para property-based coverage
const TEAM_COMBINATIONS = [
  { teamA: 'FURIA', teamB: 'NAVI' },
  { teamA: 'Team Liquid', teamB: 'Astralis' },
  { teamA: 'G2 Esports', teamB: 'Vitality' },
]

function makeTeamGame(marketType: string, teamA: string, teamB: string) {
  return {
    id: 'game-1',
    teamA,
    teamB,
    scheduledAt: new Date(Date.now() + 86400000).toISOString(),
    numberOfMaps: 3,
    status: 'Scheduled',
    markets: [
      {
        id: 'mkt-team-1',
        type: marketType,
        mapNumber: marketType === 'MapWinner' ? 1 : null,
        status: 'Open',
      },
    ],
  }
}

describe('Preservation: mercados de time continuam renderizando dropdown com teamA e teamB', () => {
  for (const { teamA, teamB } of TEAM_COMBINATIONS) {
    it(`MapWinner: renderiza <select> com "${teamA}" e "${teamB}"`, async () => {
      server.use(
        http.get('/api/games/game-1', () =>
          HttpResponse.json(makeTeamGame('MapWinner', teamA, teamB))
        )
      )

      render(
        <MemoryRouter initialEntries={['/games/game-1']}>
          <Routes>
            <Route path="/games/:id" element={<GameDetailPage />} />
          </Routes>
        </MemoryRouter>
      )

      await waitFor(() => {
        expect(screen.getByText(/Vencedor do Mapa/)).toBeInTheDocument()
      })

      const select = screen.getByRole('combobox')
      expect(select).not.toBeNull()

      const options = Array.from(select.querySelectorAll('option')).map(o => o.textContent)
      expect(options).toContain(teamA)
      expect(options).toContain(teamB)

      cleanup()
    })

    it(`SeriesWinner: renderiza <select> com "${teamA}" e "${teamB}"`, async () => {
      server.use(
        http.get('/api/games/game-1', () =>
          HttpResponse.json(makeTeamGame('SeriesWinner', teamA, teamB))
        )
      )

      render(
        <MemoryRouter initialEntries={['/games/game-1']}>
          <Routes>
            <Route path="/games/:id" element={<GameDetailPage />} />
          </Routes>
        </MemoryRouter>
      )

      await waitFor(() => {
        expect(screen.getByText('Vencedor da Série')).toBeInTheDocument()
      })

      const select = screen.getByRole('combobox')
      expect(select).not.toBeNull()

      const options = Array.from(select.querySelectorAll('option')).map(o => o.textContent)
      expect(options).toContain(teamA)
      expect(options).toContain(teamB)

      cleanup()
    })
  }
})

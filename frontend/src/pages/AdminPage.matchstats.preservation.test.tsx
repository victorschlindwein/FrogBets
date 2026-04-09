import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

/**
 * Preservation Checking Tests — MatchStatsSection: comportamento fora da bug condition
 *
 * Validates: Requirements 3.1, 3.2, 3.3, 3.4
 *
 * Estes testes verificam que o fix (Promise.all → Promise.allSettled) NÃO introduziu
 * regressões. Para todos os inputs onde a bug condition NÃO se aplica, o comportamento
 * deve ser idêntico ao original.
 *
 * Property 2: Preservation — Comportamento inalterado para inputs fora da bug condition
 */

// ── Mocks de API ──────────────────────────────────────────────────────────────

vi.mock('../api/players', () => ({
  getMapResultsByGame: vi.fn(),
  getGamePlayers: vi.fn(),
  getTeams: vi.fn().mockResolvedValue([]),
  createTeam: vi.fn(),
  getPlayers: vi.fn().mockResolvedValue([]),
  createMapResult: vi.fn(),
  registerMatchStats: vi.fn(),
  getPlayerStats: vi.fn().mockResolvedValue([]),
  getPlayersByTeam: vi.fn().mockResolvedValue([]),
  uploadTeamLogo: vi.fn(),
  removeTeamLogo: vi.fn(),
  getPlayersRanking: vi.fn().mockResolvedValue([]),
}))

const mockAdminUser = {
  id: 'admin1',
  username: 'admin',
  isAdmin: true,
  isMasterAdmin: false,
  teamId: null,
  teamName: null,
  isTeamLeader: false,
}

const mockGame = {
  id: 'game1',
  teamA: 'FURIA',
  teamB: 'NAVI',
  status: 'InProgress',
  numberOfMaps: 3,
  scheduledAt: '2024-01-01T00:00:00Z',
  markets: [],
}

const mockMapResult = {
  id: 'map1',
  mapNumber: 1,
  rounds: 30,
  gameId: 'game1',
  createdAt: '2024-01-01T00:00:00Z',
}

const mockPlayer = {
  id: 'p1',
  username: 'player1',
  teamName: 'FURIA',
}

// Mock do apiClient
vi.mock('../api/client', () => {
  const mockGet = vi.fn()
  const mockPost = vi.fn().mockResolvedValue({ data: {} })
  const mockPatch = vi.fn().mockResolvedValue({ data: {} })
  const mockPut = vi.fn().mockResolvedValue({ data: {} })
  const mockDelete = vi.fn().mockResolvedValue({ data: {} })

  return {
    default: {
      get: mockGet,
      post: mockPost,
      patch: mockPatch,
      put: mockPut,
      delete: mockDelete,
      interceptors: {
        request: { use: vi.fn() },
        response: { use: vi.fn() },
      },
    },
    getToken: vi.fn().mockReturnValue('fake-token'),
    setToken: vi.fn(),
    publicClient: {
      get: vi.fn().mockResolvedValue({ data: {} }),
      post: vi.fn().mockResolvedValue({ data: {} }),
    },
  }
})

// ── Helpers ───────────────────────────────────────────────────────────────────

async function setupApiClientMock() {
  const { default: apiClient } = await import('../api/client')
  vi.mocked(apiClient.get).mockImplementation((url: string) => {
    if (url === '/users/me') return Promise.resolve({ data: mockAdminUser })
    if (url === '/users') return Promise.resolve({ data: [mockAdminUser] })
    if (url === '/games') return Promise.resolve({ data: [mockGame] })
    if (url === '/invites') return Promise.resolve({ data: [] })
    return Promise.resolve({ data: [] })
  })
}

async function renderAdminPage() {
  const { default: AdminPage } = await import('./AdminPage')
  return render(<AdminPage />)
}

async function waitForAdminPageToLoad() {
  await waitFor(() => {
    expect(screen.queryByText('Carregando...')).not.toBeInTheDocument()
  }, { timeout: 5000 })
}

// ── Testes de Preservation ────────────────────────────────────────────────────

describe('Preservation: MatchStatsSection — comportamento fora da bug condition', () => {

  beforeEach(async () => {
    vi.clearAllMocks()
    await setupApiClientMock()
  })

  it('3.2 Both Success: ambas as chamadas com sucesso → dropdown de Mapa e Jogador populados', async () => {
    /**
     * Validates: Requirements 3.2
     *
     * Property 2 — Preservation: quando ambas as chamadas retornam com sucesso,
     * o comportamento deve ser idêntico ao original.
     *
     * EXPECTED: dropdown de Mapa exibe "Mapa 1 — 30 rounds"
     *           dropdown de Jogador exibe "player1 - FURIA" (após selecionar o mapa)
     */
    const { getMapResultsByGame, getGamePlayers } = await import('../api/players')
    vi.mocked(getMapResultsByGame).mockResolvedValue([mockMapResult])
    vi.mocked(getGamePlayers).mockResolvedValue([mockPlayer])

    await renderAdminPage()
    await waitForAdminPageToLoad()

    const statsSelect = document.getElementById('statsGameSelect') as HTMLSelectElement
    expect(statsSelect).not.toBeNull()

    await userEvent.selectOptions(statsSelect, 'game1')

    await waitFor(() => {
      expect(screen.queryByText(/Carregando mapas/i)).not.toBeInTheDocument()
    }, { timeout: 3000 })

    // Dropdown de Mapa deve estar populado
    await waitFor(() => {
      expect(screen.getByText(/Mapa 1 — 30 rounds/i)).toBeInTheDocument()
    }, { timeout: 3000 })

    // Selecionar o mapa para revelar o dropdown de Jogador
    const mapSelect = document.getElementById('statsMapResultSelect') as HTMLSelectElement
    expect(mapSelect).not.toBeNull()
    await userEvent.selectOptions(mapSelect, 'map1')

    // Dropdown de Jogador deve estar populado
    await waitFor(() => {
      expect(screen.getByText(/player1 - FURIA/i)).toBeInTheDocument()
    }, { timeout: 3000 })
  })

  it('3.3 Empty Maps: getMapResultsByGame retorna [] → mensagem "Nenhum mapa registrado" aparece', async () => {
    /**
     * Validates: Requirements 3.1
     *
     * Property 2 — Preservation: quando não há mapas registrados,
     * a mensagem "Nenhum mapa registrado para este jogo." deve continuar aparecendo.
     */
    const { getMapResultsByGame, getGamePlayers } = await import('../api/players')
    vi.mocked(getMapResultsByGame).mockResolvedValue([])
    vi.mocked(getGamePlayers).mockResolvedValue([])

    await renderAdminPage()
    await waitForAdminPageToLoad()

    const statsSelect = document.getElementById('statsGameSelect') as HTMLSelectElement
    expect(statsSelect).not.toBeNull()

    await userEvent.selectOptions(statsSelect, 'game1')

    await waitFor(() => {
      expect(screen.queryByText(/Carregando mapas/i)).not.toBeInTheDocument()
    }, { timeout: 3000 })

    // Mensagem de "nenhum mapa" deve aparecer
    await waitFor(() => {
      expect(screen.getByText(/Nenhum mapa registrado para este jogo\./i)).toBeInTheDocument()
    }, { timeout: 3000 })
  })

  it('3.4 No Game Selected: statsGameId vazio → seção de Mapa não aparece', async () => {
    /**
     * Validates: Requirements 3.3
     *
     * Property 2 — Preservation: quando nenhum jogo está selecionado,
     * a seção de Mapa não deve ser renderizada (o componente só exibe o select
     * de Mapa quando statsGameId está preenchido).
     */
    const { getMapResultsByGame, getGamePlayers } = await import('../api/players')
    vi.mocked(getMapResultsByGame).mockResolvedValue([mockMapResult])
    vi.mocked(getGamePlayers).mockResolvedValue([mockPlayer])

    await renderAdminPage()
    await waitForAdminPageToLoad()

    // Sem selecionar jogo, o select de Mapa não deve existir
    expect(document.getElementById('statsMapResultSelect')).toBeNull()
    // E a mensagem de "nenhum mapa" também não deve aparecer
    expect(screen.queryByText(/Nenhum mapa registrado para este jogo\./i)).not.toBeInTheDocument()
  })

  it('3.5 Both Fail: ambas as chamadas falham → mapResults=[] e players=[] (mensagem "Nenhum mapa" aparece)', async () => {
    /**
     * Validates: Requirements 3.1, 3.2
     *
     * Property 2 — Preservation: quando ambas as chamadas falham,
     * o comportamento deve ser idêntico ao original — mapResults=[] e players=[],
     * exibindo "Nenhum mapa registrado para este jogo."
     */
    const { getMapResultsByGame, getGamePlayers } = await import('../api/players')
    vi.mocked(getMapResultsByGame).mockRejectedValue(new Error('Network error'))
    vi.mocked(getGamePlayers).mockRejectedValue(new Error('Network error'))

    await renderAdminPage()
    await waitForAdminPageToLoad()

    const statsSelect = document.getElementById('statsGameSelect') as HTMLSelectElement
    expect(statsSelect).not.toBeNull()

    await userEvent.selectOptions(statsSelect, 'game1')

    await waitFor(() => {
      expect(screen.queryByText(/Carregando mapas/i)).not.toBeInTheDocument()
    }, { timeout: 3000 })

    // Com ambas as chamadas falhando, mapResults=[] → mensagem de "nenhum mapa"
    await waitFor(() => {
      expect(screen.getByText(/Nenhum mapa registrado para este jogo\./i)).toBeInTheDocument()
    }, { timeout: 3000 })

    // O select de Mapa não deve aparecer (mapResults está vazio)
    expect(document.getElementById('statsMapResultSelect')).toBeNull()
  })

})

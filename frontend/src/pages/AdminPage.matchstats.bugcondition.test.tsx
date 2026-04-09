import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

/**
 * Bug Condition Exploration Tests — MatchStatsSection: dropdown de Mapa zerado
 *
 * Validates: Requirements 1.1, 1.2
 *
 * Estes testes CONFIRMAM O BUG no código atual (não corrigido).
 * Quando `getGamePlayers` falha, o `.catch` compartilhado do `Promise.all`
 * zera `mapResults`, exibindo "Nenhum mapa registrado para este jogo."
 * mesmo com mapas existentes.
 *
 * EXPECTED OUTCOME: Testes FALHAM no código atual porque o componente exibe
 * "Nenhum mapa registrado para este jogo." em vez do dropdown com "Mapa 1 — 30 rounds".
 * Após o fix (Task 2), estes testes devem PASSAR.
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

// Mock do apiClient — retorna dados mínimos para o AdminPage carregar
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
  // Aguarda o AdminPage sair do estado "Carregando..."
  await waitFor(() => {
    expect(screen.queryByText('Carregando...')).not.toBeInTheDocument()
  }, { timeout: 5000 })
}

// ── Testes de Bug Condition ───────────────────────────────────────────────────

describe('Bug Condition: MatchStatsSection — getGamePlayers falha zera mapResults', () => {

  beforeEach(async () => {
    vi.clearAllMocks()
    await setupApiClientMock()
  })

  it('1.1 Maps OK + Players Fail: deve exibir dropdown de Mapa mesmo quando getGamePlayers rejeita', async () => {
    /**
     * Validates: Requirements 1.1, 1.2
     *
     * BUG CONDITION: getMapResultsByGame retorna dados com sucesso,
     * mas getGamePlayers rejeita com erro de rede.
     *
     * COMPORTAMENTO BUGADO (código atual):
     *   - Promise.all rejeita ao primeiro erro
     *   - .catch(() => { setMapResults([]); setPlayers([]) }) executa
     *   - mapResults é zerado → exibe "Nenhum mapa registrado para este jogo."
     *
     * COMPORTAMENTO ESPERADO (após fix):
     *   - mapResults preserva os dados de getMapResultsByGame
     *   - Dropdown exibe "Mapa 1 — 30 rounds"
     *
     * ESTE TESTE FALHA NO CÓDIGO ATUAL (demonstra o bug).
     */
    const { getMapResultsByGame, getGamePlayers } = await import('../api/players')
    vi.mocked(getMapResultsByGame).mockResolvedValue([mockMapResult])
    vi.mocked(getGamePlayers).mockRejectedValue(new Error('Network error'))

    await renderAdminPage()
    await waitForAdminPageToLoad()

    // Selecionar o jogo na seção de Estatísticas de Partida
    // O label "Jogo:" aparece em múltiplos selects — usar o da seção de stats (statsGameSelect)
    const statsSelect = document.getElementById('statsGameSelect') as HTMLSelectElement
    expect(statsSelect).not.toBeNull()

    await userEvent.selectOptions(statsSelect, 'game1')

    // Aguardar o carregamento terminar
    await waitFor(() => {
      expect(screen.queryByText(/Carregando mapas/i)).not.toBeInTheDocument()
    }, { timeout: 3000 })

    // ASSERTION: O dropdown de Mapa deve aparecer com "Mapa 1 — 30 rounds"
    // NO CÓDIGO BUGADO: este expect FALHA porque mapResults é zerado pelo catch compartilhado
    // APÓS O FIX: este expect PASSA porque mapResults é preservado
    await waitFor(() => {
      expect(screen.getByText(/Mapa 1 — 30 rounds/i)).toBeInTheDocument()
    }, { timeout: 3000 })
  })

  it('1.2 Maps OK + Players 404: deve exibir dropdown de Mapa mesmo quando getGamePlayers rejeita com 404', async () => {
    /**
     * Validates: Requirements 1.1, 1.2
     *
     * Variação: getGamePlayers rejeita com erro HTTP 404.
     * O comportamento bugado é o mesmo — mapResults é zerado.
     *
     * ESTE TESTE FALHA NO CÓDIGO ATUAL (demonstra o bug).
     */
    const { getMapResultsByGame, getGamePlayers } = await import('../api/players')
    vi.mocked(getMapResultsByGame).mockResolvedValue([mockMapResult])
    const error404 = Object.assign(new Error('Not Found'), { response: { status: 404 } })
    vi.mocked(getGamePlayers).mockRejectedValue(error404)

    await renderAdminPage()
    await waitForAdminPageToLoad()

    const statsSelect = document.getElementById('statsGameSelect') as HTMLSelectElement
    expect(statsSelect).not.toBeNull()

    await userEvent.selectOptions(statsSelect, 'game1')

    await waitFor(() => {
      expect(screen.queryByText(/Carregando mapas/i)).not.toBeInTheDocument()
    }, { timeout: 3000 })

    // ESTE TESTE FALHA NO CÓDIGO ATUAL
    await waitFor(() => {
      expect(screen.getByText(/Mapa 1 — 30 rounds/i)).toBeInTheDocument()
    }, { timeout: 3000 })
  })

  it('1.3 Maps OK + Players 500: deve exibir dropdown de Mapa mesmo quando getGamePlayers rejeita com 500', async () => {
    /**
     * Validates: Requirements 1.1, 1.2
     *
     * Variação: getGamePlayers rejeita com erro HTTP 500.
     *
     * ESTE TESTE FALHA NO CÓDIGO ATUAL (demonstra o bug).
     */
    const { getMapResultsByGame, getGamePlayers } = await import('../api/players')
    vi.mocked(getMapResultsByGame).mockResolvedValue([mockMapResult])
    const error500 = Object.assign(new Error('Internal Server Error'), { response: { status: 500 } })
    vi.mocked(getGamePlayers).mockRejectedValue(error500)

    await renderAdminPage()
    await waitForAdminPageToLoad()

    const statsSelect = document.getElementById('statsGameSelect') as HTMLSelectElement
    expect(statsSelect).not.toBeNull()

    await userEvent.selectOptions(statsSelect, 'game1')

    await waitFor(() => {
      expect(screen.queryByText(/Carregando mapas/i)).not.toBeInTheDocument()
    }, { timeout: 3000 })

    // ESTE TESTE FALHA NO CÓDIGO ATUAL
    await waitFor(() => {
      expect(screen.getByText(/Mapa 1 — 30 rounds/i)).toBeInTheDocument()
    }, { timeout: 3000 })
  })

})

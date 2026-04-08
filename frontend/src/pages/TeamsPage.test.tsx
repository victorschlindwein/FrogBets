import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { MemoryRouter } from 'react-router-dom'
import TeamsPage from './TeamsPage'

// ── Fixtures ──────────────────────────────────────────────────────────────────
const team1 = { id: 'team-1', name: 'Frog Alpha', logoUrl: 'https://example.com/logo.png', createdAt: '2024-01-01' }
const team2 = { id: 'team-2', name: 'Frog Beta', logoUrl: null, createdAt: '2024-01-01' }
const player1 = { id: 'p-1', nickname: 'xXx_Frog', realName: 'João', teamId: 'team-1', teamName: 'Frog Alpha', photoUrl: null, playerScore: 1.2, matchesCount: 5, createdAt: '2024-01-01' }
const meLeader = { username: 'leader', isAdmin: false, isTeamLeader: true, teamId: 'team-1' }
const mePlayer = { username: 'player', isAdmin: false, isTeamLeader: false, teamId: 'team-1' }

// ── MSW Server ────────────────────────────────────────────────────────────────
const server = setupServer(
  http.get('/api/teams', () => HttpResponse.json([team1, team2])),
  http.get('/api/teams/:teamId/players', () => HttpResponse.json([])),
  http.get('/api/users/me', () => HttpResponse.json(mePlayer)),
)

beforeAll(() => server.listen())
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

function renderPage() {
  return render(
    <MemoryRouter>
      <TeamsPage />
    </MemoryRouter>
  )
}

describe('TeamsPage', () => {
  it('exibe mensagem de carregamento enquanto APIs estão pendentes', () => {
    server.use(
      http.get('/api/teams', async () => {
        await new Promise(() => {}) // never resolves
        return HttpResponse.json([])
      })
    )

    renderPage()
    expect(screen.getByText(/Carregando times/i)).toBeInTheDocument()
  })

  it('exibe role="alert" quando GET /api/teams falha', async () => {
    server.use(
      http.get('/api/teams', () => HttpResponse.error())
    )

    renderPage()

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })
  })

  it('exibe role="alert" quando GET /api/teams/{id}/players falha', async () => {
    server.use(
      http.get('/api/teams', () => HttpResponse.json([team1])),
      http.get('/api/teams/:teamId/players', () => HttpResponse.error())
    )

    renderPage()

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })
  })

  it('exibe "Nenhum time cadastrado." quando API retorna lista vazia', async () => {
    server.use(
      http.get('/api/teams', () => HttpResponse.json([]))
    )

    renderPage()

    await waitFor(() => {
      expect(screen.getByText('Nenhum time cadastrado.')).toBeInTheDocument()
    })
  })

  it('renderiza um card por time quando dados carregam com sucesso', async () => {
    renderPage()

    await waitFor(() => {
      const cards = screen.getAllByRole('article')
      expect(cards).toHaveLength(2)
    })
  })

  it('exibe nome e jogadores do time no card', async () => {
    server.use(
      http.get('/api/teams', () => HttpResponse.json([team1])),
      http.get('/api/teams/:teamId/players', () => HttpResponse.json([player1]))
    )

    renderPage()

    await waitFor(() => {
      expect(screen.getByText('Frog Alpha')).toBeInTheDocument()
      expect(screen.getByText('xXx_Frog')).toBeInTheDocument()
    })
  })

  it('exibe placeholder 🐸 quando time não tem logoUrl', async () => {
    server.use(
      http.get('/api/teams', () => HttpResponse.json([team2]))
    )

    renderPage()

    await waitFor(() => {
      expect(screen.getByLabelText('Sem logo')).toBeInTheDocument()
    })
  })

  it('exibe "Nenhum jogador cadastrado." quando time não tem jogadores', async () => {
    server.use(
      http.get('/api/teams', () => HttpResponse.json([team1])),
      http.get('/api/teams/:teamId/players', () => HttpResponse.json([]))
    )

    renderPage()

    await waitFor(() => {
      expect(screen.getByText('Nenhum jogador cadastrado.')).toBeInTheDocument()
    })
  })

  it('botões de logo visíveis quando usuário é líder do time', async () => {
    server.use(
      http.get('/api/teams', () => HttpResponse.json([team1])),
      http.get('/api/teams/:teamId/players', () => HttpResponse.json([])),
      http.get('/api/users/me', () => HttpResponse.json(meLeader))
    )

    renderPage()

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Alterar logo/i })).toBeInTheDocument()
    })
  })

  it('botões de logo ausentes quando usuário não é líder', async () => {
    server.use(
      http.get('/api/teams', () => HttpResponse.json([team1])),
      http.get('/api/teams/:teamId/players', () => HttpResponse.json([])),
      http.get('/api/users/me', () => HttpResponse.json(mePlayer))
    )

    renderPage()

    await waitFor(() => {
      expect(screen.queryByRole('button', { name: /Alterar logo/i })).not.toBeInTheDocument()
    })
  })
})

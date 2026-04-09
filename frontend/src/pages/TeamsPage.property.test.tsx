import { describe, it, afterEach, beforeAll, afterAll } from 'vitest'
import { render, screen, waitFor, cleanup } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { MemoryRouter } from 'react-router-dom'
import * as fc from 'fast-check'
import TeamsPage, { groupPlayersByTeam } from './TeamsPage'
import { safeString, safeId } from '../test/fc-helpers'

// ── Arbitrários base ──────────────────────────────────────────────────────────
const arbTeam = fc.record({
  id: safeId(),
  name: safeString({ minLength: 1, maxLength: 20 }),
  logoUrl: fc.option(fc.constant('https://example.com/logo.png'), { nil: null }),
  createdAt: fc.constant('2024-01-01'),
})

const arbMember = fc.record({
  id: safeId(),
  username: safeString({ minLength: 1, maxLength: 15 }),
  isTeamLeader: fc.boolean(),
})

// ── MSW Server ────────────────────────────────────────────────────────────────
const server = setupServer()

beforeAll(() => server.listen({ onUnhandledRequest: 'bypass' }))
afterAll(() => server.close())
afterEach(() => {
  server.resetHandlers()
  cleanup()
})

function renderPage() {
  return render(<MemoryRouter><TeamsPage /></MemoryRouter>)
}

// ── Property Tests ────────────────────────────────────────────────────────────

describe('TeamsPage — Property Tests', () => {

  // Feature: teams-menu, Property 1: número de cards = número de times
  it('Property 1: número de Team_Cards renderizados = número de times retornados pela API', async () => {
    // Validates: Requirements 3.3, 4.3
    await fc.assert(
      fc.asyncProperty(fc.array(arbTeam, { maxLength: 4 }), async (teams) => {
        server.use(
          http.get('/api/teams', () => HttpResponse.json(teams)),
          http.get('/api/teams/:teamId/members', () => HttpResponse.json([])),
          http.get('/api/users/me', () => HttpResponse.json({ isTeamLeader: false, teamId: null })),
        )
        renderPage()
        if (teams.length === 0) {
          await waitFor(() => expect(screen.getByText('Nenhum time cadastrado.')).toBeInTheDocument(), { timeout: 3000 })
        } else {
          await waitFor(() => expect(screen.getAllByRole('article')).toHaveLength(teams.length), { timeout: 3000 })
        }
        cleanup()
        server.resetHandlers()
      }),
      { numRuns: 10 }
    )
  }, 60000)

  // Feature: teams-menu, Property 2: nome e logo exibidos corretamente
  it('Property 2: nome do time sempre no DOM; img presente ↔ logoUrl preenchido', async () => {
    // Validates: Requirements 3.4, 3.5, 3.6
    await fc.assert(
      fc.asyncProperty(arbTeam, async (team) => {
        server.use(
          http.get('/api/teams', () => HttpResponse.json([team])),
          http.get('/api/teams/:teamId/members', () => HttpResponse.json([])),
          http.get('/api/users/me', () => HttpResponse.json({ isTeamLeader: false, teamId: null })),
        )
        renderPage()
        await waitFor(() => expect(screen.getByText(team.name)).toBeInTheDocument(), { timeout: 3000 })
        if (team.logoUrl) {
          expect(screen.getByAltText(`Logo ${team.name}`)).toBeInTheDocument()
        } else {
          expect(screen.getByLabelText('Sem logo')).toBeInTheDocument()
        }
        cleanup()
        server.resetHandlers()
      }),
      { numRuns: 10 }
    )
  }, 60000)

  // Feature: teams-menu, Property 3: membros do time exibidos corretamente
  it('Property 3: nicknames de todos os membros no DOM; lista vazia → mensagem', async () => {
    // Validates: Requirements 3.7, 3.8, 3.9
    const fixedTeam = { id: 'fixed-team-id', name: 'Fixed Team', logoUrl: null, createdAt: '2024-01-01' }
    await fc.assert(
      fc.asyncProperty(fc.array(arbMember, { maxLength: 4 }), async (members) => {
        server.use(
          http.get('/api/teams', () => HttpResponse.json([fixedTeam])),
          http.get('/api/teams/:teamId/members', () => HttpResponse.json(members)),
          http.get('/api/users/me', () => HttpResponse.json({ isTeamLeader: false, teamId: null })),
        )
        renderPage()
        if (members.length === 0) {
          await waitFor(() => expect(screen.getByText('Nenhum jogador cadastrado.')).toBeInTheDocument(), { timeout: 3000 })
        } else {
          await waitFor(() => {
            for (const m of members) {
              expect(screen.getAllByText(m.username, { exact: false }).length).toBeGreaterThan(0)
            }
          }, { timeout: 3000 })
        }
        cleanup()
        server.resetHandlers()
      }),
      { numRuns: 10 }
    )
  }, 60000)

  // Feature: teams-menu, Property 4: agrupamento de jogadores por time
  it('Property 4: groupPlayersByTeam produz mapa correto', () => {
    // Validates: Requirements 3.1, 3.7
    const arbPlayer = fc.array(fc.record({
      id: fc.uuid(),
      teamId: fc.option(fc.uuid(), { nil: null }),
      nickname: fc.string({ minLength: 1 }),
      realName: fc.constant(undefined),
      teamName: fc.constant(null),
      playerScore: fc.constant(0),
      matchesCount: fc.constant(0),
      createdAt: fc.constant('2024-01-01'),
    }))

    fc.assert(
      fc.property(arbPlayer, (players) => {
        const grouped = groupPlayersByTeam(players)
        for (const [teamId, group] of Object.entries(grouped)) {
          for (const player of group) {
            expect(player.teamId).toBe(teamId)
          }
          expect(group).toHaveLength(players.filter(p => p.teamId === teamId).length)
        }
        const withoutTeam = players.filter(p => !p.teamId)
        for (const player of withoutTeam) {
          for (const group of Object.values(grouped)) {
            expect(group).not.toContainEqual(player)
          }
        }
      }),
      { numRuns: 100 }
    )
  })

  // Feature: teams-menu, Property 5: controles de logo visíveis apenas para o líder
  it('Property 5: botão "Alterar logo" presente ↔ isLeader === true', async () => {
    // Validates: Requirements 5.1, 5.2, 5.7
    await fc.assert(
      fc.asyncProperty(arbTeam, fc.boolean(), async (team, isLeader) => {
        server.use(
          http.get('/api/teams', () => HttpResponse.json([team])),
          http.get('/api/teams/:teamId/members', () => HttpResponse.json([])),
          http.get('/api/users/me', () => HttpResponse.json({
            isTeamLeader: isLeader,
            teamId: isLeader ? team.id : null,
          })),
        )
        renderPage()
        await waitFor(() => expect(screen.getByText(team.name)).toBeInTheDocument(), { timeout: 3000 })
        const btn = screen.queryByRole('button', { name: /Alterar logo/i })
        if (isLeader) {
          expect(btn).toBeInTheDocument()
        } else {
          expect(btn).not.toBeInTheDocument()
        }
        cleanup()
        server.resetHandlers()
      }),
      { numRuns: 10 }
    )
  }, 60000)
})

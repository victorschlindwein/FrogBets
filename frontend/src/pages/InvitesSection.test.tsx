/**
 * Tests for InvitesSection in AdminPage.
 * Feature: invite-improvements
 */
import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import AdminPage from './AdminPage'

// Minimal mocks so AdminPage renders without crashing
const server = setupServer(
  http.get('/api/users/me', () =>
    HttpResponse.json({ id: '1', username: 'admin', isAdmin: true, isMasterAdmin: false })
  ),
  http.get('/api/users', () => HttpResponse.json([])),
  http.get('/api/games', () => HttpResponse.json([])),
  http.get('/api/invites', () => HttpResponse.json([])),
  http.get('/api/teams', () => HttpResponse.json([])),
  http.get('/api/players', () => HttpResponse.json([])),
)

beforeAll(() => server.listen())
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

// ── Unit tests ────────────────────────────────────────────────────────────

describe('InvitesSection — campo Expira em removido', () => {
  it('não exibe o campo "Expira em" no formulário', async () => {
    render(<AdminPage />)
    await waitFor(() => expect(screen.queryByLabelText(/expira em/i)).not.toBeInTheDocument())
  })
})

describe('InvitesSection — campo Destinatário condicional', () => {
  it('exibe o campo Destinatário quando quantity = 1 (padrão)', async () => {
    render(<AdminPage />)
    await waitFor(() =>
      expect(screen.getByLabelText(/destinatário/i)).toBeInTheDocument()
    )
  })

  /**
   * Feature: invite-improvements, Property 4: formulário oculta "Destinatário" quando quantity > 1
   * Validates: Requirements 2.4
   */
  it('oculta o campo Destinatário quando quantity > 1', async () => {
    render(<AdminPage />)
    const quantityInput = await screen.findByLabelText(/quantidade/i)
    fireEvent.change(quantityInput, { target: { value: '3' } })
    await waitFor(() =>
      expect(screen.queryByLabelText(/destinatário/i)).not.toBeInTheDocument()
    )
  })
})

/**
 * Feature: invite-improvements, Property 5: todos os tokens gerados são exibidos na UI
 * Validates: Requirements 2.5
 */
describe('InvitesSection — exibição de tokens gerados', () => {
  it('exibe todos os tokens retornados pela API após geração', async () => {
    const tokens = ['aabbccdd11223344aabbccdd11223344', 'ff00ff00ff00ff00ff00ff00ff00ff00']
    server.use(
      http.post('/api/invites', () =>
        HttpResponse.json({ tokens }, { status: 201 })
      )
    )

    render(<AdminPage />)
    const submitBtn = await screen.findByRole('button', { name: /gerar convite/i })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      for (const token of tokens) {
        expect(screen.getByText(token)).toBeInTheDocument()
      }
    })
  })
})

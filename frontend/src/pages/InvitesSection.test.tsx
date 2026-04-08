/**
 * Tests for InvitesSection in AdminPage.
 * Feature: admin-usability-improvements
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

describe('InvitesSection — descrições individuais por convite', () => {
  it('exibe 1 input de descrição quando quantity = 1 (padrão)', async () => {
    render(<AdminPage />)
    await waitFor(() => {
      const inputs = screen.getAllByPlaceholderText('Nome ou identificação')
      expect(inputs).toHaveLength(1)
    })
  })

  /**
   * Feature: admin-usability-improvements, Property: N inputs de descrição quando quantity > 1
   * Validates: Requirements 6.1
   */
  it('exibe N inputs de descrição quando quantity > 1', async () => {
    render(<AdminPage />)
    const quantityInput = await screen.findByLabelText(/quantidade/i)
    fireEvent.change(quantityInput, { target: { value: '3' } })
    await waitFor(() => {
      expect(screen.getByPlaceholderText('Convite 1')).toBeInTheDocument()
      expect(screen.getByPlaceholderText('Convite 2')).toBeInTheDocument()
      expect(screen.getByPlaceholderText('Convite 3')).toBeInTheDocument()
    })
  })

  it('preserva valores existentes ao aumentar quantity', async () => {
    render(<AdminPage />)
    const quantityInput = await screen.findByLabelText(/quantidade/i)

    // Preenche o primeiro input com quantity=1
    const firstInput = await screen.findByPlaceholderText('Nome ou identificação')
    fireEvent.change(firstInput, { target: { value: 'Alice' } })

    // Aumenta para 2
    fireEvent.change(quantityInput, { target: { value: '2' } })

    await waitFor(() => {
      const input1 = screen.getByPlaceholderText('Convite 1') as HTMLInputElement
      expect(input1.value).toBe('Alice')
      expect(screen.getByPlaceholderText('Convite 2')).toBeInTheDocument()
    })
  })
})

/**
 * Feature: admin-usability-improvements, Property: N POSTs paralelos com description individual
 * Validates: Requirements 6.2
 */
describe('InvitesSection — submit envia N POSTs com description individual', () => {
  it('envia um POST por convite com quantity=1 e description individual', async () => {
    const capturedBodies: Array<{ quantity: number; description: string | null }> = []

    server.use(
      http.post('/api/invites', async ({ request }) => {
        const body = await request.json() as { quantity: number; description: string | null }
        capturedBodies.push(body)
        return HttpResponse.json({ tokens: [`token-${capturedBodies.length}`] }, { status: 201 })
      })
    )

    render(<AdminPage />)
    const quantityInput = await screen.findByLabelText(/quantidade/i)
    fireEvent.change(quantityInput, { target: { value: '2' } })

    await waitFor(() => {
      expect(screen.getByPlaceholderText('Convite 1')).toBeInTheDocument()
    })

    fireEvent.change(screen.getByPlaceholderText('Convite 1'), { target: { value: 'Alice' } })
    fireEvent.change(screen.getByPlaceholderText('Convite 2'), { target: { value: 'Bob' } })

    fireEvent.click(screen.getByRole('button', { name: /gerar convite/i }))

    await waitFor(() => {
      expect(capturedBodies).toHaveLength(2)
      expect(capturedBodies[0]).toEqual({ quantity: 1, description: 'Alice' })
      expect(capturedBodies[1]).toEqual({ quantity: 1, description: 'Bob' })
    })
  })

  it('envia description: null quando o campo está vazio', async () => {
    const capturedBodies: Array<{ quantity: number; description: string | null }> = []

    server.use(
      http.post('/api/invites', async ({ request }) => {
        const body = await request.json() as { quantity: number; description: string | null }
        capturedBodies.push(body)
        return HttpResponse.json({ tokens: [`token-${capturedBodies.length}`] }, { status: 201 })
      })
    )

    render(<AdminPage />)
    const submitBtn = await screen.findByRole('button', { name: /gerar convite/i })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(capturedBodies).toHaveLength(1)
      expect(capturedBodies[0]).toEqual({ quantity: 1, description: null })
    })
  })
})

/**
 * Feature: admin-usability-improvements, Property: todos os tokens gerados são exibidos na UI
 * Validates: Requirements 6.2
 */
describe('InvitesSection — exibição de tokens gerados', () => {
  it('exibe todos os tokens retornados pela API após geração em massa', async () => {
    let callCount = 0
    const tokenMap = ['aabbccdd11223344aabbccdd11223344', 'ff00ff00ff00ff00ff00ff00ff00ff00']

    server.use(
      http.post('/api/invites', () => {
        const token = tokenMap[callCount++]
        return HttpResponse.json({ tokens: [token] }, { status: 201 })
      })
    )

    render(<AdminPage />)
    const quantityInput = await screen.findByLabelText(/quantidade/i)
    fireEvent.change(quantityInput, { target: { value: '2' } })

    await waitFor(() => expect(screen.getByPlaceholderText('Convite 1')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: /gerar convite/i }))

    await waitFor(() => {
      for (const token of tokenMap) {
        expect(screen.getByText(token)).toBeInTheDocument()
      }
    })
  })
})

describe('InvitesSection — reset após submit bem-sucedido', () => {
  it('reseta descriptions para [""] e quantity para 1 após submit', async () => {
    server.use(
      http.post('/api/invites', () =>
        HttpResponse.json({ tokens: ['sometoken123'] }, { status: 201 })
      )
    )

    render(<AdminPage />)
    const quantityInput = await screen.findByLabelText(/quantidade/i)
    fireEvent.change(quantityInput, { target: { value: '3' } })

    await waitFor(() => expect(screen.getByPlaceholderText('Convite 3')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: /gerar convite/i }))

    await waitFor(() => {
      // After reset, quantity=1 so placeholder is 'Nome ou identificação'
      expect(screen.getByPlaceholderText('Nome ou identificação')).toBeInTheDocument()
      const quantityEl = screen.getByLabelText(/quantidade/i) as HTMLInputElement
      expect(quantityEl.value).toBe('1')
    })
  })
})

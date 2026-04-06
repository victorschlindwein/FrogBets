import { useEffect, useState } from 'react'
import apiClient from '../api/client'

interface User {
  id: string
  username: string
  isAdmin: boolean
}

interface Invite {
  id: string
  token: string
  description: string | null
  expiresAt: string
  createdAt: string
  status: 'Pending' | 'Used' | 'Expired'
}

interface Market {
  id: string
  type: string
  mapNumber: number | null
  status: string
}

interface Game {
  id: string
  teamA: string
  teamB: string
  scheduledAt: string
  numberOfMaps: number
  status: 'Scheduled' | 'InProgress' | 'Finished'
  markets?: Market[]
}

const MARKET_TYPE_LABELS: Record<string, string> = {
  MapWinner: 'Vencedor do Mapa',
  SeriesWinner: 'Vencedor da Série',
  TopKills: 'Top Kills',
  MostDeaths: 'Mais Mortes',
  MostUtilityDamage: 'Maior Dano por Utilitários',
}

function marketLabel(market: Market): string {
  const type = MARKET_TYPE_LABELS[market.type] ?? market.type
  return market.mapNumber != null ? `${type} — Mapa ${market.mapNumber}` : type
}

function CreateGameForm({ onCreated }: { onCreated: () => void }) {
  const [teamA, setTeamA] = useState('')
  const [teamB, setTeamB] = useState('')
  const [scheduledAt, setScheduledAt] = useState('')
  const [numberOfMaps, setNumberOfMaps] = useState('1')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true)
    setSuccess(null)
    setError(null)
    try {
      await apiClient.post('/games', {
        teamA,
        teamB,
        scheduledAt: new Date(scheduledAt).toISOString(),
        numberOfMaps: parseInt(numberOfMaps, 10),
      })
      setSuccess('Jogo cadastrado com sucesso!')
      setTeamA('')
      setTeamB('')
      setScheduledAt('')
      setNumberOfMaps('1')
      onCreated()
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: { message?: string } } } }
      setError(axiosErr.response?.data?.error?.message ?? 'Erro ao cadastrar jogo.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section>
      <h2>Cadastrar Jogo</h2>
      <div className="card">
      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label htmlFor="teamA">Time A:</label>
          <input
            id="teamA"
            type="text"
            value={teamA}
            onChange={e => setTeamA(e.target.value)}
            required
          />
        </div>
        <div className="form-group">
          <label htmlFor="teamB">Time B:</label>
          <input
            id="teamB"
            type="text"
            value={teamB}
            onChange={e => setTeamB(e.target.value)}
            required
          />
        </div>
        <div className="form-group">
          <label htmlFor="scheduledAt">Data e Hora:</label>
          <input
            id="scheduledAt"
            type="datetime-local"
            value={scheduledAt}
            onChange={e => setScheduledAt(e.target.value)}
            required
          />
        </div>
        <div className="form-group">
          <label htmlFor="numberOfMaps">Número de Mapas:</label>
          <input
            id="numberOfMaps"
            type="number"
            min="1"
            max="5"
            value={numberOfMaps}
            onChange={e => setNumberOfMaps(e.target.value)}
            required
          />
        </div>
        <button type="submit" disabled={submitting}>
          {submitting ? 'Cadastrando...' : 'Cadastrar Jogo'}
        </button>
        {success && <p role="status">{success}</p>}
        {error && <p role="alert">{error}</p>}
      </form>
      </div>
    </section>
  )
}

function StartGameSection({ games, onStarted }: { games: Game[]; onStarted: () => void }) {
  const [selectedGameId, setSelectedGameId] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const scheduledGames = games.filter(g => g.status === 'Scheduled')

  async function handleStart(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedGameId) return
    setSubmitting(true)
    setSuccess(null)
    setError(null)
    try {
      await apiClient.patch(`/games/${selectedGameId}/start`)
      setSuccess('Jogo iniciado com sucesso!')
      setSelectedGameId('')
      onStarted()
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: { message?: string } } } }
      setError(axiosErr.response?.data?.error?.message ?? 'Erro ao iniciar jogo.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section>
      <h2>Iniciar Jogo</h2>
      {scheduledGames.length === 0 ? (
        <p>Nenhum jogo agendado disponível para iniciar.</p>
      ) : (
        <form onSubmit={handleStart}>
          <div>
            <label htmlFor="startGameSelect">Selecionar Jogo:</label>
            <select
              id="startGameSelect"
              value={selectedGameId}
              onChange={e => setSelectedGameId(e.target.value)}
              required
            >
              <option value="">Selecione um jogo</option>
              {scheduledGames.map(game => (
                <option key={game.id} value={game.id}>
                  {game.teamA} vs {game.teamB} — {new Date(game.scheduledAt).toLocaleString('pt-BR')}
                </option>
              ))}
            </select>
          </div>
          <button type="submit" disabled={submitting || !selectedGameId}>
            {submitting ? 'Iniciando...' : 'Iniciar Jogo'}
          </button>
          {success && <p role="status">{success}</p>}
          {error && <p role="alert">{error}</p>}
        </form>
      )}
    </section>
  )
}

function RegisterResultSection({ games }: { games: Game[] }) {
  const [selectedGameId, setSelectedGameId] = useState('')
  const [selectedMarketId, setSelectedMarketId] = useState('')
  const [winningOption, setWinningOption] = useState('')
  const [markets, setMarkets] = useState<Market[]>([])
  const [loadingMarkets, setLoadingMarkets] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const activeGames = games.filter(g => g.status === 'InProgress')

  useEffect(() => {
    if (!selectedGameId) {
      setMarkets([])
      setSelectedMarketId('')
      return
    }
    setLoadingMarkets(true)
    setSelectedMarketId('')
    apiClient.get<Game>(`/games/${selectedGameId}`)
      .then(res => setMarkets(res.data.markets ?? []))
      .catch(() => setMarkets([]))
      .finally(() => setLoadingMarkets(false))
  }, [selectedGameId])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedGameId || !selectedMarketId || !winningOption) return
    setSubmitting(true)
    setSuccess(null)
    setError(null)
    try {
      await apiClient.post(`/games/${selectedGameId}/results`, {
        marketId: selectedMarketId,
        winningOption,
      })
      setSuccess('Resultado registrado com sucesso!')
      setSelectedGameId('')
      setSelectedMarketId('')
      setWinningOption('')
      setMarkets([])
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: { message?: string } } } }
      setError(axiosErr.response?.data?.error?.message ?? 'Erro ao registrar resultado.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section>
      <h2>Registrar Resultado</h2>
      {activeGames.length === 0 ? (
        <p>Nenhum jogo em andamento disponível.</p>
      ) : (
        <form onSubmit={handleSubmit}>
          <div>
            <label htmlFor="resultGameSelect">Selecionar Jogo:</label>
            <select
              id="resultGameSelect"
              value={selectedGameId}
              onChange={e => setSelectedGameId(e.target.value)}
              required
            >
              <option value="">Selecione um jogo</option>
              {activeGames.map(game => (
                <option key={game.id} value={game.id}>
                  {game.teamA} vs {game.teamB}
                </option>
              ))}
            </select>
          </div>
          {selectedGameId && (
            <div>
              <label htmlFor="marketSelect">Selecionar Mercado:</label>
              {loadingMarkets ? (
                <p>Carregando mercados...</p>
              ) : (
                <select
                  id="marketSelect"
                  value={selectedMarketId}
                  onChange={e => setSelectedMarketId(e.target.value)}
                  required
                >
                  <option value="">Selecione um mercado</option>
                  {markets.map(market => (
                    <option key={market.id} value={market.id}>
                      {marketLabel(market)}
                    </option>
                  ))}
                </select>
              )}
            </div>
          )}
          {selectedMarketId && (
            <div>
              <label htmlFor="winningOption">Opção Vencedora:</label>
              <input
                id="winningOption"
                type="text"
                value={winningOption}
                onChange={e => setWinningOption(e.target.value)}
                placeholder="Ex: TeamA ou nome do jogador"
                required
              />
            </div>
          )}
          <button type="submit" disabled={submitting || !selectedGameId || !selectedMarketId || !winningOption}>
            {submitting ? 'Registrando...' : 'Registrar Resultado'}
          </button>
          {success && <p role="status">{success}</p>}
          {error && <p role="alert">{error}</p>}
        </form>
      )}
    </section>
  )
}

function InvitesSection() {
  const [invites, setInvites] = useState<Invite[]>([])
  const [expiresAt, setExpiresAt] = useState('')
  const [description, setDescription] = useState('')
  const [newToken, setNewToken] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  function loadInvites() {
    apiClient.get<Invite[]>('/invites')
      .then(res => setInvites(res.data))
      .catch(() => {})
  }

  useEffect(() => { loadInvites() }, [])

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    setNewToken(null)
    try {
      const res = await apiClient.post<Invite>('/invites', {
        expiresAt: new Date(expiresAt).toISOString(),
        description: description || null,
      })
      setNewToken(res.data.token)
      setExpiresAt('')
      setDescription('')
      loadInvites()
    } catch {
      setError('Erro ao gerar convite.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleRevoke(id: string) {
    try {
      await apiClient.delete(`/invites/${id}`)
      loadInvites()
    } catch (err: unknown) {
      const code = (err as { response?: { data?: { error?: { code?: string } } } })
        ?.response?.data?.error?.code
      alert(code === 'INVITE_ALREADY_USED' ? 'Convite já utilizado.' : 'Convite já expirado.')
    }
  }

  return (
    <section>
      <h2>🎟️ Convites</h2>
      <div className="card">
      <form onSubmit={handleCreate}>
        <div className="form-group">
          <label htmlFor="inviteExpiresAt">Expira em:</label>
          <input
            id="inviteExpiresAt"
            type="datetime-local"
            value={expiresAt}
            onChange={e => setExpiresAt(e.target.value)}
            required
          />
        </div>
        <div className="form-group">
          <label htmlFor="inviteDescription">Destinatário (opcional):</label>
          <input
            id="inviteDescription"
            type="text"
            value={description}
            onChange={e => setDescription(e.target.value)}
          />
        </div>
        <button type="submit" disabled={submitting}>
          {submitting ? 'Gerando...' : 'Gerar Convite'}
        </button>
        {error && <p role="alert">{error}</p>}
      </form>
      {newToken && (
        <p role="status" style={{ marginTop: '1rem' }}>
          Convite gerado: <span className="token-display">{newToken}</span>
        </p>
      )}
      </div>
      <div className="card" style={{ padding: 0, overflow: 'hidden', marginTop: '1rem' }}>
      <table>
        <thead>
          <tr>
            <th>Token</th>
            <th>Descrição</th>
            <th>Status</th>
            <th>Criado em</th>
            <th>Expira em</th>
            <th>Ação</th>
          </tr>
        </thead>
        <tbody>
          {invites.map(invite => (
            <tr key={invite.id}>
              <td><code style={{ fontSize: '.8rem' }}>{invite.token}</code></td>
              <td>{invite.description ?? '—'}</td>
              <td>
                <span className={`badge badge-${invite.status.toLowerCase()}`}>{invite.status}</span>
              </td>
              <td>{new Date(invite.createdAt).toLocaleString('pt-BR')}</td>
              <td>{new Date(invite.expiresAt).toLocaleString('pt-BR')}</td>
              <td>
                {invite.status === 'Pending' && (
                  <button className="btn-danger" onClick={() => handleRevoke(invite.id)} style={{ padding: '.3rem .7rem', fontSize: '.8rem' }}>Revogar</button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      </div>
    </section>
  )
}

export default function AdminPage() {
  const [user, setUser] = useState<User | null>(null)
  const [games, setGames] = useState<Game[]>([])
  const [loading, setLoading] = useState(true)
  const [accessDenied, setAccessDenied] = useState(false)

  function loadGames() {
    apiClient.get<Game[]>('/games')
      .then(res => setGames(res.data))
      .catch(() => {})
  }

  useEffect(() => {
    apiClient.get<User>('/users/me')
      .then(res => {
        if (!res.data.isAdmin) {
          setAccessDenied(true)
        } else {
          setUser(res.data)
          loadGames()
        }
      })
      .catch(() => setAccessDenied(true))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <p>Carregando...</p>
  if (accessDenied || !user) return <p role="alert">Acesso negado</p>

  return (
    <div className="page">
      <h1>⚙️ Administração</h1>
      <CreateGameForm onCreated={loadGames} />
      <StartGameSection games={games} onStarted={loadGames} />
      <RegisterResultSection games={games} />
      <InvitesSection />
    </div>
  )
}

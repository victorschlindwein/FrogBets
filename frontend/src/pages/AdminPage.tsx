import { useEffect, useState, useCallback } from 'react'
import apiClient from '../api/client'
import { getTeams, createTeam, CS2Team, getPlayers, CS2Player, getGamePlayers, registerMatchStats, createMapResult, getMapResultsByGame, MapResult } from '../api/players'

interface User { id: string; username: string; isAdmin: boolean; isMasterAdmin?: boolean; teamId?: string | null; teamName?: string | null; isTeamLeader?: boolean }
interface Invite {
  id: string; token: string; description: string | null
  expiresAt: string; createdAt: string; status: 'Pending' | 'Used' | 'Expired'
}
interface Market { id: string; type: string; mapNumber: number | null; status: string }
interface Game {
  id: string; teamA: string; teamB: string; scheduledAt: string
  numberOfMaps: number; status: 'Scheduled' | 'InProgress' | 'Finished'; markets?: Market[]
}

const MARKET_TYPE_LABELS: Record<string, string> = {
  MapWinner: 'Vencedor do Mapa', SeriesWinner: 'Vencedor da Série',
  TopKills: 'Top Kills', MostDeaths: 'Mais Mortes', MostUtilityDamage: 'Maior Dano por Utilitários',
}
function marketLabel(m: Market) {
  const t = MARKET_TYPE_LABELS[m.type] ?? m.type
  return m.mapNumber != null ? `${t} — Mapa ${m.mapNumber}` : t
}

// ── Índice de navegação ───────────────────────────────────────────────────
const NAV_ITEMS_BASE = [
  { id: 'sec-users',       label: '👥 Usuários' },
  { id: 'sec-games',       label: '🎮 Cadastrar Jogo' },
  { id: 'sec-edit-game',   label: '✏️ Editar Jogo' },
  { id: 'sec-delete-game', label: '🗑️ Excluir Jogo' },
  { id: 'sec-start',       label: '▶️ Iniciar Jogo' },
  { id: 'sec-result',      label: '🏁 Registrar Resultado' },
  { id: 'sec-invites',     label: '🎟️ Convites' },
  { id: 'sec-teams',       label: '🛡️ Times' },
  { id: 'sec-players',     label: '👤 Jogadores' },
  { id: 'sec-map-results', label: '📍 Mapas' },
  { id: 'sec-stats',       label: '📊 Estatísticas' },
  { id: 'sec-leaders',     label: '👑 Gestão de Líderes' },
  { id: 'sec-swap',        label: '🔄 Troca Direta' },
]

const NAV_ITEM_ADMINS = { id: 'sec-admins', label: '🔐 Admins' }

function AdminNav({ isMasterAdmin }: { isMasterAdmin: boolean }) {
  const items = isMasterAdmin
    ? [NAV_ITEMS_BASE[0], NAV_ITEM_ADMINS, ...NAV_ITEMS_BASE.slice(1)]
    : NAV_ITEMS_BASE
  return (
    <nav className="admin-index card">
      <strong>Índice</strong>
      <ul>
        {items.map(item => (
          <li key={item.id}>
            <a href={`#${item.id}`}>{item.label}</a>
          </li>
        ))}
      </ul>
    </nav>
  )
}

// ── Lista de Usuários ─────────────────────────────────────────────────────
function UsersSection({ currentUser, teams }: { currentUser: User; teams: CS2Team[] }) {
  const [users, setUsers] = useState<User[]>([])
  const [copied, setCopied] = useState<string | null>(null)

  function loadUsers() {
    apiClient.get<User[]>('/users').then(res => setUsers(res.data)).catch(() => {})
  }
  useEffect(() => { loadUsers() }, [])

  function copyId(id: string) {
    navigator.clipboard.writeText(id).then(() => {
      setCopied(id)
      setTimeout(() => setCopied(null), 1500)
    })
  }

  const teamNameById = Object.fromEntries(teams.map(t => [t.id, t.name]))

  return (
    <section id="sec-users">
      <h2>👥 Usuários</h2>
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        <div className="table-wrapper">
          <table>
            <thead>
              <tr><th>Username</th><th>ID</th><th>Admin</th><th>Líder</th><th>Time</th></tr>
            </thead>
            <tbody>
              {users.map(u => (
                <tr key={u.id}>
                  <td>
                    {u.username}
                    {u.id === currentUser.id && <span className="badge badge-active" style={{ marginLeft: '.5rem' }}>você</span>}
                  </td>
                  <td>
                    <code style={{ fontSize: '.75rem', color: 'var(--text-muted)' }}>{u.id}</code>
                    <button
                      type="button"
                      className="btn-secondary"
                      onClick={() => copyId(u.id)}
                      style={{ marginLeft: '.5rem', padding: '.15rem .5rem', fontSize: '.75rem' }}
                      title="Copiar ID"
                    >
                      {copied === u.id ? '✓' : '📋'}
                    </button>
                  </td>
                  <td>{u.isAdmin ? '✅' : '—'}</td>
                  <td>{u.isTeamLeader ? '✅' : '—'}</td>
                  <td>{u.teamName ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  )
}

// ── Gestão de Admins ──────────────────────────────────────────────────────
function AdminManagementSection({ users, currentUser, onUsersChange }: {
  users: User[]
  currentUser: User
  onUsersChange: () => void
}) {
  const [submitting, setSubmitting] = useState<string | null>(null)
  const [message, setMessage] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  async function promote(userId: string) {
    setSubmitting(userId); setMessage(null)
    try {
      await apiClient.post(`/users/${userId}/promote`)
      setMessage({ type: 'ok', text: 'Usuário promovido a admin.' })
      onUsersChange()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setMessage({ type: 'err', text: e2.response?.data?.error?.message ?? 'Erro ao promover.' })
    } finally { setSubmitting(null) }
  }

  async function demote(userId: string) {
    setSubmitting(userId); setMessage(null)
    try {
      await apiClient.post(`/users/${userId}/demote`)
      setMessage({ type: 'ok', text: 'Direitos de admin revogados.' })
      onUsersChange()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setMessage({ type: 'err', text: e2.response?.data?.error?.message ?? 'Erro ao revogar.' })
    } finally { setSubmitting(null) }
  }

  const nonAdmins = users.filter(u => !u.isAdmin)
  const admins    = users.filter(u => u.isAdmin && u.id !== currentUser.id)

  return (
    <section id="sec-admins">
      <h2>🔐 Gestão de Admins</h2>

      <div className="card">
        <h3>Promover a Admin</h3>
        {nonAdmins.length === 0
          ? <p style={{ color: 'var(--text-muted)', fontSize: '.9rem' }}>Todos os usuários já são admins.</p>
          : <div className="table-wrapper">
              <table>
                <thead><tr><th>Username</th><th>Ação</th></tr></thead>
                <tbody>
                  {nonAdmins.map(u => (
                    <tr key={u.id}>
                      <td>{u.username}</td>
                      <td>
                        <button
                          type="button"
                          onClick={() => promote(u.id)}
                          disabled={submitting === u.id}
                          style={{ padding: '.3rem .7rem', fontSize: '.8rem' }}
                        >
                          {submitting === u.id ? '...' : 'Promover'}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
        }
      </div>

      <div className="card" style={{ marginTop: '1rem' }}>
        <h3>Revogar Admin</h3>
        {admins.length === 0
          ? <p style={{ color: 'var(--text-muted)', fontSize: '.9rem' }}>Nenhum outro admin para revogar.</p>
          : <div className="table-wrapper">
              <table>
                <thead><tr><th>Username</th><th>Ação</th></tr></thead>
                <tbody>
                  {admins.map(u => (
                    <tr key={u.id}>
                      <td>{u.username}</td>
                      <td>
                        <button
                          type="button"
                          className="btn-danger"
                          onClick={() => demote(u.id)}
                          disabled={submitting === u.id}
                          style={{ padding: '.3rem .7rem', fontSize: '.8rem' }}
                        >
                          {submitting === u.id ? '...' : 'Revogar'}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
        }
        {message && (
          <p role={message.type === 'ok' ? 'status' : 'alert'} style={{ marginTop: '.75rem' }}>
            {message.text}
          </p>
        )}
      </div>
    </section>
  )
}

// ── Cadastrar Jogo (com dropdown de times) ────────────────────────────────
function CreateGameForm({ teams, onCreated }: { teams: CS2Team[]; onCreated: () => void }) {
  const [teamAId, setTeamAId] = useState('')
  const [teamBId, setTeamBId] = useState('')
  const [scheduledAt, setScheduledAt] = useState('')
  const [numberOfMaps, setNumberOfMaps] = useState('1')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const teamA = teams.find(t => t.id === teamAId)?.name ?? ''
    const teamB = teams.find(t => t.id === teamBId)?.name ?? ''
    if (teamAId === teamBId) { setError('Os dois times devem ser diferentes.'); return }
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await apiClient.post('/games', {
        teamA, teamB,
        scheduledAt: new Date(scheduledAt).toISOString(),
        numberOfMaps: parseInt(numberOfMaps, 10),
      })
      setSuccess('Jogo cadastrado com sucesso!')
      setTeamAId(''); setTeamBId(''); setScheduledAt(''); setNumberOfMaps('1')
      onCreated()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao cadastrar jogo.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="sec-games">
      <h2>🎮 Cadastrar Jogo</h2>
      <div className="card">
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="teamASelect">Time A:</label>
            <select id="teamASelect" value={teamAId} onChange={e => setTeamAId(e.target.value)} required>
              <option value="">Selecione o Time A</option>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="teamBSelect">Time B:</label>
            <select id="teamBSelect" value={teamBId} onChange={e => setTeamBId(e.target.value)} required>
              <option value="">Selecione o Time B</option>
              {teams.filter(t => t.id !== teamAId).map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="scheduledAt">Data e Hora:</label>
            <input id="scheduledAt" type="datetime-local" value={scheduledAt} onChange={e => setScheduledAt(e.target.value)} required />
          </div>
          <div className="form-group">
            <label htmlFor="numberOfMaps">Número de Mapas:</label>
            <input id="numberOfMaps" type="number" min="1" max="5" value={numberOfMaps} onChange={e => setNumberOfMaps(e.target.value)} required />
          </div>
          <button type="submit" disabled={submitting}>{submitting ? 'Cadastrando...' : 'Cadastrar Jogo'}</button>
          {success && <p role="status">{success}</p>}
          {error && <p role="alert">{error}</p>}
        </form>
      </div>
    </section>
  )
}

// ── Iniciar Jogo ──────────────────────────────────────────────────────────
function StartGameSection({ games, onStarted }: { games: Game[]; onStarted: () => void }) {
  const [selectedGameId, setSelectedGameId] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const scheduledGames = games.filter(g => g.status === 'Scheduled')

  async function handleStart(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedGameId) return
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await apiClient.patch(`/games/${selectedGameId}/start`)
      setSuccess('Jogo iniciado com sucesso!')
      setSelectedGameId('')
      onStarted()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao iniciar jogo.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="sec-start">
      <h2>▶️ Iniciar Jogo</h2>
      {scheduledGames.length === 0 ? (
        <div className="card empty-card"><p>Nenhum jogo agendado disponível para iniciar.</p></div>
      ) : (
        <div className="card">
          <form onSubmit={handleStart}>
            <div className="form-group">
              <label htmlFor="startGameSelect">Selecionar Jogo:</label>
              <select id="startGameSelect" value={selectedGameId} onChange={e => setSelectedGameId(e.target.value)} required>
                <option value="">Selecione um jogo</option>
                {scheduledGames.map(g => (
                  <option key={g.id} value={g.id}>{g.teamA} vs {g.teamB} — {new Date(g.scheduledAt).toLocaleString('pt-BR')}</option>
                ))}
              </select>
            </div>
            <button type="submit" disabled={submitting || !selectedGameId}>{submitting ? 'Iniciando...' : 'Iniciar Jogo'}</button>
            {success && <p role="status">{success}</p>}
            {error && <p role="alert">{error}</p>}
          </form>
        </div>
      )}
    </section>
  )
}

// ── Registrar Resultado (sem seleção de mercado — registra por jogo) ──────
function RegisterResultSection({ games }: { games: Game[] }) {
  const [selectedGameId, setSelectedGameId] = useState('')
  const [markets, setMarkets] = useState<Market[]>([])
  const [results, setResults] = useState<Record<string, string>>({})
  const [loadingMarkets, setLoadingMarkets] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const activeGames = games.filter(g => g.status === 'InProgress')

  useEffect(() => {
    if (!selectedGameId) { setMarkets([]); setResults({}); return }
    setLoadingMarkets(true)
    apiClient.get<Game>(`/games/${selectedGameId}`)
      .then(res => {
        const open = (res.data.markets ?? []).filter(m => m.status === 'Open')
        setMarkets(open)
        setResults(Object.fromEntries(open.map(m => [m.id, ''])))
      })
      .catch(() => setMarkets([]))
      .finally(() => setLoadingMarkets(false))
  }, [selectedGameId])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const pending = markets.filter(m => results[m.id]?.trim())
    if (pending.length === 0) { setError('Preencha ao menos um resultado.'); return }
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await Promise.all(pending.map(m =>
        apiClient.post(`/games/${selectedGameId}/results`, {
          marketId: m.id,
          winningOption: results[m.id].trim(),
        })
      ))
      setSuccess(`${pending.length} resultado(s) registrado(s) com sucesso!`)
      setSelectedGameId(''); setMarkets([]); setResults({})
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao registrar resultado.')
    } finally { setSubmitting(false) }
  }

  const selectedGame = activeGames.find(g => g.id === selectedGameId)

  return (
    <section id="sec-result">
      <h2>🏁 Registrar Resultado</h2>
      {activeGames.length === 0 ? (
        <div className="card empty-card"><p>Nenhum jogo em andamento disponível.</p></div>
      ) : (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label htmlFor="resultGameSelect">Selecionar Jogo:</label>
              <select id="resultGameSelect" value={selectedGameId} onChange={e => setSelectedGameId(e.target.value)} required>
                <option value="">Selecione um jogo</option>
                {activeGames.map(g => (
                  <option key={g.id} value={g.id}>{g.teamA} vs {g.teamB}</option>
                ))}
              </select>
            </div>

            {loadingMarkets && <p style={{ color: 'var(--text-muted)' }}>Carregando mercados...</p>}

            {!loadingMarkets && selectedGameId && markets.length === 0 && (
              <p style={{ color: 'var(--text-muted)' }}>Nenhum mercado aberto para este jogo.</p>
            )}

            {!loadingMarkets && markets.length > 0 && (
              <>
                <p style={{ fontSize: '.85rem', color: 'var(--text-muted)', marginBottom: '.75rem' }}>
                  Preencha a opção vencedora para cada mercado. Deixe em branco para pular.
                </p>
                {markets.map(m => (
                  <div className="form-group" key={m.id}>
                    <label>{marketLabel(m)}</label>
                    {['MapWinner', 'SeriesWinner'].includes(m.type) ? (
                      <select value={results[m.id] ?? ''} onChange={e => setResults(r => ({ ...r, [m.id]: e.target.value }))}>
                        <option value="">— pular —</option>
                        <option value={selectedGame?.teamA}>{selectedGame?.teamA}</option>
                        <option value={selectedGame?.teamB}>{selectedGame?.teamB}</option>
                      </select>
                    ) : (
                      <input
                        type="text"
                        placeholder="Nome do jogador ou deixe em branco"
                        value={results[m.id] ?? ''}
                        onChange={e => setResults(r => ({ ...r, [m.id]: e.target.value }))}
                      />
                    )}
                  </div>
                ))}
                <button type="submit" disabled={submitting}>{submitting ? 'Registrando...' : 'Registrar Resultados'}</button>
              </>
            )}

            {success && <p role="status">{success}</p>}
            {error && <p role="alert">{error}</p>}
          </form>
        </div>
      )}
    </section>
  )
}

// ── Convites ──────────────────────────────────────────────────────────────
function InvitesSection() {
  const [invites, setInvites] = useState<Invite[]>([])
  const [quantity, setQuantity] = useState(1)
  const [descriptions, setDescriptions] = useState<string[]>([''])
  const [newTokens, setNewTokens] = useState<string[]>([])
  const [copiedToken, setCopiedToken] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  function loadInvites() {
    apiClient.get<Invite[]>('/invites').then(res => setInvites(res.data)).catch(() => {})
  }
  useEffect(() => { loadInvites() }, [])

  useEffect(() => {
    setDescriptions(prev => {
      const next = Array(quantity).fill('')
      prev.forEach((v, i) => { if (i < quantity) next[i] = v })
      return next
    })
  }, [quantity])

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true); setError(null); setNewTokens([])
    try {
      const results = await Promise.all(
        descriptions.map(desc =>
          apiClient.post<{ tokens: string[] }>('/invites', {
            quantity: 1,
            description: desc || null,
          })
        )
      )
      setNewTokens(results.flatMap(r => r.data.tokens))
      setDescriptions([''])
      setQuantity(1)
      loadInvites()
    } catch { setError('Erro ao gerar convite(s).') }
    finally { setSubmitting(false) }
  }

  function copyToken(token: string) {
    navigator.clipboard.writeText(token).then(() => {
      setCopiedToken(token)
      setTimeout(() => setCopiedToken(null), 1500)
    })
  }

  async function handleRevoke(id: string) {
    try {
      await apiClient.delete(`/invites/${id}`)
      loadInvites()
    } catch (err: unknown) {
      const code = (err as { response?: { data?: { error?: { code?: string } } } })?.response?.data?.error?.code
      alert(code === 'INVITE_ALREADY_USED' ? 'Convite já utilizado.' : 'Convite já expirado.')
    }
  }

  return (
    <section id="sec-invites">
      <h2>🎟️ Convites</h2>
      <div className="card">
        <form onSubmit={handleCreate}>
          <div className="form-group">
            <label htmlFor="inviteQuantity">Quantidade:</label>
            <input id="inviteQuantity" type="number" min="1" max="50" value={quantity} onChange={e => setQuantity(parseInt(e.target.value, 10) || 1)} required />
          </div>
          <div className="form-group">
            <label>Destinatário(s) (opcional):</label>
            {descriptions.map((desc, i) => (
              <input
                key={i}
                type="text"
                placeholder={quantity > 1 ? `Convite ${i + 1}` : 'Nome ou identificação'}
                value={desc}
                onChange={e => setDescriptions(d => d.map((v, j) => j === i ? e.target.value : v))}
                style={{ marginBottom: '.4rem' }}
              />
            ))}
          </div>
          <button type="submit" disabled={submitting}>{submitting ? 'Gerando...' : 'Gerar Convite(s)'}</button>
          {error && <p role="alert">{error}</p>}
        </form>
        {newTokens.length > 0 && (
          <div role="status" style={{ marginTop: '1rem' }}>
            <p style={{ marginBottom: '.5rem' }}>Convite(s) gerado(s):</p>
            {newTokens.map(token => (
              <div key={token} style={{ display: 'flex', alignItems: 'center', gap: '.5rem', marginBottom: '.4rem' }}>
                <span className="token-display">{token}</span>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => copyToken(token)}
                  style={{ padding: '.15rem .5rem', fontSize: '.75rem' }}
                  title="Copiar token"
                >
                  {copiedToken === token ? '✓' : '📋'}
                </button>
              </div>
            ))}
          </div>
        )}
      </div>
      {invites.length > 0 && (
        <div className="card" style={{ padding: 0, overflow: 'hidden', marginTop: '1rem' }}>
          <div className="table-wrapper">
          <table>
            <thead>
              <tr><th>Token</th><th>Descrição</th><th>Status</th><th>Criado em</th><th>Expira em</th><th>Ação</th></tr>
            </thead>
            <tbody>
              {invites.map(invite => (
                <tr key={invite.id}>
                  <td><code style={{ fontSize: '.8rem' }}>{invite.token}</code></td>
                  <td>{invite.description ?? '—'}</td>
                  <td><span className={`badge badge-${invite.status.toLowerCase()}`}>{invite.status}</span></td>
                  <td>{new Date(invite.createdAt).toLocaleString('pt-BR')}</td>
                  <td>{new Date(invite.expiresAt).toLocaleString('pt-BR')}</td>
                  <td>
                    {invite.status === 'Pending' && (
                      <button type="button" className="btn-danger" onClick={() => handleRevoke(invite.id)} style={{ padding: '.3rem .7rem', fontSize: '.8rem' }}>Revogar</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        </div>
      )}
    </section>
  )
}

// ── Times (expõe lista via onTeamsChange para outros componentes) ─────────
function TeamsSection({ onTeamsChange }: { onTeamsChange: (teams: CS2Team[]) => void }) {
  const [teams, setTeams] = useState<CS2Team[]>([])
  const [name, setName] = useState('')
  const [logoUrl, setLogoUrl] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const loadTeams = useCallback(() => {
    getTeams().then(data => { setTeams(data); onTeamsChange(data) }).catch(() => {})
  }, [onTeamsChange])

  useEffect(() => { loadTeams() }, [loadTeams])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true); setError(null); setSuccess(null)
    try {
      await createTeam({ name, logoUrl: logoUrl || undefined })
      setSuccess('Time cadastrado com sucesso!')
      setName(''); setLogoUrl('')
      loadTeams()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao cadastrar time.')
    } finally { setSubmitting(false) }
  }

  async function handleDelete(teamId: string, teamName: string) {
    if (!confirm(`Remover o time "${teamName}"? Esta ação não pode ser desfeita.`)) return
    try {
      await apiClient.delete(`/teams/${teamId}`)
      loadTeams()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      alert(e2.response?.data?.error?.message ?? 'Erro ao remover time.')
    }
  }

  return (
    <section id="sec-teams">
      <h2>🛡️ Times</h2>
      <div className="card">
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="teamName">Nome:</label>
            <input id="teamName" type="text" value={name} onChange={e => setName(e.target.value)} required />
          </div>
          <div className="form-group">
            <label htmlFor="teamLogoUrl">URL do Logo (opcional):</label>
            <input id="teamLogoUrl" type="text" value={logoUrl} onChange={e => setLogoUrl(e.target.value)} />
          </div>
          <button type="submit" disabled={submitting}>{submitting ? 'Cadastrando...' : 'Cadastrar Time'}</button>
          {success && <p role="status">{success}</p>}
          {error && <p role="alert">{error}</p>}
        </form>
      </div>
      {teams.length > 0 && (
        <div className="card" style={{ padding: 0, overflow: 'hidden', marginTop: '1rem' }}>
          <div className="table-wrapper">
          <table>
            <thead><tr><th>Nome</th><th>Logo</th><th>Criado em</th><th>Ação</th></tr></thead>
            <tbody>
              {teams.map(team => (
                <tr key={team.id}>
                  <td>{team.name}</td>
                  <td>
                    {team.logoUrl != null && team.logoUrl !== ''
                      ? <img
                          src={team.logoUrl}
                          alt={team.name}
                          style={{ height: '2rem', objectFit: 'contain' }}
                          onError={e => { (e.currentTarget as HTMLImageElement).style.display = 'none' }}
                        />
                      : '—'}
                  </td>
                  <td>{new Date(team.createdAt).toLocaleString('pt-BR')}</td>
                  <td>
                    <button
                      type="button"
                      className="btn-danger"
                      onClick={() => handleDelete(team.id, team.name)}
                      style={{ padding: '.3rem .7rem', fontSize: '.8rem' }}
                    >
                      Remover
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        </div>
      )}
    </section>
  )
}

// ── Jogadores (recebe lista de times atualizada do TeamsSection) ──────────
function PlayersSection({ teams, users, onUsersChange }: { teams: CS2Team[]; users: User[]; onUsersChange: () => void }) {
  const [players, setPlayers] = useState<CS2Player[]>([])
  const [playersError, setPlayersError] = useState<string | null>(null)
  const [userId, setUserId] = useState('')
  const [teamId, setTeamId] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const usersWithoutTeam = users.filter(u => !u.teamId)

  function loadPlayers() {
    getPlayers()
      .then(setPlayers)
      .catch(() => setPlayersError('Erro ao carregar jogadores.'))
  }

  useEffect(() => { loadPlayers() }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await apiClient.post('/players', { userId, teamId })
      setSuccess('Jogador atribuído com sucesso!')
      setUserId(''); setTeamId('')
      loadPlayers()
      onUsersChange()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao atribuir jogador.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="sec-players">
      <h2>👤 Jogadores</h2>
      <div className="card">
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="playerUserSelect">Jogador:</label>
            {usersWithoutTeam.length === 0
              ? <p style={{ color: 'var(--text-muted)', fontSize: '.9rem' }}>Todos os usuários já estão em times.</p>
              : <select id="playerUserSelect" value={userId} onChange={e => setUserId(e.target.value)} required>
                  <option value="">Selecione um jogador</option>
                  {usersWithoutTeam.map(u => (
                    <option key={u.id} value={u.id}>{u.username}</option>
                  ))}
                </select>
            }
          </div>
          <div className="form-group">
            <label htmlFor="playerTeam">Time:</label>
            <select id="playerTeam" value={teamId} onChange={e => setTeamId(e.target.value)} required>
              <option value="">Selecione um time</option>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <button type="submit" disabled={submitting || usersWithoutTeam.length === 0}>
            {submitting ? 'Atribuindo...' : 'Atribuir a Time'}
          </button>
          {success && <p role="status">{success}</p>}
          {error && <p role="alert">{error}</p>}
        </form>
      </div>
      {playersError && <p role="alert" style={{ marginTop: '1rem' }}>{playersError}</p>}
      {players.length > 0 && (
        <div className="card" style={{ padding: 0, overflow: 'hidden', marginTop: '1rem' }}>
          <div className="table-wrapper">
            <table>
              <thead><tr><th>Nickname</th><th>Username</th><th>Time</th><th>Score Atual</th><th>Partidas</th></tr></thead>
              <tbody>
                {players.map(p => (
                  <tr key={p.id}>
                    <td>{p.nickname}</td>
                    <td>{p.username ?? '—'}</td>
                    <td>{p.teamName}</td>
                    <td>{p.playerScore}</td>
                    <td>{p.matchesCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  )
}

// ── Registrar Mapa ────────────────────────────────────────────────────────
function MapResultSection({ games }: { games: Game[] }) {
  const [gameId, setGameId] = useState('')
  const [mapNumber, setMapNumber] = useState('1')
  const [rounds, setRounds] = useState('1')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const eligibleGames = games.filter(g => g.status === 'InProgress' || g.status === 'Finished')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!gameId) return
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await createMapResult({ gameId, mapNumber: parseInt(mapNumber, 10), rounds: parseInt(rounds, 10) })
      setSuccess('Mapa registrado com sucesso!')
      setGameId(''); setMapNumber('1'); setRounds('1')
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao registrar mapa.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="sec-map-results">
      <h2>📍 Registrar Mapa</h2>
      <div className="card">
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="mapResultGameSelect">Jogo:</label>
            <select id="mapResultGameSelect" value={gameId} onChange={e => setGameId(e.target.value)} required>
              <option value="">Selecione um jogo</option>
              {eligibleGames.map(g => (
                <option key={g.id} value={g.id}>{g.teamA} vs {g.teamB} — {g.status === 'InProgress' ? 'Em andamento' : 'Finalizado'}</option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="mapNumber">Número do Mapa:</label>
            <input id="mapNumber" type="number" min="1" value={mapNumber} onChange={e => setMapNumber(e.target.value)} required />
          </div>
          <div className="form-group">
            <label htmlFor="mapRounds">Número de Rounds:</label>
            <input id="mapRounds" type="number" min="1" value={rounds} onChange={e => setRounds(e.target.value)} required />
          </div>
          <button type="submit" disabled={submitting}>{submitting ? 'Registrando...' : 'Registrar Mapa'}</button>
          {success && <p role="status">{success}</p>}
          {error && <p role="alert">{error}</p>}
        </form>
      </div>
    </section>
  )
}

// ── Estatísticas de Partida ───────────────────────────────────────────────
function MatchStatsSection({ games }: { games: Game[] }) {
  const [players, setPlayers] = useState<{ id: string; nickname: string; teamName: string }[]>([])

  // Etapa 1: selecionar jogo
  const [statsGameId, setStatsGameId] = useState('')
  // Etapa 2: mapResults carregados + seleção
  const [mapResults, setMapResults] = useState<MapResult[]>([])
  const [loadingMaps, setLoadingMaps] = useState(false)
  const [mapResultId, setMapResultId] = useState('')
  // Etapa 3: stats
  const [playerId, setPlayerId] = useState('')
  const [kills, setKills] = useState('0')
  const [deaths, setDeaths] = useState('0')
  const [assists, setAssists] = useState('0')
  const [totalDamage, setTotalDamage] = useState('0')
  const [kastPercent, setKastPercent] = useState('0')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const eligibleGames = games.filter(g => g.status === 'InProgress' || g.status === 'Finished')

  useEffect(() => {
    if (!statsGameId) { setMapResults([]); setMapResultId(''); setPlayers([]); return }
    setLoadingMaps(true); setMapResultId('')
    Promise.all([
      getMapResultsByGame(statsGameId),
      getGamePlayers(statsGameId),
    ])
      .then(([maps, gamePlayers]) => { setMapResults(maps); setPlayers(gamePlayers) })
      .catch(() => { setMapResults([]); setPlayers([]) })
      .finally(() => setLoadingMaps(false))
  }, [statsGameId])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!mapResultId || !playerId) return
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await registerMatchStats(playerId, {
        mapResultId,
        kills: parseInt(kills, 10), deaths: parseInt(deaths, 10),
        assists: parseInt(assists, 10), totalDamage: parseFloat(totalDamage),
        kastPercent: parseFloat(kastPercent),
      })
      setSuccess('Estatísticas registradas com sucesso!')
      setStatsGameId(''); setMapResults([]); setMapResultId('')
      setPlayerId(''); setKills('0'); setDeaths('0')
      setAssists('0'); setTotalDamage('0'); setKastPercent('0')
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao registrar estatísticas.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="sec-stats">
      <h2>📊 Estatísticas de Partida</h2>
      <div className="card">
        <form onSubmit={handleSubmit}>
          {/* Etapa 1: Selecionar Jogo */}
          <div className="form-group">
            <label htmlFor="statsGameSelect">Jogo:</label>
            <select id="statsGameSelect" value={statsGameId} onChange={e => setStatsGameId(e.target.value)} required>
              <option value="">Selecione um jogo</option>
              {eligibleGames.map(g => (
                <option key={g.id} value={g.id}>{g.teamA} vs {g.teamB} — {g.status === 'InProgress' ? 'Em andamento' : 'Finalizado'}</option>
              ))}
            </select>
          </div>

          {/* Etapa 2: Selecionar MapResult */}
          {statsGameId && (
            <div className="form-group">
              <label htmlFor="statsMapResultSelect">Mapa:</label>
              {loadingMaps
                ? <p style={{ color: 'var(--text-muted)' }}>Carregando mapas...</p>
                : mapResults.length === 0
                  ? <p style={{ color: 'var(--text-muted)', fontSize: '.9rem' }}>Nenhum mapa registrado para este jogo.</p>
                  : <select id="statsMapResultSelect" value={mapResultId} onChange={e => setMapResultId(e.target.value)} required>
                      <option value="">Selecione um mapa</option>
                      {mapResults.map(mr => (
                        <option key={mr.id} value={mr.id}>Mapa {mr.mapNumber} — {mr.rounds} rounds</option>
                      ))}
                    </select>
              }
            </div>
          )}

          {/* Etapa 3: Jogador e stats */}
          {mapResultId && (
            <>
              <div className="form-group">
                <label htmlFor="statsPlayerSelect">Jogador:</label>
                <select id="statsPlayerSelect" value={playerId} onChange={e => setPlayerId(e.target.value)} required>
                  <option value="">Selecione um jogador</option>
                  {players.map(p => <option key={p.id} value={p.id}>{p.nickname} - {p.teamName}</option>)}
                </select>
              </div>
              {[
                { id: 'statsKills', label: 'Kills', val: kills, set: setKills, min: '0' },
                { id: 'statsDeaths', label: 'Deaths', val: deaths, set: setDeaths, min: '0' },
                { id: 'statsAssists', label: 'Assists', val: assists, set: setAssists, min: '0' },
                { id: 'statsDamage', label: 'Dano Total', val: totalDamage, set: setTotalDamage, min: '0', step: '0.1' },
                { id: 'statsKast', label: 'KAST (%)', val: kastPercent, set: setKastPercent, min: '0', max: '100', step: '0.1' },
              ].map(f => (
                <div className="form-group" key={f.id}>
                  <label htmlFor={f.id}>{f.label}:</label>
                  <input id={f.id} type="number" min={f.min} max={f.max} step={f.step ?? '1'}
                    value={f.val} onChange={e => f.set(e.target.value)} required />
                </div>
              ))}
            </>
          )}

          <button type="submit" disabled={submitting || !mapResultId || !playerId}>
            {submitting ? 'Registrando...' : 'Registrar Estatísticas'}
          </button>
          {success && <p role="status">{success}</p>}
          {error && <p role="alert">{error}</p>}
        </form>
      </div>
    </section>
  )
}

// ── Gestão de Líderes ─────────────────────────────────────────────────────
function LeaderManagementSection({ teams, users }: { teams: CS2Team[]; users: User[] }) {
  const [assignTeamId, setAssignTeamId] = useState('')
  const [assignUserId, setAssignUserId] = useState('')
  const [assignSubmitting, setAssignSubmitting] = useState(false)
  const [assignSuccess, setAssignSuccess] = useState<string | null>(null)
  const [assignError, setAssignError] = useState<string | null>(null)

  const [removeTeamId, setRemoveTeamId] = useState('')
  const [removeSubmitting, setRemoveSubmitting] = useState(false)
  const [removeSuccess, setRemoveSuccess] = useState<string | null>(null)
  const [removeError, setRemoveError] = useState<string | null>(null)

  const [moveSourceTeamId, setMoveSourceTeamId] = useState('')
  const [moveUserId, setMoveUserId] = useState('')
  const [moveTeamId, setMoveTeamId] = useState('')
  const [moveSubmitting, setMoveSubmitting] = useState(false)
  const [moveSuccess, setMoveSuccess] = useState<string | null>(null)
  const [moveError, setMoveError] = useState<string | null>(null)

  async function handleAssignLeader(e: React.FormEvent) {
    e.preventDefault()
    setAssignSubmitting(true); setAssignSuccess(null); setAssignError(null)
    try {
      await apiClient.post(`/teams/${assignTeamId}/leader/${assignUserId}`)
      setAssignSuccess('Líder designado com sucesso!')
      setAssignTeamId(''); setAssignUserId('')
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setAssignError(e2.response?.data?.error?.message ?? 'Erro ao designar líder.')
    } finally { setAssignSubmitting(false) }
  }

  async function handleRemoveLeader(e: React.FormEvent) {
    e.preventDefault()
    setRemoveSubmitting(true); setRemoveSuccess(null); setRemoveError(null)
    try {
      await apiClient.delete(`/teams/${removeTeamId}/leader`)
      setRemoveSuccess('Líder removido com sucesso!')
      setRemoveTeamId('')
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setRemoveError(e2.response?.data?.error?.message ?? 'Erro ao remover líder.')
    } finally { setRemoveSubmitting(false) }
  }

  async function handleMoveUser(e: React.FormEvent) {
    e.preventDefault()
    setMoveSubmitting(true); setMoveSuccess(null); setMoveError(null)
    try {
      await apiClient.patch(`/users/${moveUserId}/team`, { teamId: moveTeamId || null })
      setMoveSuccess('Usuário movido com sucesso!')
      setMoveSourceTeamId(''); setMoveUserId(''); setMoveTeamId('')
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setMoveError(e2.response?.data?.error?.message ?? 'Erro ao mover usuário.')
    } finally { setMoveSubmitting(false) }
  }

  const assignEligibleUsers = assignTeamId
    ? users.filter(u => u.teamId === assignTeamId)
    : []

  const moveEligibleUsers = moveSourceTeamId
    ? users.filter(u => u.teamId === moveSourceTeamId)
    : []

  return (
    <section id="sec-leaders">
      <h2>👑 Gestão de Líderes</h2>
      <div className="card">
        <h3>Designar Líder</h3>
        <form onSubmit={handleAssignLeader}>
          <div className="form-group">
            <label htmlFor="assignLeaderTeam">Time:</label>
            <select id="assignLeaderTeam" value={assignTeamId} onChange={e => { setAssignTeamId(e.target.value); setAssignUserId('') }} required>
              <option value="">Selecione um time</option>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="assignLeaderUserId">ID do Usuário:</label>
            <select id="assignLeaderUserId" value={assignUserId} onChange={e => setAssignUserId(e.target.value)} disabled={!assignTeamId} required>
              <option value="">{assignTeamId ? 'Selecione um usuário' : 'Selecione um time primeiro'}</option>
              {assignEligibleUsers.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
            </select>
          </div>
          <button type="submit" disabled={assignSubmitting}>{assignSubmitting ? 'Designando...' : 'Designar Líder'}</button>
          {assignSuccess && <p role="status">{assignSuccess}</p>}
          {assignError && <p role="alert">{assignError}</p>}
        </form>
      </div>

      <div className="card" style={{ marginTop: '1rem' }}>
        <h3>Remover Líder</h3>
        <form onSubmit={handleRemoveLeader}>
          <div className="form-group">
            <label htmlFor="removeLeaderTeam">Time:</label>
            <select id="removeLeaderTeam" value={removeTeamId} onChange={e => setRemoveTeamId(e.target.value)} required>
              <option value="">Selecione um time</option>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <button type="submit" disabled={removeSubmitting}>{removeSubmitting ? 'Removendo...' : 'Remover Líder'}</button>
          {removeSuccess && <p role="status">{removeSuccess}</p>}
          {removeError && <p role="alert">{removeError}</p>}
        </form>
      </div>

      <div className="card" style={{ marginTop: '1rem' }}>
        <h3>Mover Usuário de Time</h3>
        <form onSubmit={handleMoveUser}>
          <div className="form-group">
            <label htmlFor="moveSourceTeam">Time de Origem:</label>
            <select id="moveSourceTeam" value={moveSourceTeamId}
              onChange={e => { setMoveSourceTeamId(e.target.value); setMoveUserId('') }} required>
              <option value="">Selecione o time de origem</option>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="moveUserId">Usuário:</label>
            <select id="moveUserId" value={moveUserId} onChange={e => setMoveUserId(e.target.value)} disabled={!moveSourceTeamId} required>
              <option value="">{moveSourceTeamId ? 'Selecione um usuário' : 'Selecione o time de origem primeiro'}</option>
              {moveEligibleUsers.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="moveUserTeam">Time Destino:</label>
            <select id="moveUserTeam" value={moveTeamId} onChange={e => setMoveTeamId(e.target.value)}>
              <option value="">Remover do time</option>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <button type="submit" disabled={moveSubmitting}>{moveSubmitting ? 'Movendo...' : 'Mover'}</button>
          {moveSuccess && <p role="status">{moveSuccess}</p>}
          {moveError && <p role="alert">{moveError}</p>}
        </form>
      </div>
    </section>
  )
}

// ── Excluir Jogo ──────────────────────────────────────────────────────────
function DeleteGameSection({ games, onDeleted }: { games: Game[]; onDeleted: () => void }) {
  const [error, setError] = useState<string | null>(null)
  const scheduledGames = games.filter(g => g.status === 'Scheduled')

  async function handleDelete(game: Game) {
    if (!confirm(`Excluir o jogo "${game.teamA} vs ${game.teamB}" agendado para ${new Date(game.scheduledAt).toLocaleString('pt-BR')}? Esta ação não pode ser desfeita e todas as apostas serão canceladas.`)) return
    setError(null)
    try {
      await apiClient.delete(`/games/${game.id}`)
      onDeleted()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao excluir jogo.')
    }
  }

  return (
    <section id="sec-delete-game">
      <h2>🗑️ Excluir Jogo</h2>
      {scheduledGames.length === 0 ? (
        <div className="card empty-card"><p>Nenhum jogo agendado para excluir.</p></div>
      ) : (
        <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
          <div className="table-wrapper">
            <table>
              <thead>
                <tr><th>Jogo</th><th>Data</th><th>Mapas</th><th>Ação</th></tr>
              </thead>
              <tbody>
                {scheduledGames.map(g => (
                  <tr key={g.id}>
                    <td>{g.teamA} vs {g.teamB}</td>
                    <td>{new Date(g.scheduledAt).toLocaleString('pt-BR')}</td>
                    <td>{g.numberOfMaps}</td>
                    <td>
                      <button
                        type="button"
                        className="btn-danger"
                        onClick={() => handleDelete(g)}
                        style={{ padding: '.3rem .7rem', fontSize: '.8rem' }}
                      >
                        Excluir
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {error && <p role="alert" style={{ padding: '1rem' }}>{error}</p>}
        </div>
      )}
    </section>
  )
}

// ── Editar Jogo ───────────────────────────────────────────────────────────
function EditGameSection({ games, onUpdated }: { games: Game[]; onUpdated: () => void }) {
  const [selectedGameId, setSelectedGameId] = useState('')
  const [teamA, setTeamA] = useState('')
  const [teamB, setTeamB] = useState('')
  const [scheduledAt, setScheduledAt] = useState('')
  const [numberOfMaps, setNumberOfMaps] = useState('1')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const scheduledGames = games.filter(g => g.status === 'Scheduled')

  function toDatetimeLocal(iso: string): string {
    const d = new Date(iso)
    const pad = (n: number) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
  }

  function handleSelectGame(id: string) {
    setSelectedGameId(id)
    setSuccess(null); setError(null)
    const game = scheduledGames.find(g => g.id === id)
    if (game) {
      setTeamA(game.teamA)
      setTeamB(game.teamB)
      setScheduledAt(toDatetimeLocal(game.scheduledAt))
      setNumberOfMaps(String(game.numberOfMaps))
    } else {
      setTeamA(''); setTeamB(''); setScheduledAt(''); setNumberOfMaps('1')
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedGameId) return
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await apiClient.patch(`/games/${selectedGameId}`, {
        teamA,
        teamB,
        scheduledAt: new Date(scheduledAt).toISOString(),
        numberOfMaps: parseInt(numberOfMaps, 10),
      })
      setSuccess('Jogo atualizado com sucesso!')
      onUpdated()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao atualizar jogo.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="sec-edit-game">
      <h2>✏️ Editar Jogo</h2>
      {scheduledGames.length === 0 ? (
        <div className="card empty-card"><p>Nenhum jogo agendado disponível para editar.</p></div>
      ) : (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label htmlFor="editGameSelect">Selecionar Jogo:</label>
              <select id="editGameSelect" value={selectedGameId} onChange={e => handleSelectGame(e.target.value)} required>
                <option value="">Selecione um jogo</option>
                {scheduledGames.map(g => (
                  <option key={g.id} value={g.id}>{g.teamA} vs {g.teamB} — {new Date(g.scheduledAt).toLocaleString('pt-BR')}</option>
                ))}
              </select>
            </div>
            {selectedGameId && (
              <>
                <div className="form-group">
                  <label htmlFor="editTeamA">Time A:</label>
                  <input id="editTeamA" type="text" value={teamA} onChange={e => setTeamA(e.target.value)} required />
                </div>
                <div className="form-group">
                  <label htmlFor="editTeamB">Time B:</label>
                  <input id="editTeamB" type="text" value={teamB} onChange={e => setTeamB(e.target.value)} required />
                </div>
                <div className="form-group">
                  <label htmlFor="editScheduledAt">Data e Hora:</label>
                  <input id="editScheduledAt" type="datetime-local" value={scheduledAt} onChange={e => setScheduledAt(e.target.value)} required />
                </div>
                <div className="form-group">
                  <label htmlFor="editNumberOfMaps">Número de Mapas:</label>
                  <input id="editNumberOfMaps" type="number" min="1" max="5" value={numberOfMaps} onChange={e => setNumberOfMaps(e.target.value)} required />
                </div>
                <button type="submit" disabled={submitting}>{submitting ? 'Salvando...' : 'Salvar alterações'}</button>
              </>
            )}
            {success && <p role="status">{success}</p>}
            {error && <p role="alert">{error}</p>}
          </form>
        </div>
      )}
    </section>
  )
}

// ── Troca Direta ──────────────────────────────────────────────────────────
function DirectSwapSection({ users, teams }: { users: User[]; teams: CS2Team[] }) {
  const [teamAId, setTeamAId] = useState('')
  const [teamBId, setTeamBId] = useState('')
  const [userAId, setUserAId] = useState('')
  const [userBId, setUserBId] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const usersTeamA = teamAId ? users.filter(u => u.teamId === teamAId) : []
  const usersTeamB = teamBId ? users.filter(u => u.teamId === teamBId) : []

  async function handleSwap(e: React.FormEvent) {
    e.preventDefault()
    if (userAId === userBId) { setError('Os dois usuários devem ser diferentes.'); return }
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await apiClient.post('/trades/direct', { userAId, userBId })
      setSuccess('Troca realizada com sucesso!')
      setTeamAId(''); setTeamBId(''); setUserAId(''); setUserBId('')
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao realizar troca.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="sec-swap">
      <h2>🔄 Troca Direta</h2>
      <div className="card">
        <form onSubmit={handleSwap}>
          {/* Grupo A */}
          <div className="form-group">
            <label htmlFor="swapTeamA">Time A:</label>
            <select id="swapTeamA" value={teamAId}
              onChange={e => { setTeamAId(e.target.value); setUserAId('') }} required>
              <option value="">Selecione o Time A</option>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="swapUserAId">Jogador A:</label>
            <select id="swapUserAId" value={userAId}
              onChange={e => setUserAId(e.target.value)}
              disabled={!teamAId} required>
              <option value="">{teamAId ? 'Selecione o Jogador A' : 'Selecione o Time A primeiro'}</option>
              {usersTeamA.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
            </select>
          </div>

          {/* Grupo B */}
          <div className="form-group">
            <label htmlFor="swapTeamB">Time B:</label>
            <select id="swapTeamB" value={teamBId}
              onChange={e => { setTeamBId(e.target.value); setUserBId('') }} required>
              <option value="">Selecione o Time B</option>
              {teams.filter(t => t.id !== teamAId).map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="swapUserBId">Jogador B:</label>
            <select id="swapUserBId" value={userBId}
              onChange={e => setUserBId(e.target.value)}
              disabled={!teamBId} required>
              <option value="">{teamBId ? 'Selecione o Jogador B' : 'Selecione o Time B primeiro'}</option>
              {usersTeamB.map(u => <option key={u.id} value={u.id}>{u.username}</option>)}
            </select>
          </div>

          <button type="submit" disabled={submitting}>{submitting ? 'Trocando...' : 'Trocar'}</button>
          {success && <p role="status">{success}</p>}
          {error && <p role="alert">{error}</p>}
        </form>
      </div>
    </section>
  )
}

// ── Página principal ──────────────────────────────────────────────────────
export default function AdminPage() {
  const [user, setUser] = useState<User | null>(null)
  const [users, setUsers] = useState<User[]>([])
  const [games, setGames] = useState<Game[]>([])
  const [teams, setTeams] = useState<CS2Team[]>([])
  const [loading, setLoading] = useState(true)
  const [accessDenied, setAccessDenied] = useState(false)

  function loadGames() {
    apiClient.get<Game[]>('/games').then(res => setGames(res.data)).catch(() => {})
  }

  function loadUsers() {
    apiClient.get<User[]>('/users').then(res => setUsers(res.data)).catch(() => {})
  }

  function loadTeams() {
    getTeams().then(data => setTeams(data)).catch(() => {})
  }

  useEffect(() => {
    apiClient.get<User>('/users/me')
      .then(res => {
        if (!res.data.isAdmin) { setAccessDenied(true) }
        else { setUser(res.data); loadGames(); loadUsers(); loadTeams() }
      })
      .catch(() => setAccessDenied(true))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="page"><div className="card empty-card"><p>Carregando...</p></div></div>
  if (accessDenied || !user) return <div className="page"><div className="card empty-card"><p role="alert">Acesso negado.</p></div></div>

  const isMasterAdmin = user.isMasterAdmin ?? false

  return (
    <div className="page page-admin">
      <h1>⚙️ Administração</h1>
      <AdminNav isMasterAdmin={isMasterAdmin} />
      <UsersSection currentUser={user} teams={teams} />
      {isMasterAdmin && <AdminManagementSection users={users} currentUser={user} onUsersChange={loadUsers} />}
      <CreateGameForm teams={teams} onCreated={loadGames} />
      <DeleteGameSection games={games} onDeleted={loadGames} />
      <EditGameSection games={games} onUpdated={loadGames} />
      <StartGameSection games={games} onStarted={loadGames} />
      <RegisterResultSection games={games} />
      <InvitesSection />
      <TeamsSection onTeamsChange={setTeams} />
      <PlayersSection teams={teams} users={users} onUsersChange={loadUsers} />
      <MapResultSection games={games} />
      <MatchStatsSection games={games} />
      <LeaderManagementSection teams={teams} users={users} />
      <DirectSwapSection users={users} teams={teams} />
    </div>
  )
}

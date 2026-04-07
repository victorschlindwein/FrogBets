import { useEffect, useState, useCallback } from 'react'
import apiClient from '../api/client'
import { getTeams, createTeam, CS2Team, getPlayers, createPlayer, CS2Player, registerMatchStats } from '../api/players'

interface User { id: string; username: string; isAdmin: boolean }
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
const NAV_ITEMS = [
  { id: 'sec-users',       label: '👥 Usuários' },
  { id: 'sec-admins',      label: '🔐 Admins' },
  { id: 'sec-games',       label: '🎮 Cadastrar Jogo' },
  { id: 'sec-start',       label: '▶️ Iniciar Jogo' },
  { id: 'sec-result',      label: '🏁 Registrar Resultado' },
  { id: 'sec-invites',     label: '🎟️ Convites' },
  { id: 'sec-teams',       label: '🛡️ Times' },
  { id: 'sec-players',     label: '👤 Jogadores' },
  { id: 'sec-stats',       label: '📊 Estatísticas' },
  { id: 'sec-leaders',     label: '👑 Gestão de Líderes' },
  { id: 'sec-swap',        label: '🔄 Troca Direta' },
]

function AdminNav() {
  return (
    <nav className="admin-index card">
      <strong>Índice</strong>
      <ul>
        {NAV_ITEMS.map(item => (
          <li key={item.id}>
            <a href={`#${item.id}`}>{item.label}</a>
          </li>
        ))}
      </ul>
    </nav>
  )
}

// ── Lista de Usuários ─────────────────────────────────────────────────────
function UsersSection({ currentUser }: { currentUser: User }) {
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

  return (
    <section id="sec-users">
      <h2>👥 Usuários</h2>
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        <div className="table-wrapper">
          <table>
            <thead>
              <tr><th>Username</th><th>ID</th><th>Admin</th><th>Líder</th><th>Time ID</th></tr>
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
                  <td>{(u as unknown as { isTeamLeader: boolean }).isTeamLeader ? '✅' : '—'}</td>
                  <td>
                    {(u as unknown as { teamId: string | null }).teamId
                      ? <code style={{ fontSize: '.75rem', color: 'var(--text-muted)' }}>{(u as unknown as { teamId: string }).teamId}</code>
                      : '—'}
                  </td>
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
  const [expiresAt, setExpiresAt] = useState('')
  const [description, setDescription] = useState('')
  const [newToken, setNewToken] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  function loadInvites() {
    apiClient.get<Invite[]>('/invites').then(res => setInvites(res.data)).catch(() => {})
  }
  useEffect(() => { loadInvites() }, [])

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true); setError(null); setNewToken(null)
    try {
      const res = await apiClient.post<Invite>('/invites', {
        expiresAt: new Date(expiresAt).toISOString(),
        description: description || null,
      })
      setNewToken(res.data.token)
      setExpiresAt(''); setDescription('')
      loadInvites()
    } catch { setError('Erro ao gerar convite.') }
    finally { setSubmitting(false) }
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
            <label htmlFor="inviteExpiresAt">Expira em:</label>
            <input id="inviteExpiresAt" type="datetime-local" value={expiresAt} onChange={e => setExpiresAt(e.target.value)} required />
          </div>
          <div className="form-group">
            <label htmlFor="inviteDescription">Destinatário (opcional):</label>
            <input id="inviteDescription" type="text" value={description} onChange={e => setDescription(e.target.value)} />
          </div>
          <button type="submit" disabled={submitting}>{submitting ? 'Gerando...' : 'Gerar Convite'}</button>
          {error && <p role="alert">{error}</p>}
        </form>
        {newToken && (
          <p role="status" style={{ marginTop: '1rem' }}>
            Convite gerado: <span className="token-display">{newToken}</span>
          </p>
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
            <thead><tr><th>Nome</th><th>Logo</th><th>Criado em</th></tr></thead>
            <tbody>
              {teams.map(team => (
                <tr key={team.id}>
                  <td>{team.name}</td>
                  <td>
                    {team.logoUrl
                      ? <img
                          src={team.logoUrl}
                          alt={team.name}
                          style={{ height: '2rem', objectFit: 'contain' }}
                          onError={e => { (e.currentTarget as HTMLImageElement).style.display = 'none' }}
                        />
                      : '—'}
                  </td>
                  <td>{new Date(team.createdAt).toLocaleString('pt-BR')}</td>
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
function PlayersSection({ teams }: { teams: CS2Team[] }) {
  const [players, setPlayers] = useState<CS2Player[]>([])
  const [nickname, setNickname] = useState('')
  const [realName, setRealName] = useState('')
  const [teamId, setTeamId] = useState('')
  const [photoUrl, setPhotoUrl] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  function loadPlayers() {
    getPlayers().then(setPlayers).catch(() => {})
  }

  useEffect(() => { loadPlayers() }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true); setError(null); setSuccess(null)
    try {
      await createPlayer({ nickname, realName: realName || undefined, teamId, photoUrl: photoUrl || undefined })
      setSuccess('Jogador cadastrado com sucesso!')
      setNickname(''); setRealName(''); setTeamId(''); setPhotoUrl('')
      loadPlayers()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao cadastrar jogador.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="sec-players">
      <h2>👤 Jogadores</h2>
      <div className="card">
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="playerNickname">Nickname:</label>
            <input id="playerNickname" type="text" value={nickname} onChange={e => setNickname(e.target.value)} required />
          </div>
          <div className="form-group">
            <label htmlFor="playerRealName">Nome Real (opcional):</label>
            <input id="playerRealName" type="text" value={realName} onChange={e => setRealName(e.target.value)} />
          </div>
          <div className="form-group">
            <label htmlFor="playerTeamId">Time:</label>
            <select id="playerTeamId" value={teamId} onChange={e => setTeamId(e.target.value)} required>
              <option value="">Selecione um time</option>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="playerPhotoUrl">URL da Foto (opcional):</label>
            <input id="playerPhotoUrl" type="text" value={photoUrl} onChange={e => setPhotoUrl(e.target.value)} />
          </div>
          <button type="submit" disabled={submitting}>{submitting ? 'Cadastrando...' : 'Cadastrar Jogador'}</button>
          {success && <p role="status">{success}</p>}
          {error && <p role="alert">{error}</p>}
        </form>
      </div>
      {players.length > 0 && (
        <div className="card" style={{ padding: 0, overflow: 'hidden', marginTop: '1rem' }}>
          <div className="table-wrapper">
          <table>
            <thead><tr><th>Nickname</th><th>Time</th><th>Score Atual</th><th>Partidas</th></tr></thead>
            <tbody>
              {players.map(p => (
                <tr key={p.id}>
                  <td>{p.nickname}</td>
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

// ── Estatísticas de Partida ───────────────────────────────────────────────
function MatchStatsSection() {
  const [games, setGames] = useState<Game[]>([])
  const [players, setPlayers] = useState<CS2Player[]>([])
  const [gameId, setGameId] = useState('')
  const [playerId, setPlayerId] = useState('')
  const [kills, setKills] = useState('0')
  const [deaths, setDeaths] = useState('0')
  const [assists, setAssists] = useState('0')
  const [totalDamage, setTotalDamage] = useState('0')
  const [rounds, setRounds] = useState('1')
  const [kastPercent, setKastPercent] = useState('0')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    apiClient.get<Game[]>('/games').then(res => setGames(res.data)).catch(() => {})
    getPlayers().then(setPlayers).catch(() => {})
  }, [])

  const eligibleGames = games.filter(g => g.status === 'InProgress' || g.status === 'Finished')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!gameId || !playerId) return
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await registerMatchStats(playerId, {
        gameId,
        kills: parseInt(kills, 10), deaths: parseInt(deaths, 10),
        assists: parseInt(assists, 10), totalDamage: parseFloat(totalDamage),
        rounds: parseInt(rounds, 10), kastPercent: parseFloat(kastPercent),
      })
      setSuccess('Estatísticas registradas com sucesso!')
      setGameId(''); setPlayerId(''); setKills('0'); setDeaths('0')
      setAssists('0'); setTotalDamage('0'); setRounds('1'); setKastPercent('0')
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
          <div className="form-group">
            <label htmlFor="statsGameSelect">Jogo:</label>
            <select id="statsGameSelect" value={gameId} onChange={e => setGameId(e.target.value)} required>
              <option value="">Selecione um jogo</option>
              {eligibleGames.map(g => (
                <option key={g.id} value={g.id}>{g.teamA} vs {g.teamB} — {g.status === 'InProgress' ? 'Em andamento' : 'Finalizado'}</option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="statsPlayerSelect">Jogador:</label>
            <select id="statsPlayerSelect" value={playerId} onChange={e => setPlayerId(e.target.value)} required>
              <option value="">Selecione um jogador</option>
              {players.map(p => <option key={p.id} value={p.id}>{p.nickname} — {p.teamName}</option>)}
            </select>
          </div>
          {[
            { id: 'statsKills', label: 'Kills', val: kills, set: setKills, min: '0' },
            { id: 'statsDeaths', label: 'Deaths', val: deaths, set: setDeaths, min: '0' },
            { id: 'statsAssists', label: 'Assists', val: assists, set: setAssists, min: '0' },
            { id: 'statsDamage', label: 'Dano Total', val: totalDamage, set: setTotalDamage, min: '0', step: '0.1' },
            { id: 'statsRounds', label: 'Rounds', val: rounds, set: setRounds, min: '1' },
            { id: 'statsKast', label: 'KAST (%)', val: kastPercent, set: setKastPercent, min: '0', max: '100', step: '0.1' },
          ].map(f => (
            <div className="form-group" key={f.id}>
              <label htmlFor={f.id}>{f.label}:</label>
              <input id={f.id} type="number" min={f.min} max={f.max} step={f.step ?? '1'}
                value={f.val} onChange={e => f.set(e.target.value)} required />
            </div>
          ))}
          <button type="submit" disabled={submitting}>{submitting ? 'Registrando...' : 'Registrar Estatísticas'}</button>
          {success && <p role="status">{success}</p>}
          {error && <p role="alert">{error}</p>}
        </form>
      </div>
    </section>
  )
}

// ── Gestão de Líderes ─────────────────────────────────────────────────────
function LeaderManagementSection({ teams }: { teams: CS2Team[] }) {
  const [assignTeamId, setAssignTeamId] = useState('')
  const [assignUserId, setAssignUserId] = useState('')
  const [assignSubmitting, setAssignSubmitting] = useState(false)
  const [assignSuccess, setAssignSuccess] = useState<string | null>(null)
  const [assignError, setAssignError] = useState<string | null>(null)

  const [removeTeamId, setRemoveTeamId] = useState('')
  const [removeSubmitting, setRemoveSubmitting] = useState(false)
  const [removeSuccess, setRemoveSuccess] = useState<string | null>(null)
  const [removeError, setRemoveError] = useState<string | null>(null)

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
      setMoveUserId(''); setMoveTeamId('')
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setMoveError(e2.response?.data?.error?.message ?? 'Erro ao mover usuário.')
    } finally { setMoveSubmitting(false) }
  }

  return (
    <section id="sec-leaders">
      <h2>👑 Gestão de Líderes</h2>
      <div className="card">
        <h3>Designar Líder</h3>
        <form onSubmit={handleAssignLeader}>
          <div className="form-group">
            <label htmlFor="assignLeaderTeam">Time:</label>
            <select id="assignLeaderTeam" value={assignTeamId} onChange={e => setAssignTeamId(e.target.value)} required>
              <option value="">Selecione um time</option>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="assignLeaderUserId">ID do Usuário:</label>
            <input id="assignLeaderUserId" type="text" value={assignUserId} onChange={e => setAssignUserId(e.target.value)} placeholder="UUID do usuário" required />
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
            <label htmlFor="moveUserId">ID do Usuário:</label>
            <input id="moveUserId" type="text" value={moveUserId} onChange={e => setMoveUserId(e.target.value)} placeholder="UUID do usuário" required />
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

// ── Troca Direta ──────────────────────────────────────────────────────────
function DirectSwapSection() {
  const [userAId, setUserAId] = useState('')
  const [userBId, setUserBId] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function handleSwap(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await apiClient.post('/trades/direct', { userAId, userBId })
      setSuccess('Troca realizada com sucesso!')
      setUserAId(''); setUserBId('')
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
          <div className="form-group">
            <label htmlFor="swapUserAId">ID do Usuário A:</label>
            <input id="swapUserAId" type="text" value={userAId} onChange={e => setUserAId(e.target.value)} placeholder="UUID do usuário A" required />
          </div>
          <div className="form-group">
            <label htmlFor="swapUserBId">ID do Usuário B:</label>
            <input id="swapUserBId" type="text" value={userBId} onChange={e => setUserBId(e.target.value)} placeholder="UUID do usuário B" required />
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

  useEffect(() => {
    apiClient.get<User>('/users/me')
      .then(res => {
        if (!res.data.isAdmin) { setAccessDenied(true) }
        else { setUser(res.data); loadGames(); loadUsers() }
      })
      .catch(() => setAccessDenied(true))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="page"><div className="card empty-card"><p>Carregando...</p></div></div>
  if (accessDenied || !user) return <div className="page"><div className="card empty-card"><p role="alert">Acesso negado.</p></div></div>

  return (
    <div className="page page-admin">
      <h1>⚙️ Administração</h1>
      <AdminNav />
      <UsersSection currentUser={user} />
      <AdminManagementSection users={users} currentUser={user} onUsersChange={loadUsers} />
      <CreateGameForm teams={teams} onCreated={loadGames} />
      <StartGameSection games={games} onStarted={loadGames} />
      <RegisterResultSection games={games} />
      <InvitesSection />
      <TeamsSection onTeamsChange={setTeams} />
      <PlayersSection teams={teams} />
      <MatchStatsSection />
      <LeaderManagementSection teams={teams} />
      <DirectSwapSection />
    </div>
  )
}

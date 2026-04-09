import { useEffect, useState, useCallback } from 'react'
import apiClient from '../api/client'
import {
  getTeams, CS2Team,
  getGamePlayers, registerMatchStats,
  createMapResult, getMapResultsByGame, MapResult,
} from '../api/players'

interface Market { id: string; type: string; mapNumber: number | null; status: string; winningOption?: string | null }
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

const STEPS = [
  { id: 'step-start',   label: '1. Iniciar Jogo',           emoji: '▶️' },
  { id: 'step-map',     label: '2. Registrar Mapa',         emoji: '📍' },
  { id: 'step-stats',   label: '3. Estatísticas de Jogador', emoji: '📊' },
  { id: 'step-result',  label: '4. Registrar Resultado',    emoji: '🏁' },
]

function StepNav() {
  return (
    <nav className="admin-index card">
      <strong>Fluxo de Partida</strong>
      <p style={{ fontSize: '.8rem', color: 'var(--text-muted)', margin: '.25rem 0 .75rem' }}>
        Execute as etapas nesta ordem para cada partida.
      </p>
      <ul>
        {STEPS.map(s => (
          <li key={s.id}>
            <a href={`#${s.id}`}>{s.emoji} {s.label}</a>
          </li>
        ))}
      </ul>
    </nav>
  )
}

// ── 1. Iniciar Jogo ───────────────────────────────────────────────────────
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
      setSuccess('Jogo iniciado! Os mercados foram fechados para novas apostas.')
      setSelectedGameId('')
      onStarted()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao iniciar jogo.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="step-start">
      <h2>▶️ Etapa 1 — Iniciar Jogo</h2>
      <p style={{ color: 'var(--text-muted)', fontSize: '.9rem', marginBottom: '1rem' }}>
        Inicia o jogo e fecha todos os mercados para novas apostas. Faça isso quando a partida começar.
      </p>
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
                  <option key={g.id} value={g.id}>
                    {g.teamA} vs {g.teamB} — {new Date(g.scheduledAt).toLocaleString('pt-BR')} — Bo{g.numberOfMaps}
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
        </div>
      )}
    </section>
  )
}

// ── 2. Registrar Mapa ─────────────────────────────────────────────────────
function MapResultSection({ games, onMapRegistered }: { games: Game[]; onMapRegistered: () => void }) {
  const [gameId, setGameId] = useState('')
  const [mapNumber, setMapNumber] = useState('1')
  const [rounds, setRounds] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const eligibleGames = games.filter(g => g.status === 'InProgress' || g.status === 'Finished')
  const selectedGame = eligibleGames.find(g => g.id === gameId)

  function handleSelectGame(id: string) {
    setGameId(id)
    setMapNumber('1')
    setRounds('')
    setSuccess(null); setError(null)
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!gameId) return
    setSubmitting(true); setSuccess(null); setError(null)
    try {
      await createMapResult({ gameId, mapNumber: parseInt(mapNumber, 10), rounds: parseInt(rounds, 10) })
      setSuccess(`Mapa ${mapNumber} registrado com sucesso!`)
      setMapNumber('1'); setRounds('')
      onMapRegistered()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao registrar mapa.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="step-map">
      <h2>📍 Etapa 2 — Registrar Mapa</h2>
      <p style={{ color: 'var(--text-muted)', fontSize: '.9rem', marginBottom: '1rem' }}>
        Registre cada mapa jogado com o número de rounds. Necessário antes de lançar estatísticas de jogadores.
      </p>
      {eligibleGames.length === 0 ? (
        <div className="card empty-card"><p>Nenhum jogo em andamento. Inicie um jogo primeiro (Etapa 1).</p></div>
      ) : (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label htmlFor="mapGameSelect">Jogo:</label>
              <select id="mapGameSelect" value={gameId} onChange={e => handleSelectGame(e.target.value)} required>
                <option value="">Selecione um jogo</option>
                {eligibleGames.map(g => (
                  <option key={g.id} value={g.id}>
                    {g.teamA} vs {g.teamB} — {g.status === 'InProgress' ? 'Em andamento' : 'Finalizado'} — Bo{g.numberOfMaps}
                  </option>
                ))}
              </select>
            </div>
            {selectedGame && (
              <>
                <div className="form-group">
                  <label htmlFor="mapNumber">Número do Mapa:</label>
                  <input
                    id="mapNumber" type="number"
                    min="1" max={selectedGame.numberOfMaps}
                    value={mapNumber}
                    onChange={e => setMapNumber(e.target.value)}
                    required
                  />
                  <small style={{ color: 'var(--text-muted)' }}>
                    Bo{selectedGame.numberOfMaps} — mapas de 1 a {selectedGame.numberOfMaps}
                  </small>
                </div>
                <div className="form-group">
                  <label htmlFor="mapRounds">Número de Rounds:</label>
                  <input
                    id="mapRounds" type="number" min="1"
                    value={rounds}
                    onChange={e => setRounds(e.target.value)}
                    placeholder="Ex: 24"
                    required
                  />
                </div>
              </>
            )}
            <button type="submit" disabled={submitting || !gameId}>
              {submitting ? 'Registrando...' : 'Registrar Mapa'}
            </button>
            {success && <p role="status">{success}</p>}
            {error && <p role="alert">{error}</p>}
          </form>
        </div>
      )}
    </section>
  )
}

// ── 3. Estatísticas de Jogador ────────────────────────────────────────────
function MatchStatsSection({ games }: { games: Game[] }) {
  const [statsGameId, setStatsGameId] = useState('')
  const [mapResults, setMapResults] = useState<MapResult[]>([])
  const [players, setPlayers] = useState<{ id: string; username: string; teamName: string }[]>([])
  const [loadingMaps, setLoadingMaps] = useState(false)
  const [mapResultId, setMapResultId] = useState('')
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
    Promise.allSettled([
      getMapResultsByGame(statsGameId),
      getGamePlayers(statsGameId),
    ]).then(([mapsResult, playersResult]) => {
      setMapResults(mapsResult.status === 'fulfilled' ? mapsResult.value : [])
      setPlayers(playersResult.status === 'fulfilled' ? playersResult.value : [])
    }).finally(() => setLoadingMaps(false))
  }, [statsGameId])

  function resetForm() {
    setPlayerId(''); setKills('0'); setDeaths('0')
    setAssists('0'); setTotalDamage('0'); setKastPercent('0')
  }

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
      const playerName = players.find(p => p.id === playerId)?.username ?? ''
      const mapLabel = mapResults.find(m => m.id === mapResultId)
      setSuccess(`Stats de ${playerName} no Mapa ${mapLabel?.mapNumber} registradas!`)
      resetForm()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao registrar estatísticas.')
    } finally { setSubmitting(false) }
  }

  return (
    <section id="step-stats">
      <h2>📊 Etapa 3 — Estatísticas de Jogador</h2>
      <p style={{ color: 'var(--text-muted)', fontSize: '.9rem', marginBottom: '1rem' }}>
        Lance as stats de cada jogador por mapa. Repita para todos os jogadores dos dois times.
        Requer que o mapa já tenha sido registrado na Etapa 2.
      </p>
      {eligibleGames.length === 0 ? (
        <div className="card empty-card"><p>Nenhum jogo em andamento. Inicie um jogo primeiro (Etapa 1).</p></div>
      ) : (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label htmlFor="statsGameSelect">Jogo:</label>
              <select id="statsGameSelect" value={statsGameId} onChange={e => setStatsGameId(e.target.value)} required>
                <option value="">Selecione um jogo</option>
                {eligibleGames.map(g => (
                  <option key={g.id} value={g.id}>
                    {g.teamA} vs {g.teamB} — {g.status === 'InProgress' ? 'Em andamento' : 'Finalizado'}
                  </option>
                ))}
              </select>
            </div>

            {statsGameId && (
              <div className="form-group">
                <label htmlFor="statsMapSelect">Mapa:</label>
                {loadingMaps
                  ? <p style={{ color: 'var(--text-muted)' }}>Carregando mapas...</p>
                  : mapResults.length === 0
                    ? <p style={{ color: 'var(--danger)', fontSize: '.9rem' }}>
                        Nenhum mapa registrado — registre o mapa na Etapa 2 primeiro.
                      </p>
                    : <select id="statsMapSelect" value={mapResultId} onChange={e => setMapResultId(e.target.value)} required>
                        <option value="">Selecione um mapa</option>
                        {mapResults.map(mr => (
                          <option key={mr.id} value={mr.id}>Mapa {mr.mapNumber} — {mr.rounds} rounds</option>
                        ))}
                      </select>
                }
              </div>
            )}

            {mapResultId && (
              <>
                <div className="form-group">
                  <label htmlFor="statsPlayerSelect">Jogador:</label>
                  {players.length === 0
                    ? <p style={{ color: 'var(--text-muted)', fontSize: '.9rem' }}>Nenhum jogador encontrado para este jogo.</p>
                    : <select id="statsPlayerSelect" value={playerId} onChange={e => setPlayerId(e.target.value)} required>
                        <option value="">Selecione um jogador</option>
                        {players.map(p => (
                          <option key={p.id} value={p.id}>{p.username} — {p.teamName}</option>
                        ))}
                      </select>
                  }
                </div>
                {[
                  { id: 'statsKills',   label: 'Kills',       val: kills,       set: setKills,       min: '0' },
                  { id: 'statsDeaths',  label: 'Deaths',      val: deaths,      set: setDeaths,      min: '0' },
                  { id: 'statsAssists', label: 'Assists',     val: assists,     set: setAssists,     min: '0' },
                  { id: 'statsDamage',  label: 'Dano Total',  val: totalDamage, set: setTotalDamage, min: '0', step: '0.1' },
                  { id: 'statsKast',    label: 'KAST (%)',    val: kastPercent, set: setKastPercent, min: '0', max: '100', step: '0.1' },
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
      )}
    </section>
  )
}

// ── 4. Registrar Resultado ────────────────────────────────────────────────
function RegisterResultSection({ games, onSettled }: { games: Game[]; onSettled: () => void }) {
  const [selectedGameId, setSelectedGameId] = useState('')
  const [markets, setMarkets] = useState<Market[]>([])
  const [gamePlayers, setGamePlayers] = useState<{ id: string; username: string; teamName: string }[]>([])
  const [results, setResults] = useState<Record<string, string>>({})
  const [loadingMarkets, setLoadingMarkets] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const activeGames = games.filter(g => g.status === 'InProgress')

  const PLAYER_MARKETS = ['TopKills', 'MostDeaths', 'MostUtilityDamage']
  const MARKET_ORDER: Record<string, number> = {
    MapWinner: 0, SeriesWinner: 1, TopKills: 2, MostDeaths: 3, MostUtilityDamage: 4,
  }

  function sortMarkets(ms: Market[]): Market[] {
    return [...ms].sort((a, b) => {
      const typeDiff = (MARKET_ORDER[a.type] ?? 99) - (MARKET_ORDER[b.type] ?? 99)
      if (typeDiff !== 0) return typeDiff
      return (a.mapNumber ?? 0) - (b.mapNumber ?? 0)
    })
  }

  useEffect(() => {
    if (!selectedGameId) { setMarkets([]); setResults({}); setGamePlayers([]); return }
    setLoadingMarkets(true)
    Promise.allSettled([
      apiClient.get<Game>(`/games/${selectedGameId}`),
      getGamePlayers(selectedGameId),
    ]).then(([gameRes, playersRes]) => {
      if (gameRes.status === 'fulfilled') {
        const closed = sortMarkets((gameRes.value.data.markets ?? []).filter(m => m.status === 'Closed'))
        setMarkets(closed)
        setResults(Object.fromEntries(closed.map(m => [m.id, ''])))
      }
      setGamePlayers(playersRes.status === 'fulfilled' ? playersRes.value : [])
    }).finally(() => setLoadingMarkets(false))
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
      setSuccess(`${pending.length} resultado(s) registrado(s)! Apostas liquidadas.`)
      setSelectedGameId(''); setMarkets([]); setResults({}); setGamePlayers([])
      onSettled()
    } catch (err: unknown) {
      const e2 = err as { response?: { data?: { error?: { message?: string } } } }
      setError(e2.response?.data?.error?.message ?? 'Erro ao registrar resultado.')
    } finally { setSubmitting(false) }
  }

  const selectedGame = activeGames.find(g => g.id === selectedGameId)

  return (
    <section id="step-result">
      <h2>🏁 Etapa 4 — Registrar Resultado</h2>
      <p style={{ color: 'var(--text-muted)', fontSize: '.9rem', marginBottom: '1rem' }}>
        Define o vencedor de cada mercado e liquida as apostas automaticamente.
        Só aparecem mercados fechados (apostas encerradas).
      </p>
      {activeGames.length === 0 ? (
        <div className="card empty-card"><p>Nenhum jogo em andamento. Inicie um jogo primeiro (Etapa 1).</p></div>
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
              <p style={{ color: 'var(--text-muted)' }}>Nenhum mercado fechado para este jogo.</p>
            )}

            {!loadingMarkets && markets.length > 0 && (
              <>
                <p style={{ fontSize: '.85rem', color: 'var(--text-muted)', marginBottom: '.75rem' }}>
                  Preencha o vencedor de cada mercado. Deixe em branco para pular.
                </p>
                {markets.map(m => (
                  <div className="form-group" key={m.id}>
                    <label>{marketLabel(m)}</label>
                    {PLAYER_MARKETS.includes(m.type) ? (
                      <select
                        value={results[m.id] ?? ''}
                        onChange={e => setResults(r => ({ ...r, [m.id]: e.target.value }))}
                        disabled={gamePlayers.length === 0}
                      >
                        <option value="">— pular —</option>
                        {gamePlayers.map(p => (
                          <option key={p.id} value={p.username}>{p.username} ({p.teamName})</option>
                        ))}
                      </select>
                    ) : (
                      <select value={results[m.id] ?? ''} onChange={e => setResults(r => ({ ...r, [m.id]: e.target.value }))}>
                        <option value="">— pular —</option>
                        <option value={selectedGame?.teamA}>{selectedGame?.teamA}</option>
                        <option value={selectedGame?.teamB}>{selectedGame?.teamB}</option>
                      </select>
                    )}
                  </div>
                ))}
                {gamePlayers.length === 0 && (
                  <p style={{ fontSize: '.8rem', color: 'var(--text-muted)' }}>
                    Nenhum jogador encontrado — mercados de jogador desabilitados.
                  </p>
                )}
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

// ── Página principal ──────────────────────────────────────────────────────
export default function GameManagementPage() {
  const [games, setGames] = useState<Game[]>([])
  const [_teams, setTeams] = useState<CS2Team[]>([])
  const [loading, setLoading] = useState(true)
  const [accessDenied, setAccessDenied] = useState(false)

  const loadGames = useCallback(() => {
    apiClient.get<Game[]>('/games').then(res => setGames(res.data)).catch(() => {})
  }, [])

  useEffect(() => {
    apiClient.get('/users/me')
      .then(res => {
        if (!(res.data as { isAdmin: boolean }).isAdmin) { setAccessDenied(true); return }
        loadGames()
        getTeams().then(setTeams).catch(() => {})
      })
      .catch(() => setAccessDenied(true))
      .finally(() => setLoading(false))
  }, [loadGames])

  if (loading) return <div className="page"><div className="card empty-card"><p>Carregando...</p></div></div>
  if (accessDenied) return <div className="page"><div className="card empty-card"><p role="alert">Acesso negado.</p></div></div>

  const inProgress = games.filter(g => g.status === 'InProgress')
  const scheduled  = games.filter(g => g.status === 'Scheduled')
  const finished   = games.filter(g => g.status === 'Finished')

  return (
    <div className="page page-admin">
      <h1>🎮 Gestão de Partidas</h1>

      {/* Painel de status rápido */}
      <div className="card" style={{ display: 'flex', gap: '2rem', flexWrap: 'wrap', marginBottom: '1.5rem' }}>
        <span>📅 Agendados: <strong>{scheduled.length}</strong></span>
        <span>⚡ Em andamento: <strong>{inProgress.length}</strong></span>
        <span>✅ Finalizados: <strong>{finished.length}</strong></span>
      </div>

      <StepNav />
      <StartGameSection   games={games} onStarted={loadGames} />
      <MapResultSection   games={games} onMapRegistered={loadGames} />
      <MatchStatsSection  games={games} />
      <RegisterResultSection games={games} onSettled={loadGames} />
    </div>
  )
}

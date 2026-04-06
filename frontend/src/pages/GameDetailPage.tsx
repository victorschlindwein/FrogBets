import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import apiClient from '../api/client'

interface Market {
  id: string
  type: string
  mapNumber: number | null
  status: 'Open' | 'Closed' | 'Settled' | 'Voided'
}

interface Game {
  id: string
  teamA: string
  teamB: string
  scheduledAt: string
  numberOfMaps: number
  status: 'Scheduled' | 'InProgress' | 'Finished'
  markets: Market[]
}

const MARKET_TYPE_LABELS: Record<string, string> = {
  MapWinner: 'Vencedor do Mapa',
  SeriesWinner: 'Vencedor da Série',
  TopKills: 'Top Kills',
  MostDeaths: 'Mais Mortes',
  MostUtilityDamage: 'Maior Dano por Utilitários',
}

const TEAM_MARKETS = ['MapWinner', 'SeriesWinner']

function marketLabel(market: Market): string {
  const type = MARKET_TYPE_LABELS[market.type] ?? market.type
  return market.mapNumber != null ? `${type} — Mapa ${market.mapNumber}` : type
}

function BetForm({ market, game }: { market: Market; game: Game }) {
  const [option, setOption] = useState('')
  const [amount, setAmount] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [success, setSuccess] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const isTeamMarket = TEAM_MARKETS.includes(market.type)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    setSuccess(false)
    try {
      await apiClient.post('/bets', { marketId: market.id, creatorOption: option, amount: parseFloat(amount) })
      setSuccess(true)
      setOption('')
      setAmount('')
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: { message?: string } } } }
      setError(axiosErr.response?.data?.error?.message ?? 'Erro ao criar aposta.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} style={{ marginTop: '.75rem', display: 'flex', gap: '.75rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
      <div className="form-group" style={{ marginBottom: 0, minWidth: '160px' }}>
        <label htmlFor={`option-${market.id}`}>Opção</label>
        {isTeamMarket ? (
          <select id={`option-${market.id}`} value={option} onChange={e => setOption(e.target.value)} required>
            <option value="">Selecione</option>
            <option value={game.teamA}>{game.teamA}</option>
            <option value={game.teamB}>{game.teamB}</option>
          </select>
        ) : (
          <input id={`option-${market.id}`} type="text" placeholder="Nome do jogador" value={option} onChange={e => setOption(e.target.value)} required />
        )}
      </div>
      <div className="form-group" style={{ marginBottom: 0, minWidth: '120px' }}>
        <label htmlFor={`amount-${market.id}`}>Valor</label>
        <input id={`amount-${market.id}`} type="number" min="1" step="1" placeholder="Valor" value={amount} onChange={e => setAmount(e.target.value)} required />
      </div>
      <button type="submit" disabled={submitting} style={{ marginBottom: 0 }}>
        {submitting ? 'Criando...' : 'Apostar'}
      </button>
      {success && <p role="status">Aposta criada!</p>}
      {error && <p role="alert">{error}</p>}
    </form>
  )
}

export default function GameDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [game, setGame] = useState<Game | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    apiClient.get<Game>(`/games/${id}`)
      .then(res => setGame(res.data))
      .catch(() => setError('Erro ao carregar jogo.'))
      .finally(() => setLoading(false))
  }, [id])

  if (loading) return <div className="page"><div className="card empty-card"><p>Carregando jogo...</p></div></div>
  if (error) return <div className="page"><div className="card empty-card"><p role="alert">{error}</p></div></div>
  if (!game) return null

  const openMarkets = game.markets.filter(m => m.status === 'Open')
  const canBet = game.status === 'Scheduled'

  return (
    <div className="page">
      <h1>{game.teamA} vs {game.teamB}</h1>
      <div className="card">
        <p>📅 {new Date(game.scheduledAt).toLocaleString('pt-BR')} &nbsp;|&nbsp; Bo{game.numberOfMaps}</p>
      </div>

      <section>
        <h2>Mercados</h2>
        {!canBet && <p style={{ color: 'var(--text-muted)', marginBottom: '1rem' }}>Apostas encerradas para este jogo.</p>}
        {openMarkets.length === 0 ? (
          <div className="card empty-card"><p>Nenhum mercado disponível.</p></div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '.75rem' }}>
            {openMarkets.map(market => (
              <div key={market.id} className="card">
                <strong>{marketLabel(market)}</strong>
                {canBet && <BetForm market={market} game={game} />}
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  )
}

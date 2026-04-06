import { useEffect, useState } from 'react'
import apiClient from '../api/client'

interface Market {
  type: string
  mapNumber: number | null
  gameId: string
}

interface Bet {
  id: string
  marketId: string
  creatorOption: string
  covererOption: string | null
  amount: number
  status: 'Pending' | 'Active' | 'Settled' | 'Cancelled' | 'Voided'
  coveredById: string | null
  market: Market
}

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Pendente',
  Active: 'Ativa',
  Settled: 'Liquidada',
  Cancelled: 'Cancelada',
  Voided: 'Anulada',
}

const STATUS_BADGE: Record<string, string> = {
  Pending: 'badge-pending',
  Active: 'badge-active',
  Settled: 'badge-settled',
  Cancelled: 'badge-expired',
  Voided: 'badge-settled',
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

function BetCard({ bet, onCancel }: { bet: Bet; onCancel: (id: string) => void }) {
  const [cancelling, setCancelling] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleCancel() {
    setCancelling(true)
    setError(null)
    try {
      await apiClient.delete(`/bets/${bet.id}`)
      onCancel(bet.id)
    } catch {
      setError('Erro ao cancelar aposta.')
    } finally {
      setCancelling(false)
    }
  }

  return (
    <li className="bet-card">
      <div className="bet-info">
        <span><strong>Mercado:</strong> {marketLabel(bet.market)}</span>
        <span><strong>Opção:</strong> {bet.creatorOption}</span>
        <span><strong>Valor:</strong> 🪙 {bet.amount}</span>
        {bet.coveredById && <span><strong>Contraparte:</strong> {bet.coveredById}</span>}
        <span className={`badge ${STATUS_BADGE[bet.status] ?? ''}`}>{STATUS_LABELS[bet.status] ?? bet.status}</span>
      </div>
      <div style={{ display: 'flex', gap: '.5rem', alignItems: 'center' }}>
        {bet.status === 'Pending' && (
          <button className="btn-danger" onClick={handleCancel} disabled={cancelling} style={{ padding: '.4rem .9rem', fontSize: '.85rem' }}>
            {cancelling ? 'Cancelando...' : 'Cancelar'}
          </button>
        )}
        {error && <p role="alert">{error}</p>}
      </div>
    </li>
  )
}

export default function BetsPage() {
  const [bets, setBets] = useState<Bet[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    apiClient.get<Bet[]>('/bets')
      .then(res => setBets(res.data))
      .catch(() => setError('Erro ao carregar apostas.'))
      .finally(() => setLoading(false))
  }, [])

  function handleCancel(id: string) {
    setBets(prev => prev.filter(b => b.id !== id))
  }

  const pending = bets.filter(b => b.status === 'Pending')
  const active = bets.filter(b => b.status === 'Active')
  const settled = bets.filter(b => b.status === 'Settled' || b.status === 'Voided' || b.status === 'Cancelled')

  if (loading) return <div className="page"><p className="empty-state">Carregando apostas...</p></div>

  return (
    <div className="page">
      <h1>Minhas Apostas</h1>
      {error && <p role="alert">{error}</p>}

      <section>
        <h2>Pendentes</h2>
        {pending.length === 0
          ? <div className="card empty-card"><p>Nenhuma aposta pendente.</p></div>
          : <ul className="bet-list">{pending.map(bet => <BetCard key={bet.id} bet={bet} onCancel={handleCancel} />)}</ul>
        }
      </section>

      <section>
        <h2>Ativas</h2>
        {active.length === 0
          ? <div className="card empty-card"><p>Nenhuma aposta ativa.</p></div>
          : <ul className="bet-list">{active.map(bet => <BetCard key={bet.id} bet={bet} onCancel={handleCancel} />)}</ul>
        }
      </section>

      <section>
        <h2>Liquidadas / Encerradas</h2>
        {settled.length === 0
          ? <div className="card empty-card"><p>Nenhuma aposta liquidada.</p></div>
          : <ul className="bet-list">{settled.map(bet => <BetCard key={bet.id} bet={bet} onCancel={handleCancel} />)}</ul>
        }
      </section>
    </div>
  )
}

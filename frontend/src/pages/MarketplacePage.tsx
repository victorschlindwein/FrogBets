import { useEffect, useState } from 'react'
import apiClient from '../api/client'

interface Market {
  type: string
  mapNumber: number | null
  gameId: string
}

interface MarketplaceBet {
  id: string
  marketId: string
  creatorOption: string
  amount: number
  creatorId: string
  market: Market
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

function BetRow({ bet, onCovered }: { bet: MarketplaceBet; onCovered: (id: string) => void }) {
  const [covering, setCovering] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleCover() {
    setCovering(true)
    setError(null)
    try {
      await apiClient.post(`/bets/${bet.id}/cover`)
      onCovered(bet.id)
    } catch {
      setError('Erro ao cobrir aposta.')
    } finally {
      setCovering(false)
      setConfirming(false)
    }
  }

  return (
    <li className="bet-card">
      <div className="bet-info">
        <span><strong>Mercado:</strong> {marketLabel(bet.market)}</span>
        <span><strong>Opção:</strong> {bet.creatorOption}</span>
        <span><strong>Valor:</strong> 🪙 {bet.amount}</span>
      </div>
      <div style={{ display: 'flex', gap: '.5rem', alignItems: 'center' }}>
        {confirming ? (
          <>
            <span style={{ fontSize: '.85rem', color: 'var(--text-muted)' }}>Confirmar {bet.amount} unidades?</span>
            <button className="btn-primary" onClick={handleCover} disabled={covering} style={{ padding: '.4rem .9rem', fontSize: '.85rem' }}>
              {covering ? 'Cobrindo...' : 'Confirmar'}
            </button>
            <button className="btn-secondary" onClick={() => setConfirming(false)} disabled={covering} style={{ padding: '.4rem .9rem', fontSize: '.85rem' }}>
              Cancelar
            </button>
          </>
        ) : (
          <button className="btn-orange" onClick={() => setConfirming(true)} style={{ padding: '.4rem .9rem', fontSize: '.85rem' }}>
            Cobrir
          </button>
        )}
        {error && <p role="alert">{error}</p>}
      </div>
    </li>
  )
}

export default function MarketplacePage() {
  const [bets, setBets] = useState<MarketplaceBet[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    apiClient.get<MarketplaceBet[]>('/marketplace')
      .then(res => setBets(res.data))
      .catch(() => setError('Erro ao carregar apostas disponíveis.'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="page"><p>Carregando marketplace...</p></div>

  return (
    <div className="page">
      <h1>🛒 Marketplace</h1>
      {error && <p role="alert">{error}</p>}
      {bets.length === 0 ? (
        <p>Nenhuma aposta disponível para cobertura.</p>
      ) : (
        <ul className="bet-list">
          {bets.map(bet => (
            <BetRow key={bet.id} bet={bet} onCovered={id => setBets(prev => prev.filter(b => b.id !== id))} />
          ))}
        </ul>
      )}
    </div>
  )
}

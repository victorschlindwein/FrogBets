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

function BetRow({
  bet,
  onCovered,
}: {
  bet: MarketplaceBet
  onCovered: (id: string) => void
}) {
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
    <li>
      <div>
        <span><strong>Mercado:</strong> {marketLabel(bet.market)}</span>
        <span> | <strong>Opção do criador:</strong> {bet.creatorOption}</span>
        <span> | <strong>Valor:</strong> {bet.amount}</span>
      </div>
      {confirming ? (
        <div>
          <span>Confirmar cobertura de {bet.amount} unidades?</span>
          <button onClick={handleCover} disabled={covering}>
            {covering ? 'Cobrindo...' : 'Confirmar'}
          </button>
          <button onClick={() => setConfirming(false)} disabled={covering}>
            Cancelar
          </button>
        </div>
      ) : (
        <button onClick={() => setConfirming(true)}>Cobrir</button>
      )}
      {error && <p role="alert">{error}</p>}
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

  function handleCovered(id: string) {
    setBets(prev => prev.filter(b => b.id !== id))
  }

  if (loading) return <p>Carregando marketplace...</p>

  return (
    <div>
      <h1>Marketplace</h1>
      {error && <p role="alert">{error}</p>}
      {bets.length === 0 ? (
        <p>Nenhuma aposta disponível para cobertura.</p>
      ) : (
        <ul>
          {bets.map(bet => (
            <BetRow key={bet.id} bet={bet} onCovered={handleCovered} />
          ))}
        </ul>
      )}
    </div>
  )
}

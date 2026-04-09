import { useEffect, useState } from 'react'
import apiClient from '../api/client'
import CoinIcon from '../components/CoinIcon'

interface Market {
  type: string
  mapNumber: number | null
  gameId: string
}

interface BetDtoResponse {
  id: string
  marketId: string
  marketType: string
  mapNumber: number | null
  gameId: string
  creatorOption: string
  amount: number
  creatorId: string
}

interface MarketplaceBet {
  id: string
  marketId: string
  creatorOption: string
  amount: number
  creatorId: string
  market: Market
}

function mapDtoToMarketplaceBet(dto: BetDtoResponse): MarketplaceBet {
  return {
    id: dto.id,
    marketId: dto.marketId,
    creatorOption: dto.creatorOption,
    amount: dto.amount,
    creatorId: dto.creatorId,
    market: {
      type: dto.marketType,
      mapNumber: dto.mapNumber,
      gameId: dto.gameId,
    },
  }
}

interface TradeListingItem {
  userId: string
  username: string
  teamId: string
  teamName: string
  createdAt: string
}

interface TradeOffer {
  id: string
  offeredUserId: string
  offeredUsername: string
  targetUserId: string
  targetUsername: string
  proposerTeamName: string
  receiverTeamName: string
  status: string
  createdAt: string
}

interface CurrentUser {
  id: string
  username: string
  isAdmin: boolean
  createdAt: string
  isTeamLeader?: boolean
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
        <span><strong>Valor:</strong> <CoinIcon /> {bet.amount}</span>
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

function TradeSection() {
  const [listings, setListings] = useState<TradeListingItem[]>([])
  const [offers, setOffers] = useState<TradeOffer[]>([])
  const [isTeamLeader, setIsTeamLeader] = useState(false)
  const [loadingListings, setLoadingListings] = useState(true)
  const [listingsError, setListingsError] = useState<string | null>(null)
  const [offersError, setOffersError] = useState<string | null>(null)

  // Form state
  const [listingUserId, setListingUserId] = useState('')
  const [offeredUserId, setOfferedUserId] = useState('')
  const [targetUserId, setTargetUserId] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const [formSuccess, setFormSuccess] = useState<string | null>(null)

  useEffect(() => {
    apiClient.get<CurrentUser>('/users/me')
      .then(res => setIsTeamLeader(res.data.isTeamLeader ?? false))
      .catch(() => setIsTeamLeader(false))

    apiClient.get<TradeListingItem[]>('/trades/listings')
      .then(res => setListings(res.data))
      .catch(() => setListingsError('Erro ao carregar jogadores disponíveis.'))
      .finally(() => setLoadingListings(false))
  }, [])

  useEffect(() => {
    if (!isTeamLeader) return
    apiClient.get<TradeOffer[]>('/trades/offers')
      .then(res => setOffers(res.data))
      .catch(() => setOffersError('Erro ao carregar ofertas.'))
  }, [isTeamLeader])

  async function handleAcceptOffer(id: string) {
    try {
      await apiClient.patch(`/trades/offers/${id}/accept`)
      setOffers(prev => prev.filter(o => o.id !== id))
    } catch {
      setOffersError('Erro ao aceitar oferta.')
    }
  }

  async function handleRejectOffer(id: string) {
    try {
      await apiClient.patch(`/trades/offers/${id}/reject`)
      setOffers(prev => prev.filter(o => o.id !== id))
    } catch {
      setOffersError('Erro ao recusar oferta.')
    }
  }

  async function handleAddListing(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)
    setFormSuccess(null)
    try {
      await apiClient.post('/trades/listings', { userId: listingUserId })
      setFormSuccess('Jogador disponibilizado para troca.')
      setListingUserId('')
      const res = await apiClient.get<TradeListingItem[]>('/trades/listings')
      setListings(res.data)
    } catch {
      setFormError('Erro ao disponibilizar jogador.')
    }
  }

  async function handleCreateOffer(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)
    setFormSuccess(null)
    try {
      await apiClient.post('/trades/offers', { offeredUserId, targetUserId })
      setFormSuccess('Oferta de troca criada.')
      setOfferedUserId('')
      setTargetUserId('')
    } catch {
      setFormError('Erro ao criar oferta de troca.')
    }
  }

  // Group listings by team
  const byTeam = listings.reduce<Record<string, TradeListingItem[]>>((acc, item) => {
    if (!acc[item.teamName]) acc[item.teamName] = []
    acc[item.teamName].push(item)
    return acc
  }, {})

  return (
    <div>
      <h2>🔄 Trocas de Jogadores</h2>

      <h3>Jogadores Disponíveis para Troca</h3>
      {listingsError && <p role="alert">{listingsError}</p>}
      {loadingListings ? (
        <div className="card empty-card"><p>Carregando...</p></div>
      ) : listings.length === 0 ? (
        <div className="card empty-card"><p>Nenhum jogador disponível para troca.</p></div>
      ) : (
        <div className="card">
          {Object.entries(byTeam).map(([teamName, members]) => (
            <div key={teamName} style={{ marginBottom: '1rem' }}>
              <strong>{teamName}</strong>
              <ul style={{ marginTop: '.35rem', paddingLeft: '1.25rem' }}>
                {members.map(m => (
                  <li key={m.userId}>{m.username}</li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}

      {isTeamLeader && (
        <>
          <h3>Minhas Ofertas Recebidas</h3>
          {offersError && <p role="alert">{offersError}</p>}
          {offers.filter(o => o.status === 'Pending').length === 0 ? (
            <div className="card empty-card"><p>Nenhuma oferta pendente.</p></div>
          ) : (
            <div className="card">
              <ul style={{ listStyle: 'none' }}>
                {offers.filter(o => o.status === 'Pending').map(offer => (
                  <li key={offer.id} style={{ marginBottom: '.75rem', display: 'flex', alignItems: 'center', gap: '.5rem', flexWrap: 'wrap' }}>
                    <span>
                      {offer.proposerTeamName} oferece <strong>{offer.offeredUsername}</strong> por <strong>{offer.targetUsername}</strong> ({offer.receiverTeamName})
                    </span>
                    <button className="btn-primary" onClick={() => handleAcceptOffer(offer.id)} style={{ padding: '.3rem .7rem', fontSize: '.85rem' }}>
                      Aceitar
                    </button>
                    <button className="btn-secondary" onClick={() => handleRejectOffer(offer.id)} style={{ padding: '.3rem .7rem', fontSize: '.85rem' }}>
                      Recusar
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          )}

          <h3>Disponibilizar para Troca</h3>
          <form onSubmit={handleAddListing} style={{ display: 'flex', gap: '.5rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
            <input
              type="text"
              placeholder="ID do membro"
              value={listingUserId}
              onChange={e => setListingUserId(e.target.value)}
              required
              style={{ padding: '.4rem', flex: '1' }}
            />
            <button className="btn-primary" type="submit" style={{ padding: '.4rem .9rem' }}>
              Disponibilizar
            </button>
          </form>

          <h3>Criar Oferta de Troca</h3>
          <form onSubmit={handleCreateOffer} style={{ display: 'flex', gap: '.5rem', flexWrap: 'wrap' }}>
            <input
              type="text"
              placeholder="ID do jogador oferecido"
              value={offeredUserId}
              onChange={e => setOfferedUserId(e.target.value)}
              required
              style={{ padding: '.4rem', flex: '1' }}
            />
            <input
              type="text"
              placeholder="ID do jogador alvo"
              value={targetUserId}
              onChange={e => setTargetUserId(e.target.value)}
              required
              style={{ padding: '.4rem', flex: '1' }}
            />
            <button className="btn-primary" type="submit" style={{ padding: '.4rem .9rem' }}>
              Criar Oferta
            </button>
          </form>

          {formError && <p role="alert" style={{ marginTop: '.5rem' }}>{formError}</p>}
          {formSuccess && <p style={{ marginTop: '.5rem', color: 'green' }}>{formSuccess}</p>}
        </>
      )}
    </div>
  )
}

export default function MarketplacePage() {
  const [bets, setBets] = useState<MarketplaceBet[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    apiClient.get<BetDtoResponse[]>('/marketplace')
      .then(res => setBets(res.data.map(mapDtoToMarketplaceBet)))
      .catch(() => setError('Erro ao carregar apostas disponíveis.'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="page"><div className="card empty-card"><p>Carregando marketplace...</p></div></div>

  return (
    <div className="page">
      <h1>🛒 Marketplace</h1>
      {error && <p role="alert">{error}</p>}
      {bets.length === 0 ? (
        <div className="card empty-card"><p>Nenhuma aposta disponível para cobertura.</p></div>
      ) : (
        <ul className="bet-list">
          {bets.map(bet => (
            <BetRow key={bet.id} bet={bet} onCovered={id => setBets(prev => prev.filter(b => b.id !== id))} />
          ))}
        </ul>
      )}

      <hr />
      <TradeSection />
    </div>
  )
}

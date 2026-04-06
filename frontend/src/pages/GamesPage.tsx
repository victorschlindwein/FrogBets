import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import apiClient from '../api/client'

interface Game {
  id: string
  teamA: string
  teamB: string
  scheduledAt: string
  numberOfMaps: number
  status: 'Scheduled' | 'InProgress' | 'Finished'
}

const STATUS_LABELS: Record<string, string> = {
  Scheduled: 'Agendado',
  InProgress: 'Em andamento',
  Finished: 'Finalizado',
}

const STATUS_BADGE: Record<string, string> = {
  Scheduled: 'badge-pending',
  InProgress: 'badge-active',
  Finished: 'badge-settled',
}

export default function GamesPage() {
  const [games, setGames] = useState<Game[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    apiClient.get<Game[]>('/games')
      .then(res => setGames(res.data))
      .catch(() => setError('Erro ao carregar jogos.'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="page"><p>Carregando jogos...</p></div>

  return (
    <div className="page">
      <h1>Jogos</h1>
      {error && <p role="alert">{error}</p>}
      {games.length === 0 ? (
        <p>Nenhum jogo disponível.</p>
      ) : (
        <ul className="game-list">
          {games.map(game => (
            <li key={game.id} className="game-item">
              <span className="game-teams">
                <Link to={`/games/${game.id}`}>{game.teamA} vs {game.teamB}</Link>
              </span>
              <span className="game-meta">📅 {new Date(game.scheduledAt).toLocaleString('pt-BR')}</span>
              <span className="game-meta">Bo{game.numberOfMaps}</span>
              <span className={`badge ${STATUS_BADGE[game.status] ?? ''}`}>
                {STATUS_LABELS[game.status] ?? game.status}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

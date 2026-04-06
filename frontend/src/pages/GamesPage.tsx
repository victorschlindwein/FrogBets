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

  if (loading) return <p>Carregando jogos...</p>

  return (
    <div>
      <h1>Jogos</h1>
      {error && <p role="alert">{error}</p>}
      {games.length === 0 ? (
        <p>Nenhum jogo disponível.</p>
      ) : (
        <ul>
          {games.map(game => (
            <li key={game.id}>
              <Link to={`/games/${game.id}`}>
                <strong>{game.teamA} vs {game.teamB}</strong>
              </Link>
              <span> | {new Date(game.scheduledAt).toLocaleString('pt-BR')}</span>
              <span> | Bo{game.numberOfMaps}</span>
              <span> | {STATUS_LABELS[game.status] ?? game.status}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

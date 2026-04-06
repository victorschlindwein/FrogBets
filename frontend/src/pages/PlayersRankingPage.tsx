import { useEffect, useState } from 'react'
import { getPlayersRanking, PlayerRankingItem } from '../api/players'

export default function PlayersRankingPage() {
  const [ranking, setRanking] = useState<PlayerRankingItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getPlayersRanking()
      .then(data => setRanking(data))
      .catch(() => setError('Erro ao carregar ranking de jogadores.'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="page"><div className="card empty-card"><p>Carregando ranking...</p></div></div>

  return (
    <div className="page">
      <h1>🎮 Ranking CS2</h1>
      {error && <p role="alert">{error}</p>}
      {ranking.length === 0 ? (
        <div className="card empty-card"><p>Nenhum jogador encontrado.</p></div>
      ) : (
        <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Nickname</th>
                <th>Time</th>
                <th>Score</th>
                <th>Partidas</th>
              </tr>
            </thead>
            <tbody>
              {ranking.map((item) => (
                <tr key={item.playerId}>
                  <td><strong>{item.position}</strong></td>
                  <td>
                    {item.position === 1 ? '🥇 ' : item.position === 2 ? '🥈 ' : item.position === 3 ? '🥉 ' : ''}
                    {item.nickname}
                  </td>
                  <td>{item.teamName}</td>
                  <td>{item.playerScore.toFixed(4)}</td>
                  <td>{item.matchesCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

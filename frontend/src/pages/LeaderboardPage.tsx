import { useEffect, useState } from 'react'
import apiClient from '../api/client'

interface LeaderboardEntry {
  username: string
  virtualBalance: number
  winsCount: number
  lossesCount: number
}

export default function LeaderboardPage() {
  const [entries, setEntries] = useState<LeaderboardEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    apiClient.get<LeaderboardEntry[]>('/leaderboard')
      .then(res => setEntries(res.data))
      .catch(() => setError('Erro ao carregar classificação.'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="page"><p>Carregando classificação...</p></div>

  return (
    <div className="page">
      <h1>🏆 Classificação</h1>
      {error && <p role="alert">{error}</p>}
      {entries.length === 0 ? (
        <p>Nenhum usuário encontrado.</p>
      ) : (
        <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
          <table>
            <thead>
              <tr>
                <th>Posição</th>
                <th>Usuário</th>
                <th>Saldo Virtual</th>
                <th>Vitórias</th>
                <th>Derrotas</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((entry, index) => (
                <tr key={entry.username}>
                  <td><strong>{index + 1}</strong></td>
                  <td>{index === 0 ? '🥇 ' : index === 1 ? '🥈 ' : index === 2 ? '🥉 ' : ''}<span>{entry.username}</span></td>
                  <td>🪙 <span>{entry.virtualBalance}</span></td>
                  <td style={{ color: 'var(--green)' }}>{entry.winsCount}</td>
                  <td style={{ color: 'var(--danger)' }}>{entry.lossesCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

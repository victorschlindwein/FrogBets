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

  if (loading) return <p>Carregando classificação...</p>

  return (
    <div>
      <h1>Classificação</h1>
      {error && <p role="alert">{error}</p>}
      {entries.length === 0 ? (
        <p>Nenhum usuário encontrado.</p>
      ) : (
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
                <td>{index + 1}</td>
                <td>{entry.username}</td>
                <td>{entry.virtualBalance}</td>
                <td>{entry.winsCount}</td>
                <td>{entry.lossesCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

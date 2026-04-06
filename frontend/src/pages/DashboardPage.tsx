import { useEffect, useState } from 'react'
import apiClient from '../api/client'

interface Balance {
  virtualBalance: number
  reservedBalance: number
}

export default function DashboardPage() {
  const [balance, setBalance] = useState<Balance | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    apiClient.get<Balance>('/users/me/balance')
      .then(res => setBalance(res.data))
      .catch(() => setError('Erro ao carregar saldo.'))
  }, [])

  return (
    <div className="page">
      <h1>Dashboard</h1>
      {error && <p role="alert">{error}</p>}
      {balance && (
        <div className="stats-grid">
          <div className="stat-card">
            <div className="stat-label">Saldo Disponível</div>
            <div className="stat-value">🪙 {balance.virtualBalance.toLocaleString('pt-BR')}</div>
          </div>
          <div className="stat-card" style={{ borderLeftColor: 'var(--green)' }}>
            <div className="stat-label">Saldo Reservado</div>
            <div className="stat-value">🔒 {balance.reservedBalance.toLocaleString('pt-BR')}</div>
          </div>
        </div>
      )}
    </div>
  )
}

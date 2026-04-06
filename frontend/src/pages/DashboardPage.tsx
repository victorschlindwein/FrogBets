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
    <div>
      <h1>Dashboard</h1>
      {error && <p role="alert">{error}</p>}
      {balance && (
        <div>
          <p>Saldo disponível: <strong>{balance.virtualBalance}</strong></p>
          <p>Saldo reservado: <strong>{balance.reservedBalance}</strong></p>
        </div>
      )}
    </div>
  )
}

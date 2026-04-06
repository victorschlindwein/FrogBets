import { Link, useNavigate } from 'react-router-dom'
import apiClient, { setToken, getToken } from '../api/client'
import { useEffect, useState } from 'react'

interface Me {
  username: string
  isAdmin: boolean
}

export default function Navbar() {
  const [me, setMe] = useState<Me | null>(null)
  const navigate = useNavigate()

  useEffect(() => {
    if (!getToken()) return
    apiClient.get<Me>('/users/me')
      .then(res => setMe(res.data))
      .catch(() => {})
  }, [])

  async function handleLogout() {
    try { await apiClient.post('/auth/logout') } catch { /* ignore */ }
    setToken(null)
    navigate('/login')
  }

  return (
    <nav style={{ display: 'flex', gap: '1rem', padding: '0.75rem 1rem', borderBottom: '1px solid #ccc', alignItems: 'center' }}>
      <Link to="/">Dashboard</Link>
      <Link to="/games">Jogos</Link>
      <Link to="/bets">Minhas Apostas</Link>
      <Link to="/marketplace">Marketplace</Link>
      <Link to="/leaderboard">Ranking</Link>
      {me?.isAdmin && <Link to="/admin">Admin</Link>}
      <span style={{ marginLeft: 'auto' }}>{me?.username}</span>
      <button onClick={handleLogout}>Sair</button>
    </nav>
  )
}

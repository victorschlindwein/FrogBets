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
    <nav className="navbar">
      <span className="nav-brand">🐸 FrogBets</span>
      <Link to="/">Dashboard</Link>
      <Link to="/games">Jogos</Link>
      <Link to="/bets">Minhas Apostas</Link>
      <Link to="/marketplace">Marketplace</Link>
      <Link to="/leaderboard">Ranking Apostas</Link>
      <Link to="/players/ranking">Ranking CS2</Link>
      {me?.isAdmin && <Link to="/admin">Admin</Link>}
      <span className="nav-spacer" />
      <span className="nav-user">{me?.username}</span>
      <button className="btn-orange" onClick={handleLogout} style={{ padding: '.4rem .9rem', fontSize: '.85rem' }}>Sair</button>
    </nav>
  )
}

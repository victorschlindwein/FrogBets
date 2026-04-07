import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { publicClient, setToken } from '../api/client'

export default function LoginPage() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      const res = await publicClient.post<{ token: string }>('/auth/login', { username, password })
      setToken(res.data.token)
      navigate('/')
    } catch {
      setError('Credenciais inválidas.')
    }
  }

  return (
    <div className="auth-wrapper">
      <div className="auth-card">
        <div className="auth-logo">🐸</div>
        <h1>FrogBets</h1>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="username">Usuário</label>
            <input id="username" value={username} onChange={e => setUsername(e.target.value)} required autoFocus />
          </div>
          <div className="form-group">
            <label htmlFor="password">Senha</label>
            <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} required />
          </div>
          {error && <p role="alert">{error}</p>}
          <button type="submit" style={{ width: '100%', marginTop: '.5rem' }}>Entrar</button>
        </form>
        <p className="auth-footer">
          Não tem conta? <Link to="/register">Criar conta</Link>
        </p>
      </div>
    </div>
  )
}

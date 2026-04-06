import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import apiClient, { setToken } from '../api/client'

export default function LoginPage() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      const res = await apiClient.post<{ token: string }>('/auth/login', { username, password })
      setToken(res.data.token)
      navigate('/')
    } catch {
      setError('Credenciais inválidas.')
    }
  }

  return (
    <div>
      <h1>FrogBets — Login</h1>
      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="username">Usuário</label>
          <input id="username" value={username} onChange={e => setUsername(e.target.value)} required />
        </div>
        <div>
          <label htmlFor="password">Senha</label>
          <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} required />
        </div>
        {error && <p role="alert">{error}</p>}
        <button type="submit">Entrar</button>
      </form>
      <p>
        Não tem conta? <Link to="/register">Criar conta</Link>
      </p>
    </div>
  )
}

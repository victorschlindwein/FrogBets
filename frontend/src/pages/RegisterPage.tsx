import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import apiClient, { setToken } from '../api/client'

const ERROR_MESSAGES: Record<string, string> = {
  INVALID_INVITE: 'Convite inválido ou expirado.',
  INVITE_ALREADY_USED: 'Este convite já foi utilizado.',
  USERNAME_TAKEN: 'Nome de usuário já está em uso.',
  PASSWORD_TOO_SHORT: 'A senha deve ter no mínimo 8 caracteres.',
}

export default function RegisterPage() {
  const [inviteToken, setInviteToken] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      const res = await apiClient.post<{ token: string }>('/auth/register', {
        inviteToken,
        username,
        password,
      })
      setToken(res.data.token)
      navigate('/')
    } catch (err: unknown) {
      const code = (err as { response?: { data?: { error?: { code?: string } } } })
        ?.response?.data?.error?.code
      setError(code ? (ERROR_MESSAGES[code] ?? 'Erro ao criar conta.') : 'Erro ao criar conta.')
    }
  }

  return (
    <div className="auth-wrapper">
      <div className="auth-card">
        <div className="auth-logo">🐸</div>
        <h1>Criar Conta</h1>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="inviteToken">Código de Convite</label>
            <input
              id="inviteToken"
              value={inviteToken}
              onChange={e => setInviteToken(e.target.value)}
              placeholder="Cole o código aqui"
              required
            />
          </div>
          <div className="form-group">
            <label htmlFor="username">Usuário</label>
            <input
              id="username"
              value={username}
              onChange={e => setUsername(e.target.value)}
              required
            />
          </div>
          <div className="form-group">
            <label htmlFor="password">Senha</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              minLength={8}
              required
            />
          </div>
          {error && <p role="alert">{error}</p>}
          <button type="submit" style={{ width: '100%', marginTop: '.5rem' }}>Criar Conta</button>
        </form>
        <p className="auth-footer">
          Já tem conta? <Link to="/login">Entrar</Link>
        </p>
      </div>
    </div>
  )
}

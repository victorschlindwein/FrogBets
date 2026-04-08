import { useEffect, useState } from 'react'
import apiClient from '../api/client'
import { CS2Team, CS2Player, getTeams, uploadTeamLogo, removeTeamLogo } from '../api/players'

export interface TeamMember {
  id: string
  username: string
  isTeamLeader: boolean
}

// ── Exported pure function for property-based tests ──────────────────────────
export function groupPlayersByTeam(players: CS2Player[]): Record<string, CS2Player[]> {
  return players.reduce<Record<string, CS2Player[]>>((acc, p) => {
    if (!p.teamId) return acc
    acc[p.teamId] = [...(acc[p.teamId] ?? []), p]
    return acc
  }, {})
}

const getMembersByTeam = (teamId: string): Promise<TeamMember[]> =>
  apiClient.get<TeamMember[]>(`/teams/${teamId}/members`).then(r => r.data)

// ── TeamCard ──────────────────────────────────────────────────────────────────
interface TeamCardProps {
  team: CS2Team
  members: TeamMember[]
  isLeader: boolean
  onLogoUpdate: (teamId: string, logoUrl: string | null) => void
}

const MAX_SIZE = 256 // px
const MAX_BYTES = 200 * 1024 // 200 KB

function resizeToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    const url = URL.createObjectURL(file)
    img.onload = () => {
      URL.revokeObjectURL(url)
      const scale = Math.min(1, MAX_SIZE / Math.max(img.width, img.height))
      const w = Math.round(img.width * scale)
      const h = Math.round(img.height * scale)
      const canvas = document.createElement('canvas')
      canvas.width = w
      canvas.height = h
      canvas.getContext('2d')!.drawImage(img, 0, 0, w, h)
      // try webp first, fall back to jpeg
      let data = canvas.toDataURL('image/webp', 0.85)
      if (data.length > MAX_BYTES * 1.37) data = canvas.toDataURL('image/jpeg', 0.8)
      if (data.length > MAX_BYTES * 1.37) { reject(new Error('TOO_LARGE')); return }
      resolve(data)
    }
    img.onerror = () => { URL.revokeObjectURL(url); reject(new Error('LOAD_ERROR')) }
    img.src = url
  })
}

function TeamCard({ team, members, isLeader, onLogoUpdate }: TeamCardProps) {
  const [logoError, setLogoError] = useState(false)
  const [cardError, setCardError] = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    e.target.value = ''
    setUploading(true)
    setCardError(null)
    try {
      const base64 = await resizeToBase64(file)
      const updated = await uploadTeamLogo(team.id, base64)
      onLogoUpdate(team.id, updated.logoUrl ?? null)
      setLogoError(false)
    } catch (err) {
      const msg = err instanceof Error && err.message === 'TOO_LARGE'
        ? 'Imagem muito grande. Use uma imagem menor que 200 KB após redimensionamento.'
        : 'Erro ao processar imagem. Tente outro arquivo.'
      setCardError(msg)
    } finally {
      setUploading(false)
    }
  }

  async function handleRemove() {
    setUploading(true)
    setCardError(null)
    try {
      await removeTeamLogo(team.id)
      onLogoUpdate(team.id, null)
      setLogoError(false)
    } catch {
      setCardError('Erro ao remover logo. Tente novamente.')
    } finally {
      setUploading(false)
    }
  }

  const showLogo = team.logoUrl && !logoError

  return (
    <div role="article" className="card" style={{ marginBottom: '1.25rem' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', marginBottom: '1rem' }}>
        {showLogo ? (
          <img
            src={team.logoUrl!}
            alt={`Logo ${team.name}`}
            onError={() => setLogoError(true)}
            style={{ width: 64, height: 64, objectFit: 'contain', borderRadius: 'var(--radius)', border: '1px solid var(--sand)' }}
          />
        ) : (
          <span style={{ fontSize: '2.5rem', lineHeight: 1 }} aria-label="Sem logo">🐸</span>
        )}
        <h2 style={{ margin: 0 }}>{team.name}</h2>
      </div>

      {isLeader && (
        <div style={{ marginBottom: '1rem' }}>
          <div style={{ display: 'flex', gap: '.5rem', flexWrap: 'wrap', alignItems: 'center' }}>
            <label
              role="button"
              aria-label="Alterar logo"
              style={{
                cursor: uploading ? 'not-allowed' : 'pointer',
                opacity: uploading ? 0.6 : 1,
              }}
            >
              <input
                type="file"
                accept="image/*"
                style={{ display: 'none' }}
                disabled={uploading}
                onChange={handleFileChange}
                aria-label="Alterar logo"
              />
              <span className="btn-primary" style={{ pointerEvents: 'none' }}>
                {uploading ? 'Enviando...' : 'Alterar logo'}
              </span>
            </label>
            {team.logoUrl && (
              <button
                className="btn-danger"
                onClick={handleRemove}
                disabled={uploading}
              >
                Remover logo
              </button>
            )}
          </div>
          {cardError && <p role="alert" style={{ marginTop: '.5rem' }}>{cardError}</p>}
        </div>
      )}

      {members.length === 0 ? (
        <p style={{ color: 'var(--text-muted)', fontStyle: 'italic' }}>Nenhum jogador cadastrado.</p>
      ) : (
        <ul style={{ listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '.5rem' }}>
          {members.map(member => (
            <li key={member.id} style={{ display: 'flex', alignItems: 'center', gap: '.75rem' }}>
              <span
                style={{
                  width: 36, height: 36, borderRadius: '50%',
                  background: 'var(--cream)', border: '1px solid var(--sand)',
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: '1.1rem'
                }}
                aria-label="Sem foto"
              >
                🎮
              </span>
              <span style={{ fontWeight: 500 }}>
                {member.username}
                {member.isTeamLeader && <span style={{ marginLeft: '.4rem', fontSize: '.75rem', color: 'var(--text-muted)' }}>👑</span>}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

// ── TeamsPage ─────────────────────────────────────────────────────────────────
interface MeResponse {
  isTeamLeader: boolean
  teamId: string | null
}

export default function TeamsPage() {
  const [teams, setTeams] = useState<CS2Team[]>([])
  const [membersByTeam, setMembersByTeam] = useState<Record<string, TeamMember[]>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [me, setMe] = useState<MeResponse | null>(null)

  useEffect(() => {
    async function load() {
      try {
        const [fetchedTeams, meData] = await Promise.all([
          getTeams(),
          apiClient.get<MeResponse>('/users/me').then(r => r.data).catch(() => null),
        ])

        setMe(meData)

        if (fetchedTeams.length === 0) {
          setTeams([])
          setLoading(false)
          return
        }

        const memberResults = await Promise.all(
          fetchedTeams.map(t =>
            getMembersByTeam(t.id)
              .then(members => ({ teamId: t.id, members }))
              .catch(() => ({ teamId: t.id, members: [] as TeamMember[] }))
          )
        )

        const map: Record<string, TeamMember[]> = {}
        for (const { teamId, members } of memberResults) {
          map[teamId] = members
        }

        setTeams(fetchedTeams)
        setMembersByTeam(map)
      } catch {
        setError('Erro ao carregar times.')
      } finally {
        setLoading(false)
      }
    }

    load()
  }, [])

  function handleLogoUpdate(teamId: string, logoUrl: string | null) {
    setTeams(prev =>
      prev.map(t => t.id === teamId ? { ...t, logoUrl } : t)
    )
  }

  if (loading) {
    return (
      <div className="page">
        <div className="card empty-card"><p>Carregando times...</p></div>
      </div>
    )
  }

  return (
    <div className="page">
      <h1>🛡️ Times</h1>
      {error && <p role="alert">{error}</p>}
      {!error && teams.length === 0 ? (
        <div className="card empty-card"><p>Nenhum time cadastrado.</p></div>
      ) : (
        teams.map(team => (
          <TeamCard
            key={team.id}
            team={team}
            members={membersByTeam[team.id] ?? []}
            isLeader={!!(me?.isTeamLeader && me.teamId === team.id)}
            onLogoUpdate={handleLogoUpdate}
          />
        ))
      )}
    </div>
  )
}

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

function TeamCard({ team, members, isLeader, onLogoUpdate }: TeamCardProps) {
  const [logoInput, setLogoInput] = useState('')
  const [logoError, setLogoError] = useState(false)
  const [cardError, setCardError] = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)

  async function handleUpload() {
    if (!logoInput.trim()) return
    setUploading(true)
    setCardError(null)
    try {
      const updated = await uploadTeamLogo(team.id, logoInput.trim())
      onLogoUpdate(team.id, updated.logoUrl ?? null)
      setLogoInput('')
      setLogoError(false)
    } catch {
      setCardError('Erro ao atualizar logo. Verifique a URL e tente novamente.')
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
            <input
              type="url"
              placeholder="URL da nova logo"
              value={logoInput}
              onChange={e => setLogoInput(e.target.value)}
              style={{ flex: 1, minWidth: 200 }}
              aria-label="URL da nova logo"
            />
            <button
              className="btn-primary"
              onClick={handleUpload}
              disabled={uploading || !logoInput.trim()}
            >
              Alterar logo
            </button>
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

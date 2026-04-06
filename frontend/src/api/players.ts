import apiClient, { publicClient } from './client'

export interface CS2Team {
  id: string;
  name: string;
  logoUrl?: string;
  createdAt: string;
}

export interface CS2Player {
  id: string;
  nickname: string;
  realName?: string;
  teamId: string;
  teamName: string;
  photoUrl?: string;
  playerScore: number;
  matchesCount: number;
  createdAt: string;
}

export interface PlayerRankingItem {
  position: number;
  playerId: string;
  nickname: string;
  teamName: string;
  playerScore: number;
  matchesCount: number;
}

export interface RegisterStatsPayload {
  gameId: string;
  kills: number;
  deaths: number;
  assists: number;
  totalDamage: number;
  rounds: number;
  kastPercent: number;
}

export const getTeams = (): Promise<CS2Team[]> =>
  publicClient.get<CS2Team[]>('/teams').then((r) => r.data)

export const createTeam = (data: { name: string; logoUrl?: string }): Promise<CS2Team> =>
  apiClient.post<CS2Team>('/teams', data).then((r) => r.data)

export const getPlayers = (): Promise<CS2Player[]> =>
  apiClient.get<CS2Player[]>('/players').then((r) => r.data)

export const createPlayer = (data: {
  nickname: string;
  realName?: string;
  teamId: string;
  photoUrl?: string;
}): Promise<CS2Player> =>
  apiClient.post<CS2Player>('/players', data).then((r) => r.data)

export const getPlayersRanking = (): Promise<PlayerRankingItem[]> =>
  apiClient.get<PlayerRankingItem[]>('/players/ranking').then((r) => r.data)

export const registerMatchStats = (
  playerId: string,
  data: RegisterStatsPayload
): Promise<unknown> =>
  apiClient.post(`/players/${playerId}/stats`, data).then((r) => r.data)

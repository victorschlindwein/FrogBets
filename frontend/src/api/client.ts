import axios from 'axios'

const TOKEN_KEY = 'frogbets_token'

// Em produção, VITE_API_URL aponta para a URL pública da API (ex: https://frogbets-api.azurecontainerapps.io)
// Em dev, usa '/api' que é proxiado pelo Vite para http://api:8080
const BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

export const setToken = (token: string | null) => {
  if (token) {
    sessionStorage.setItem(TOKEN_KEY, token)
  } else {
    sessionStorage.removeItem(TOKEN_KEY)
  }
}

export const getToken = () => sessionStorage.getItem(TOKEN_KEY)

const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' }
})

apiClient.interceptors.request.use((config) => {
  const token = getToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      setToken(null)
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

// Cliente sem interceptor de redirecionamento — para endpoints públicos
export const publicClient = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' }
})

export default apiClient

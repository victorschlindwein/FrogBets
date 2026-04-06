import { Navigate, Outlet } from 'react-router-dom'
import { getToken } from '../api/client'
import Navbar from './Navbar'

export default function ProtectedRoute() {
  const token = getToken()
  if (!token) return <Navigate to="/login" replace />
  return (
    <>
      <Navbar />
      <Outlet />
    </>
  )
}

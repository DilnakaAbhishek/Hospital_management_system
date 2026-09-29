import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from './AuthContext'

export default function ProtectedRoute({ roles }) {
  const { user } = useAuth()
  if (!user || !localStorage.getItem('hms_token')) return <Navigate to="/login" replace />
  if (roles && !roles.includes(user.role)) return <Navigate to={user.role === 'Doctor' ? '/doctor/dashboard' : user.role === 'Admin' ? '/dashboard' : '/login'} replace />
  return <Outlet />
}

import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function AppNav() {
  const { logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <nav className="flex items-center justify-between border-b border-slate-800 bg-slate-950 px-6 py-4 lg:px-8">
      <Link to="/" className="font-bold tracking-wide text-indigo-400">DIGITAL WALLET</Link>
      <button onClick={handleLogout} className="text-sm text-slate-400 transition hover:text-white">Sign out</button>
    </nav>
  )
}

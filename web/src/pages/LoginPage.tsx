import { useState } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function LoginPage() {
  const { isAuthenticated, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  if (isAuthenticated) return <Navigate to="/" replace />

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    setLoading(true)
    try {
      await login(email, password)
      navigate((location.state as { from?: string } | null)?.from ?? '/', { replace: true })
    } catch {
      setError('Login failed. Please check your email and password.')
    } finally {
      setLoading(false)
    }
  }

  return <AuthForm title="Welcome back" submitLabel={loading ? 'Signing in...' : 'Sign in'} email={email} password={password} error={error} onEmailChange={setEmail} onPasswordChange={setPassword} onSubmit={handleSubmit} footer={<Link to="/register" className="text-indigo-400 hover:text-indigo-300">Create an account</Link>} />
}

type AuthFormProps = { title: string; submitLabel: string; email: string; password: string; error: string; onEmailChange: (value: string) => void; onPasswordChange: (value: string) => void; onSubmit: (event: React.FormEvent<HTMLFormElement>) => void; footer: React.ReactNode }

export function AuthForm({ title, submitLabel, email, password, error, onEmailChange, onPasswordChange, onSubmit, footer }: AuthFormProps) {
  return <main className="flex min-h-screen items-center justify-center bg-slate-950 px-6 text-slate-100"><form onSubmit={onSubmit} className="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900 p-8"><p className="text-sm font-semibold uppercase tracking-[0.25em] text-indigo-400">Digital Wallet</p><h1 className="mt-4 text-3xl font-bold">{title}</h1>{error && <p className="mt-4 rounded-lg bg-rose-500/10 p-3 text-sm text-rose-300">{error}</p>}<label className="mt-6 block text-sm text-slate-300">Email<input required type="email" value={email} onChange={(event) => onEmailChange(event.target.value)} className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-3 outline-none focus:border-indigo-400" /></label><label className="mt-4 block text-sm text-slate-300">Password<input required type="password" minLength={1} value={password} onChange={(event) => onPasswordChange(event.target.value)} className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-3 outline-none focus:border-indigo-400" /></label><button type="submit" className="mt-6 w-full rounded-lg bg-indigo-500 px-4 py-3 font-semibold transition hover:bg-indigo-400">{submitLabel}</button><p className="mt-6 text-center text-sm text-slate-400">{footer}</p></form></main>
}

import { useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { register } from '../api/services/identityApi'
import { useAuth } from '../auth/AuthContext'
import { AuthForm } from './LoginPage'

export function RegisterPage() {
  const { isAuthenticated } = useAuth()
  const navigate = useNavigate()
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
      await register({ email, password })
      navigate('/login', { replace: true })
    } catch {
      setError('Registration failed. Use a valid email and a password of at least 12 characters.')
    } finally {
      setLoading(false)
    }
  }

  return <AuthForm title="Create your account" submitLabel={loading ? 'Creating...' : 'Create account'} email={email} password={password} error={error} onEmailChange={setEmail} onPasswordChange={setPassword} onSubmit={handleSubmit} footer={<Link to="/login" className="text-indigo-400 hover:text-indigo-300">Already have an account?</Link>} />
}

import { useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { getRegistrationStatus, register } from '../api/services/identityApi'
import { useAuth } from '../auth/AuthContext'
import { AuthForm } from './LoginPage'

export function RegisterPage() {
  const { isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [waiting, setWaiting] = useState(false)

  if (isAuthenticated) return <Navigate to="/" replace />

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    setLoading(true)
    try {
      const registration = await register({ email, password })
      setWaiting(true)
      for (let attempt = 0; attempt < 15; attempt += 1) {
        await new Promise((resolve) => setTimeout(resolve, 1000))
        const status = await getRegistrationStatus(registration.requestId)
        if (status.status === 'Completed') {
          navigate('/login', { replace: true, state: { message: 'Registration completed. You can now sign in.' } })
          return
        }
        if (status.status === 'Failed') throw new Error('Registration failed')
      }
      throw new Error('Registration is still processing')
    } catch {
      setError('Registration is not complete yet. Please try again or wait before signing in.')
    } finally {
      setLoading(false)
      setWaiting(false)
    }
  }

  return <AuthForm title="Create your account" submitLabel={loading ? (waiting ? 'Confirming registration...' : 'Creating...') : 'Create account'} email={email} password={password} error={error} onEmailChange={setEmail} onPasswordChange={setPassword} onSubmit={handleSubmit} footer={<Link to="/login" className="text-indigo-400 hover:text-indigo-300">Already have an account?</Link>} />
}

import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await login(email, password)
      navigate('/app/dashboard')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-base-200 flex flex-col items-center justify-center px-4">
      <div className="w-full max-w-sm">
        <div className="flex items-center justify-center gap-2 mb-8">
          <div className="flex items-center justify-center w-9 h-9 rounded-lg bg-primary">
            <span className="icon-[tabler--zap] size-5 text-primary-content" />
          </div>
          <span className="text-base-content text-lg font-bold">MyServiceAO</span>
        </div>
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body gap-5">
            <div>
              <h1 className="text-base-content text-xl font-semibold">Sign in</h1>
              <p className="text-base-content/60 text-sm mt-1">Welcome back to your command center.</p>
            </div>
            {error && <div className="alert alert-soft alert-error text-sm"><span className="icon-[tabler--alert-circle] size-4 shrink-0" />{error}</div>}
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="label-text mb-1.5 block" htmlFor="email">Email</label>
                <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required className="input w-full" placeholder="you@company.com" />
              </div>
              <div>
                <label className="label-text mb-1.5 block" htmlFor="password">Password</label>
                <input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required className="input w-full" placeholder="••••••••" />
              </div>
              <button type="submit" disabled={loading} className="btn btn-primary w-full">
                {loading ? <span className="loading loading-spinner loading-sm" /> : null}
                {loading ? 'Signing in...' : 'Sign in'}
              </button>
            </form>
          </div>
        </div>
        <p className="text-center text-sm text-base-content/60 mt-4">Don't have an account?{' '}<Link to="/app/register" className="link link-primary">Get started</Link></p>
      </div>
    </div>
  )
}

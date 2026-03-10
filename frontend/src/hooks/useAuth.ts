import { useState, useEffect, createContext, useContext } from 'react'
import { authApi, type AuthUser } from '@/api/auth'

interface AuthContextType {
  user: AuthUser | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  register: (payload: {
    companyName: string
    email: string
    password: string
    firstName: string
    lastName: string
  }) => Promise<void>
}

export const AuthContext = createContext<AuthContextType | null>(null)

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}

export function useAuthProvider(): AuthContextType {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    authApi.me()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false))
  }, [])

  const login = async (email: string, password: string) => {
    const u = await authApi.login(email, password)
    setUser(u)
  }

  const logout = async () => {
    await authApi.logout()
    setUser(null)
  }

  const register = async (payload: {
    companyName: string
    email: string
    password: string
    firstName: string
    lastName: string
  }) => {
    const u = await authApi.register(payload)
    setUser(u)
  }

  return { user, loading, login, logout, register }
}

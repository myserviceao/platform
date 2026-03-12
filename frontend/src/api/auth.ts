export interface AuthUser {
  id: number
  email: string
  firstName: string
  lastName: string
  role: string
  title: string | null
  theme: string | null
  tenant: {
    id: number
    name: string
    slug: string
    theme: string | null
    logoUrl: string | null
  }
}

async function request<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    ...options,
  })
  const data = await res.json()
  if (!res.ok) throw new Error(data.error || 'Request failed')
  return data as T
}

export const authApi = {
  login: (email: string, password: string) =>
    request<AuthUser>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  register: (payload: {
    companyName: string
    email: string
    password: string
    firstName: string
    lastName: string
  }) =>
    request<AuthUser>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  logout: () =>
    request<{ message: string }>('/api/auth/logout', { method: 'POST' }),

  me: () => request<AuthUser>('/api/auth/me'),
}

import { useState, useEffect, useRef } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { useTheme, type Theme } from '@/hooks/useTheme'

const navItems = [
  {
    label: 'Dashboard',
    icon: 'icon-[tabler--layout-dashboard]',
    path: '/app/dashboard',
  },
  {
    label: 'PM Tracker',
    icon: 'icon-[tabler--calendar-check]',
    path: '/app/pm-tracker',
  },
  {
    label: 'PM Outreach',
    icon: 'icon-[tabler--send]',
    path: '/app/pm-outreach',
  },
  {
    label: 'Work Orders',
    icon: 'icon-[tabler--clipboard-list]',
    path: '/app/work-orders',
  },
  {
    label: 'Customers',
    icon: 'icon-[tabler--users]',
    path: '/app/customers',
  },
  {
    label: 'Accounts Payable',
    icon: 'icon-[tabler--receipt]',
    path: '/app/ap',
  },
  {
    label: 'AR Alerts',
    icon: 'icon-[tabler--alert-circle]',
    path: '/app/ar-alerts',
  },
]

const THEMES: { value: Theme; label: string; icon: string }[] = [
  { value: 'dark', label: 'Dark', icon: 'icon-[tabler--moon]' },
  { value: 'light', label: 'Light', icon: 'icon-[tabler--sun]' },
  { value: 'corporate', label: 'Corporate', icon: 'icon-[tabler--building]' },
]

interface SearchResult {
  type: 'customer' | 'job' | 'page'
  label: string
  sub?: string
  path: string
}

const pageResults: SearchResult[] = [
  { type: 'page', label: 'Dashboard', path: '/app/dashboard' },
  { type: 'page', label: 'PM Tracker', path: '/app/pm-tracker' },
  { type: 'page', label: 'Customers', path: '/app/customers' },
  { type: 'page', label: 'Work Orders', path: '/app/work-orders' },
  { type: 'page', label: 'Settings', path: '/app/settings' },
  { type: 'page', label: 'ServiceTitan', path: '/app/servicetitan' },
  { type: 'page', label: 'AR Alerts', path: '/app/ar-alerts' },
]

function GlobalSearch() {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<SearchResult[]>([])
  const [open, setOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const navigate = useNavigate()
  const ref = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  // Close on click outside
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  // Keyboard shortcut Ctrl+K
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        inputRef.current?.focus()
        setOpen(true)
      }
      if (e.key === 'Escape') {
        setOpen(false)
        inputRef.current?.blur()
      }
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [])

  useEffect(() => {
    if (!query.trim()) {
      setResults([])
      return
    }

    const q = query.toLowerCase()

    // Search pages
    const pages = pageResults.filter(p => p.label.toLowerCase().includes(q))

    // Search customers & jobs from API
    const timer = setTimeout(async () => {
      setLoading(true)
      try {
        const res = await fetch('/api/customers', { credentials: 'include' })
        if (res.ok) {
          const customers = await res.json()
          const matched = customers
            .filter((c: any) => c.name.toLowerCase().includes(q) || String(c.serviceTitanCustomerId).includes(q))
            .slice(0, 5)
            .map((c: any) => ({
              type: 'customer' as const,
              label: c.name,
              sub: `ST #${c.serviceTitanCustomerId}`,
              path: `/app/customers/${c.id}`,
            }))
          setResults([...pages, ...matched])
        } else {
          setResults(pages)
        }
      } catch {
        setResults(pages)
      } finally {
        setLoading(false)
      }
    }, 250)

    return () => clearTimeout(timer)
  }, [query])

  const handleSelect = (r: SearchResult) => {
    navigate(r.path)
    setQuery('')
    setOpen(false)
  }

  const iconMap = {
    customer: 'icon-[tabler--user]',
    job: 'icon-[tabler--clipboard-list]',
    page: 'icon-[tabler--layout-dashboard]',
  }

  return (
    <div ref={ref} className="relative flex-1">
      <div className="input input-sm bg-base-200/60 border-base-content/10 w-full">
        <span className="icon-[tabler--search] text-base-content/40 size-4 shrink-0" />
        <input
          ref={inputRef}
          type="search"
          placeholder="Search customers, pages..."
          value={query}
          onChange={e => { setQuery(e.target.value); setOpen(true) }}
          onFocus={() => { if (query.trim()) setOpen(true) }}
          className="grow"
        />
        <kbd className="kbd kbd-sm text-base-content/30">Ctrl+K</kbd>
      </div>

      {open && (query.trim() || loading) && (
        <div className="absolute top-full mt-1.5 inset-x-0 z-50 rounded-box bg-base-100 border border-base-content/10 shadow-lg max-h-72 overflow-y-auto">
          {loading && results.length === 0 && (
            <div className="flex items-center justify-center py-4">
              <span className="loading loading-spinner loading-xs text-primary" />
            </div>
          )}
          {!loading && results.length === 0 && query.trim() && (
            <div className="text-sm text-base-content/40 text-center py-4">No results found</div>
          )}
          {results.map((r, i) => (
            <button
              key={i}
              onClick={() => handleSelect(r)}
              className="flex items-center gap-3 w-full px-3 py-2.5 text-left hover:bg-base-200/60 transition-colors"
            >
              <span className={`${iconMap[r.type]} size-4 text-base-content/40 shrink-0`} />
              <div className="min-w-0">
                <div className="text-sm font-medium text-base-content truncate">{r.label}</div>
                {r.sub && <div className="text-xs text-base-content/40">{r.sub}</div>}
              </div>
              <span className="text-xs text-base-content/30 ms-auto shrink-0 capitalize">{r.type}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const { user, logout } = useAuth()
  const { theme, setTheme } = useTheme()
  const location = useLocation()
  const navigate = useNavigate()
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const handleLogout = async () => {
    await logout()
    navigate('/')
  }

  const initials = `${user?.firstName?.[0] ?? ''}${user?.lastName?.[0] ?? ''}`

  return (
    <div className="bg-base-200 flex min-h-screen">

      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-20 bg-black/60 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* -- Sidebar -- */}
      <aside
        className={`fixed inset-y-0 start-0 z-30 flex h-full w-72 flex-col border-e border-base-content/10 bg-base-100 transition-transform duration-200 lg:sticky lg:top-0 lg:h-screen lg:translate-x-0 ${
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        {/* Close button (mobile) */}
        <button
          className="btn btn-text btn-circle btn-sm absolute end-3 top-3 lg:hidden"
          onClick={() => setSidebarOpen(false)}
          aria-label="Close sidebar"
        >
          <span className="icon-[tabler--x] size-4.5" />
        </button>

        {/* Profile header */}
        <div className="flex flex-col items-center gap-3 border-b border-base-content/10 px-4 py-6 text-center">
          {user?.tenant?.logoUrl ? (
            <img
              src={user.tenant.logoUrl}
              alt={user.tenant.name}
              className="max-h-16 max-w-[10rem] object-contain"
            />
          ) : (
            <div className="avatar placeholder">
              <div className="bg-primary text-primary-content rounded-full w-16">
                <span className="text-xl font-semibold">{initials}</span>
              </div>
            </div>
          )}
          <div>
            <h3 className="text-base-content font-semibold">
              {user?.firstName} {user?.lastName}
            </h3>
            <p className="text-base-content/60 text-sm capitalize">{user?.title || user?.role}</p>
            <p className="text-base-content/40 text-xs truncate max-w-[180px]">{user?.tenant?.name}</p>
          </div>
        </div>

        {/* Nav */}
        <nav className="flex-1 overflow-y-auto p-3">
          <ul className="menu menu-sm gap-0.5 p-0">
            <li className="menu-title text-xs uppercase tracking-wider opacity-50 px-2 pt-2 pb-1">Main</li>
            {navItems.map((item) => {
              const active = location.pathname.startsWith(item.path)
              return (
                <li key={item.path}>
                  <Link to={item.path} onClick={() => setSidebarOpen(false)} className={active ? 'menu-active' : ''}>
                    <span className={`${item.icon} size-4.5`} />
                    {item.label}
                  </Link>
                </li>
              )
            })}
            <li className="menu-title text-xs uppercase tracking-wider opacity-50 px-2 pt-4 pb-1">Settings</li>
            <li>
              <Link to="/app/settings" onClick={() => setSidebarOpen(false)} className={location.pathname.startsWith('/app/settings') ? 'menu-active' : ''}>
                <span className="icon-[tabler--settings] size-4.5" />
                Settings
              </Link>
            </li>
            <li>
              <Link to="/app/servicetitan" onClick={() => setSidebarOpen(false)} className={location.pathname.startsWith('/app/servicetitan') ? 'menu-active' : ''}>
                <span className="icon-[tabler--plug] size-4.5" />
                ServiceTitan
              </Link>
            </li>
          </ul>
        </nav>

        <div className="border-t border-base-content/10 p-3">
          <button onClick={handleLogout} className="btn btn-ghost btn-sm w-full justify-start gap-2 text-base-content/70">
            <span className="icon-[tabler--logout] size-4.5" />
            Sign out
          </button>
        </div>
      </aside>

      <div className="flex flex-1 flex-col min-w-0">
        <header className="navbar sticky top-0 z-20 border-b border-base-content/10 bg-base-100 px-4 lg:px-6">
          <div className="navbar-start gap-2 lg:w-auto lg:flex-none">
            <button className="btn btn-ghost btn-square btn-sm lg:hidden" onClick={() => setSidebarOpen(true)} aria-label="Open menu">
              <span className="icon-[tabler--menu-2] size-5" />
            </button>
            <span className="text-base-content font-semibold lg:hidden">MyServiceAO</span>
          </div>
          <div className="navbar-center hidden lg:flex flex-1 px-4">
            <GlobalSearch />
          </div>
          <div className="navbar-end gap-2 lg:w-auto lg:flex-none">
            <div className="dropdown dropdown-end">
              <button tabIndex={0} className="btn btn-ghost btn-square btn-sm" aria-label="Switch theme">
                <span className={`size-5 ${THEMES.find(t => t.value === theme)?.icon ?? 'icon-[tabler--moon]'}`} />
              </button>
              <ul tabIndex={0} className="dropdown-menu dropdown-open:opacity-100 hidden w-40 space-y-0.5 p-2">
                {THEMES.map((t) => (
                  <li key={t.value}>
                    <button className={`dropdown-item flex items-center gap-2 w-full ${theme === t.value ? 'dropdown-active' : ''}`} onClick={() => setTheme(t.value)}>
                      <span className={`${t.icon} size-4`} />
                      {t.label}
                      {theme === t.value && <span className="icon-[tabler--check] size-4 ms-auto text-primary" />}
                    </button>
                  </li>
                ))}
              </ul>
            </div>
            <div className="avatar placeholder">
              <div className="bg-primary text-primary-content rounded-full w-8">
                <span className="text-xs font-semibold">{initials}</span>
              </div>
            </div>
          </div>
        </header>
        <main className="flex-1 overflow-y-auto p-4 lg:p-6">
          {children}
        </main>
      </div>
    </div>
  )
}

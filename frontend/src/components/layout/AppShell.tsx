import { useState, useEffect, useRef } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '@/hooks/useAuth'
import { useTheme, type Theme } from '@/hooks/useTheme'

interface NavItem {
  label: string
  icon: string
  path: string
}

interface NavGroup {
  label: string
  icon: string
  children: NavItem[]
}

type NavEntry = NavItem | NavGroup


const THEME_OPTIONS: { value: string; label: string; group: 'dark' | 'light' }[] = [
  { value: 'dark', label: 'Dark', group: 'dark' },
  { value: 'black', label: 'Black', group: 'dark' },
  { value: 'shadcn', label: 'Shadcn', group: 'dark' },
  { value: 'vscode', label: 'VS Code', group: 'dark' },
  { value: 'spotify', label: 'Spotify', group: 'dark' },
  { value: 'slack', label: 'Slack', group: 'dark' },
  { value: 'valorant', label: 'Valorant', group: 'dark' },
  { value: 'claude', label: 'Claude', group: 'dark' },
  { value: 'luxury', label: 'Luxury', group: 'dark' },
  { value: 'light', label: 'Light', group: 'light' },
  { value: 'corporate', label: 'Corporate', group: 'light' },
  { value: 'soft', label: 'Soft', group: 'light' },
  { value: 'pastel', label: 'Pastel', group: 'light' },
  { value: 'gourmet', label: 'Gourmet', group: 'light' },
  { value: 'ghibli', label: 'Ghibli', group: 'light' },
  { value: 'mintlify', label: 'Mintlify', group: 'light' },
  { value: 'perplexity', label: 'Perplexity', group: 'light' },
]

function isGroup(entry: NavEntry): entry is NavGroup {
  return 'children' in entry
}

const navItems: NavEntry[] = [
  {
    label: 'Dashboard',
    icon: 'icon-[tabler--layout-dashboard]',
    path: '/app/dashboard',
  },
  {
    label: 'PMs/Maintenances',
    icon: 'icon-[tabler--tool]',
    children: [
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
        label: 'PM Planner',
        icon: 'icon-[tabler--map-pin]',
        path: '/app/pm-planner',
      },
    ],
  },
  {
    label: 'Work Orders',
    icon: 'icon-[tabler--clipboard-list]',
    children: [
      {
        label: 'WO Board',
        icon: 'icon-[tabler--layout-board]',
        path: '/app/wo-board',
      },
      {
        label: 'Hold Board',
        icon: 'icon-[tabler--hand-stop]',
        path: '/app/hold-board',
      },
    ],
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

// Legacy THEMES removed, using THEME_OPTIONS
const _LEGACY_THEMES: { value: Theme; label: string; icon: string }[] = [
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
  { type: 'page', label: 'PM Outreach', path: '/app/pm-outreach' },
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
  const [openMenus, setOpenMenus] = useState<Record<number, boolean>>({})

  const toggleMenu = (idx: number) => {
    setOpenMenus(prev => ({ ...prev, [idx]: !prev[idx] }))
  }

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
            <div className="avatar avatar-placeholder">
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
            {navItems.map((entry, idx) => {
              if (isGroup(entry)) {
                const groupActive = entry.children.some(c => location.pathname.startsWith(c.path))
                const isOpen = openMenus[idx] !== undefined ? openMenus[idx] : groupActive
                return (
                  <li key={idx}>
                    <button
                      type="button"
                      onClick={() => toggleMenu(idx)}
                      className={`inline-flex w-full items-center gap-2 rounded-lg p-2 text-sm font-medium hover:bg-base-content/5 transition-colors ${groupActive ? 'text-primary' : 'text-base-content/80'}`}
                    >
                      <span className={`${entry.icon} size-4.5`} />
                      <span className="flex-1 text-left">{entry.label}</span>
                      <span className={`icon-[tabler--chevron-right] size-4 shrink-0 transition-transform duration-200 ${isOpen ? 'rotate-90' : ''}`} />
                    </button>
                    <div
                      className="overflow-hidden transition-all duration-200 ease-in-out"
                      style={{ maxHeight: isOpen ? '200px' : '0px', opacity: isOpen ? 1 : 0 }}
                    >
                      <ul className="menu menu-sm ps-7 pe-0 py-1 gap-0.5">
                        {entry.children.map((child) => {
                          const childActive = location.pathname.startsWith(child.path)
                          return (
                            <li key={child.path}>
                              <Link to={child.path} onClick={() => setSidebarOpen(false)} className={childActive ? 'menu-active' : ''}>
                                <span className={`${child.icon} size-4`} />
                                {child.label}
                              </Link>
                            </li>
                          )
                        })}
                      </ul>
                    </div>
                  </li>
                )
              }
              const item = entry as NavItem
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
                <div className="flex items-center gap-2 p-2 text-sm text-base-content/60">
                  <span className="icon-[tabler--palette] size-4.5" />
                  <select
                    value={theme}
                    onChange={e => setTheme(e.target.value as Theme)}
                    className="select select-xs select-ghost flex-1 text-xs"
                  >
                    <optgroup label="Dark Themes">
                      {THEME_OPTIONS.filter(t => t.group === 'dark').map(t => (
                        <option key={t.value} value={t.value}>{t.label}</option>
                      ))}
                    </optgroup>
                    <optgroup label="Light Themes">
                      {THEME_OPTIONS.filter(t => t.group === 'light').map(t => (
                        <option key={t.value} value={t.value}>{t.label}</option>
                      ))}
                    </optgroup>
                  </select>
                </div>
              </li>
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
            <div className="avatar avatar-placeholder">
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

import { useState } from 'react'
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

      {/* ── Sidebar ── */}
      <aside
        className={`fixed inset-y-0 start-0 z-30 flex h-full w-72 flex-col border-e border-base-content/10 bg-base-100 transition-transform duration-200 lg:relative lg:translate-x-0 ${
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
          <div className="avatar placeholder">
            <div className="bg-primary text-primary-content rounded-full w-16">
              <span className="text-xl font-semibold">{initials}</span>
            </div>
          </div>
          <div>
            <h3 className="text-base-content font-semibold">
              {user?.firstName} {user?.lastName}
            </h3>
            <p className="text-base-content/60 text-sm capitalize">{user?.role}</p>
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
          <div className="navbar-start gap-2">
            <button className="btn btn-ghost btn-square btn-sm lg:hidden" onClick={() => setSidebarOpen(true)} aria-label="Open menu">
              <span className="icon-[tabler--menu-2] size-5" />
            </button>
            <span className="text-base-content font-semibold lg:hidden">MyServiceAO</span>
          </div>
          <div className="navbar-end gap-2">
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

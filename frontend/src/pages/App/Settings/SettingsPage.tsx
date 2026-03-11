import { useTheme, type Theme } from '@/hooks/useTheme'

const THEMES: { value: Theme; label: string; description: string; icon: string }[] = [
  {
    value: 'dark',
    label: 'Dark',
    description: 'Easy on the eyes, great for night work.',
    icon: 'icon-[tabler--moon]',
  },
  {
    value: 'light',
    label: 'Light',
    description: 'Clean and bright for daytime use.',
    icon: 'icon-[tabler--sun]',
  },
  {
    value: 'corporate',
    label: 'Corporate',
    description: 'Professional blue tones for presentations.',
    icon: 'icon-[tabler--building]',
  },
]

export function SettingsPage() {
  const { theme, setTheme } = useTheme()

  return (
    <div className="max-w-2xl mx-auto space-y-8 py-2">
      {/* Page header */}
      <div>
        <h1 className="text-base-content text-2xl font-semibold">Settings</h1>
        <p className="text-base-content/60 text-sm mt-1">
          Manage your account preferences.
        </p>
      </div>

      {/* Theme section */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-5">
          <div>
            <h2 className="text-base-content font-semibold text-base">Appearance</h2>
            <p className="text-base-content/60 text-sm mt-0.5">Choose how MyServiceAO looks for you.</p>
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            {THEMES.map((t) => {
              const active = theme === t.value
              return (
                <button
                  key={t.value}
                  onClick={() => setTheme(t.value)}
                  className={`rounded-box border-2 p-4 text-left transition-all ${
                    active
                      ? 'border-primary bg-primary/5'
                      : 'border-base-content/10 hover:border-base-content/30 bg-base-200/50'
                  }`}
                >
                  <div className="flex items-center gap-2 mb-2">
                    <span className={`${t.icon} size-5 ${active ? 'text-primary' : 'text-base-content/60'}`} />
                    <span className={`font-medium text-sm ${active ? 'text-primary' : 'text-base-content'}`}>
                      {t.label}
                    </span>
                    {active && (
                      <span className="icon-[tabler--check] size-4 text-primary ms-auto" />
                    )}
                  </div>
                  <p className="text-xs text-base-content/50">{t.description}</p>
                </button>
              )
            })}
          </div>
        </div>
      </div>

      {/* Placeholder sections */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-2">
          <h2 className="text-base-content font-semibold text-base">Account</h2>
          <p className="text-base-content/40 text-sm">Profile and password settings coming soon.</p>
        </div>
      </div>

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-2">
          <h2 className="text-base-content font-semibold text-base">Integrations</h2>
          <p className="text-base-content/40 text-sm">Manage your connected services.</p>
          <a href="/app/servicetitan" className="link link-primary text-sm w-fit">
            ServiceTitan settings →
          </a>
        </div>
      </div>
    </div>
  )
}

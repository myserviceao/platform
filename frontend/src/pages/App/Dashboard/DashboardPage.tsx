import { useAuth } from '@/hooks/useAuth'
import { DollarSign, ClipboardList, AlertCircle, CalendarClock, TrendingUp, Wrench } from 'lucide-react'

const kpis = [
  { label: 'Open Work Orders', value: '—', icon: ClipboardList, color: 'text-primary', bg: 'bg-primary/10' },
  { label: 'AR Balance', value: '—', icon: DollarSign, color: 'text-yellow-400', bg: 'bg-yellow-400/10' },
  { label: 'Net Position', value: '—', icon: TrendingUp, color: 'text-green-400', bg: 'bg-green-400/10' },
  { label: 'Month Revenue', value: '—', icon: DollarSign, color: 'text-green-400', bg: 'bg-green-400/10' },
  { label: 'Overdue PMs', value: '—', icon: CalendarClock, color: 'text-red-400', bg: 'bg-red-400/10' },
  { label: 'AR Alerts', value: '—', icon: AlertCircle, color: 'text-orange-400', bg: 'bg-orange-400/10' },
]

export function DashboardPage() {
  const { user } = useAuth()

  return (
    <div>
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-foreground">
          Good {getTimeOfDay()}, {user?.firstName}
        </h1>
        <p className="text-sm text-muted-foreground mt-0.5">
          {user?.tenant.name} · {new Date().toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })}
        </p>
      </div>

      {/* KPI tiles */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 mb-6">
        {kpis.map((kpi) => (
          <div key={kpi.label} className="bg-card border border-border rounded-xl p-4">
            <div className={`w-8 h-8 rounded-md ${kpi.bg} flex items-center justify-center mb-3`}>
              <kpi.icon className={`w-4 h-4 ${kpi.color}`} />
            </div>
            <div className={`text-2xl font-bold ${kpi.color}`}>{kpi.value}</div>
            <div className="text-xs text-muted-foreground mt-1 leading-tight">{kpi.label}</div>
          </div>
        ))}
      </div>

      {/* Connect ServiceTitan banner */}
      <div className="bg-card border border-border rounded-xl p-5 flex flex-col sm:flex-row items-start sm:items-center gap-4">
        <div className="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
          <Wrench className="w-5 h-5 text-primary" />
        </div>
        <div className="flex-1">
          <h3 className="font-medium text-foreground text-sm">Connect ServiceTitan</h3>
          <p className="text-xs text-muted-foreground mt-0.5">
            Link your ServiceTitan account to start syncing jobs, customers, and invoices.
          </p>
        </div>
        <button className="px-4 py-2 rounded-md bg-primary text-white text-sm font-medium hover:bg-primary/90 transition-colors shrink-0">
          Connect now
        </button>
      </div>
    </div>
  )
}

function getTimeOfDay() {
  const h = new Date().getHours()
  if (h < 12) return 'morning'
  if (h < 17) return 'afternoon'
  return 'evening'
}

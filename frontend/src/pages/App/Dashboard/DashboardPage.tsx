import { useState, useEffect, useCallback } from 'react'

// ── Types ──────────────────────────────────────────────────────────────────
interface ArBucket {
  bucket0_30: number
  bucket31_60: number
  bucket61_90: number
  bucket90Plus: number
}
interface ArCustomer {
  customerName: string
  totalOwed: number
  oldestInvoiceDays: number
}
interface OpenJob {
  stJobId: number
  jobNumber: string
  customerName: string
  status: string
  totalAmount: number
  createdOn: string | null
}
interface OverduePm {
  customerName: string
  lastPmDate: string | null
  daysSince: number
}
interface ScheduleItem {
  jobNumber: string
  customerName: string
  start: string
  techs: string[]
}
interface DaySchedule {
  count: number
  items: ScheduleItem[]
}

interface DashboardData {
  synced: boolean
  snapshotTakenAt: string | null
  totalAR: number
  oldestWoDays: number
  aRaging: ArBucket
  aRbyCustomer: ArCustomer[]
  revenueThisMonth: number
  revenueLastMonth: number
  daysInMonth: number
  daysElapsed: number
  openWorkOrders: OpenJob[]
  openWoCount: number
  overduePms: OverduePm[]
  overduePmCount: number
  scheduledToday: DaySchedule
  scheduledTomorrow: DaySchedule
  scheduledDayAfter: DaySchedule
  todayLabel: string
  tomorrowLabel: string
  dayAfterLabel: string
}

// ── Formatters ─────────────────────────────────────────────────────────────
function fmt(n: number) {
  if (n >= 1000000) return `$${(n / 1000000).toFixed(1)}M`
  if (n >= 1000) return `$${(n / 1000).toFixed(1)}k`
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(n)
}
function fmtFull(n: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 2 }).format(n)
}
function revPct(curr: number, prev: number) {
  if (prev === 0) return null
  const p = ((curr - prev) / prev) * 100
  return { pct: Math.abs(p).toFixed(1), up: p >= 0 }
}
function agePct(bucket: number, total: number) {
  return total === 0 ? 0 : Math.round((bucket / total) * 100)
}
function fmtTime(utcIso: string) {
  try {
    return new Date(utcIso).toLocaleTimeString('en-US', {
      hour: 'numeric', minute: '2-digit', hour12: true,
      timeZone: 'America/Chicago'
    })
  } catch { return '' }
}
function daysSince(createdOn: string | null) {
  if (!createdOn) return 0
  return Math.floor((Date.now() - new Date(createdOn).getTime()) / 86400000)
}

// ── Main ───────────────────────────────────────────────────────────────────
export function DashboardPage() {
  const [data, setData] = useState<DashboardData | null>(null)
  const [loading, setLoading] = useState(true)
  const [syncing, setSyncing] = useState(false)
  const [error, setError] = useState('')
  const [schedTab, setSchedTab] = useState<'today' | 'tomorrow' | 'dayafter'>('today')

  const fetchDashboard = useCallback(async () => {
    const res = await fetch('/api/dashboard', { credentials: 'include' })
    if (res.ok) setData(await res.json())
  }, [])

  useEffect(() => {
    fetchDashboard().finally(() => setLoading(false))
  }, [fetchDashboard])

  const handleSync = async () => {
    setSyncing(true)
    setError('')
    try {
      const res = await fetch('/api/dashboard/sync', { method: 'POST', credentials: 'include' })
      const json = await res.json()
      if (!res.ok) { setError(json.error || 'Sync failed'); return }
      setData(json)
    } catch {
      setError('Network error — please try again')
    } finally {
      setSyncing(false)
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <span className="loading loading-spinner loading-md text-primary" />
      </div>
    )
  }

  const d = data
  const change = d ? revPct(d.revenueThisMonth, d.revenueLastMonth) : null
  const arTotal = d?.totalAR ?? 0
  const aging = d?.aRaging

  const activeSchedule: DaySchedule | null =
    schedTab === 'today'    ? (d?.scheduledToday    ?? null) :
    schedTab === 'tomorrow' ? (d?.scheduledTomorrow ?? null) :
                              (d?.scheduledDayAfter ?? null)

  // Stat items for the unified card
  const stats = [
    {
      icon: 'icon-[tabler--currency-dollar]',
      iconBg: arTotal > 0 ? 'bg-warning/10' : 'bg-success/10',
      iconColor: arTotal > 0 ? 'text-warning' : 'text-success',
      label: 'Total AR',
      value: fmt(arTotal),
      sub: arTotal > 0 ? `${d?.aRbyCustomer?.length ?? 0} customers` : 'All paid up',
    },
    {
      icon: 'icon-[tabler--chart-line]',
      iconBg: 'bg-success/10',
      iconColor: 'text-success',
      label: 'Month Revenue',
      value: fmt(d?.revenueThisMonth ?? 0),
      sub: change ? `${change.up ? '↑' : '↓'} ${change.pct}% vs last month` : `Last: ${fmt(d?.revenueLastMonth ?? 0)}`,
      subColor: change ? (change.up ? 'text-success' : 'text-error') : undefined,
    },
    {
      icon: 'icon-[tabler--clipboard-list]',
      iconBg: 'bg-primary/10',
      iconColor: 'text-primary',
      label: 'Open Work Orders',
      value: (d?.openWoCount ?? 0).toString(),
      sub: d?.oldestWoDays ? `oldest ${d.oldestWoDays}d` : 'None open',
    },
    {
      icon: 'icon-[tabler--alert-triangle]',
      iconBg: (d?.overduePmCount ?? 0) > 0 ? 'bg-error/10' : 'bg-success/10',
      iconColor: (d?.overduePmCount ?? 0) > 0 ? 'text-error' : 'text-success',
      label: 'Overdue PMs',
      value: (d?.overduePmCount ?? 0).toString(),
      sub: (d?.overduePmCount ?? 0) > 0 ? `${d!.overduePmCount} past due` : 'All current',
    },
  ]

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-base-content">Dashboard</h1>
          {d?.snapshotTakenAt && (
            <p className="text-xs text-base-content/40 mt-0.5">
              Last synced {new Date(d.snapshotTakenAt).toLocaleString()}
            </p>
          )}
        </div>
        <button onClick={handleSync} disabled={syncing} className="btn btn-primary btn-sm gap-1.5">
          <span className={`icon-[tabler--refresh] size-4 ${syncing ? 'animate-spin' : ''}`} />
          {syncing ? 'Syncing...' : 'Sync Data'}
        </button>
      </div>

      {/* Error */}
      {error && (
        <div className="alert alert-soft alert-error text-sm">
          <span className="icon-[tabler--alert-circle] size-4 shrink-0" />
          {error}
        </div>
      )}

      {/* Not synced yet */}
      {!d?.synced && (
        <div className="alert bg-primary/10 border border-primary/20">
          <span className="icon-[tabler--plug] size-5 text-primary shrink-0" />
          <div className="flex-1">
            <p className="font-medium text-base-content">Connect ServiceTitan to see live data</p>
            <p className="text-base-content/60 text-sm">Hit Sync Data after connecting to populate your dashboard.</p>
          </div>
          <a href="/app/servicetitan" className="btn btn-primary btn-sm whitespace-nowrap">Connect &#x2192;</a>
        </div>
      )}

      {/* ── Combined Stat Card ── */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body p-0">
          <div className="grid grid-cols-2 lg:grid-cols-4 divide-x divide-base-content/10">
            {stats.map((s, i) => (
              <div key={i} className="flex items-center gap-3 px-5 py-4">
                <div className={`flex items-center justify-center size-10 rounded-lg ${s.iconBg} shrink-0`}>
                  <span className={`${s.icon} size-5 ${s.iconColor}`} />
                </div>
                <div className="min-w-0">
                  <p className="text-xs font-medium text-base-content/50 truncate">{s.label}</p>
                  <p className="text-xl font-bold text-base-content leading-tight">{s.value}</p>
                  <p className={`text-xs ${s.subColor ?? 'text-base-content/40'} truncate`}>{s.sub}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Schedule Strip */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body p-0">
          <div className="flex border-b border-base-200 px-4 pt-3 gap-1">
            {(['today', 'tomorrow', 'dayafter'] as const).map((tab) => {
              const label = tab === 'today' ? (d?.todayLabel ?? 'Today') :
                            tab === 'tomorrow' ? (d?.tomorrowLabel ?? 'Tomorrow') :
                            (d?.dayAfterLabel ?? 'Day After')
              const count = tab === 'today'    ? d?.scheduledToday?.count :
                            tab === 'tomorrow' ? d?.scheduledTomorrow?.count :
                            d?.scheduledDayAfter?.count
              return (
                <button
                  key={tab}
                  onClick={() => setSchedTab(tab)}
                  className={`px-3 py-1.5 text-sm font-medium rounded-t border-b-2 transition-colors flex items-center gap-1.5
                    ${schedTab === tab
                      ? 'border-b-primary text-primary bg-primary/5'
                      : 'border-b-transparent text-base-content/50 hover:text-base-content'}`}
                >
                  {label}
                  {count !== undefined && (
                    <span className={`badge badge-sm ${schedTab === tab ? 'badge-primary' : 'bg-base-200 text-base-content/50'}`}>
                      {count}
                    </span>
                  )}
                </button>
              )
            })}
          </div>

          {activeSchedule && activeSchedule.items.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="table table-sm">
                <thead>
                  <tr className="text-xs text-base-content/40 uppercase">
                    <th>Technician</th>
                    <th>Job #</th>
                    <th>Customer</th>
                    <th>Time</th>
                  </tr>
                </thead>
                <tbody>
                  {activeSchedule.items.map((item, i) => (
                    <tr key={i} className="hover:bg-base-200/40">
                      <td className="text-sm">
                        {item.techs?.length > 0
                          ? item.techs.join(', ')
                          : <span className="text-base-content/30 italic">Unassigned</span>}
                      </td>
                      <td className="text-sm font-mono text-primary">{item.jobNumber || '—'}</td>
                      <td className="text-sm font-medium">{item.customerName}</td>
                      <td className="text-sm text-base-content/60">{fmtTime(item.start)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-sm text-base-content/40 px-4 py-4">No appointments scheduled</p>
          )}
        </div>
      </div>

      {/* Bottom 3-column layout */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">

        {/* Col 1: Open Work Orders */}
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-0">
            <div className="flex items-center justify-between px-4 pt-4 pb-3 border-b border-base-200">
              <h3 className="font-semibold text-sm text-base-content flex items-center gap-2">
                Active Work Orders
                <span className="badge badge-sm badge-ghost">{d?.openWoCount ?? 0}</span>
              </h3>
            </div>
            <div className="overflow-auto max-h-96">
              {d?.openWorkOrders && d.openWorkOrders.length > 0 ? (
                <table className="table table-sm">
                  <thead>
                    <tr className="text-xs text-base-content/40 uppercase">
                      <th>Customer</th>
                      <th className="text-right">Job#</th>
                      <th className="text-right">Age</th>
                    </tr>
                  </thead>
                  <tbody>
                    {d.openWorkOrders.map((job, i) => {
                      const age = daysSince(job.createdOn)
                      return (
                        <tr key={i} className="hover:bg-base-200/40">
                          <td className="text-sm font-medium max-w-[8rem] truncate">{job.customerName}</td>
                          <td className="text-sm font-mono text-primary text-right">{job.jobNumber || '—'}</td>
                          <td className={`text-sm text-right font-medium ${age >= 90 ? 'text-error' : age >= 30 ? 'text-warning' : 'text-base-content/60'}`}>
                            {age}d
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              ) : (
                <p className="text-sm text-base-content/40 px-4 py-4">No open work orders</p>
              )}
            </div>
          </div>
        </div>

        {/* Col 2: AR Aging */}
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-0">
            <div className="flex items-center justify-between px-4 pt-4 pb-3 border-b border-base-200">
              <h3 className="font-semibold text-sm text-base-content">AR Aging</h3>
              <span className="text-sm text-base-content/50">{fmtFull(arTotal)} outstanding</span>
            </div>

            {aging && (
              <div className="px-4 py-3 space-y-2 border-b border-base-200">
                {[
                  { label: '0–30d',  val: aging.bucket0_30,   color: 'bg-success' },
                  { label: '31–60d', val: aging.bucket31_60,  color: 'bg-warning' },
                  { label: '61–90d', val: aging.bucket61_90,  color: 'bg-orange-400' },
                  { label: '90d+',        val: aging.bucket90Plus, color: 'bg-error' },
                ].map(({ label, val, color }) => (
                  <div key={label} className="flex items-center gap-2">
                    <span className="text-xs text-base-content/50 w-12 shrink-0">{label}</span>
                    <div className="flex-1 h-2 bg-base-200 rounded-full overflow-hidden">
                      <div
                        className={`h-full ${color} rounded-full transition-all`}
                        style={{ width: `${agePct(val, arTotal)}%` }}
                      />
                    </div>
                    <span className="text-xs font-medium text-base-content/70 w-16 text-right shrink-0">{fmt(val)}</span>
                  </div>
                ))}
              </div>
            )}

            <div className="overflow-auto max-h-64">
              {d?.aRbyCustomer && d.aRbyCustomer.length > 0 ? (
                <table className="table table-sm">
                  <thead>
                    <tr className="text-xs text-base-content/40 uppercase">
                      <th>Customer</th>
                      <th className="text-right">Amount Owed</th>
                    </tr>
                  </thead>
                  <tbody>
                    {d.aRbyCustomer.map((ar, i) => (
                      <tr key={i} className="hover:bg-base-200/40">
                        <td>
                          <span className={`text-sm font-medium ${ar.oldestInvoiceDays > 90 ? 'text-error' : ar.oldestInvoiceDays > 60 ? 'text-warning' : 'text-base-content'}`}>
                            {ar.customerName}
                          </span>
                          <span className="text-xs text-base-content/40 ml-1">{ar.oldestInvoiceDays}d</span>
                        </td>
                        <td className="text-sm font-semibold text-right">{fmtFull(ar.totalOwed)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p className="text-sm text-base-content/40 px-4 py-4">No outstanding AR</p>
              )}
            </div>
          </div>
        </div>

        {/* Col 3: Overdue PMs */}
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-0">
            <div className="flex items-center justify-between px-4 pt-4 pb-3 border-b border-base-200">
              <h3 className="font-semibold text-sm text-base-content flex items-center gap-2">
                Overdue PMs
                {(d?.overduePmCount ?? 0) > 0 && (
                  <span className="badge badge-sm badge-error badge-soft">{d!.overduePmCount}</span>
                )}
              </h3>
              <a href="/app/pm-tracker" className="text-xs text-primary hover:underline">PM Tracker &#x2192;</a>
            </div>
            <div className="overflow-auto max-h-96">
              {d?.overduePms && d.overduePms.length > 0 ? (
                <ul className="divide-y divide-base-200">
                  {d.overduePms.map((pm, i) => (
                    <li key={i} className="flex items-center justify-between px-4 py-2.5 hover:bg-base-200/40">
                      <div className="flex items-center gap-2">
                        <span className="size-2 rounded-full bg-error shrink-0" />
                        <span className="text-sm font-medium">{pm.customerName}</span>
                      </div>
                      <span className="text-xs text-base-content/40">{pm.daysSince}d ago</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-sm text-base-content/40 px-4 py-4">No overdue PMs &#x1F389;</p>
              )}
            </div>
          </div>
        </div>

      </div>
    </div>
  )
}

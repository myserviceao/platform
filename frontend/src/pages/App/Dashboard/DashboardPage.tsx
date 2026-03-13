import { useState, useEffect, useCallback } from 'react'
import { WeatherCard } from './WeatherCard'

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

interface ApVendorSummary {
  vendorName: string
  totalOwed: number
  invoiceCount: number
  nextDue: string
}
interface ApSummary {
  totalAp: number
  nextDueDate: string | null
  nextDueDays: number
  vendorCount: number
  byVendor: ApVendorSummary[]
}
interface ScheduleItem {
  jobNumber: string
  customerName: string
  locationName: string
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
  arOldestDays: number
  oldestWoDays: number
  aRaging: ArBucket
  aRbyCustomer: ArCustomer[]
  revenueThisMonth: number
  revenueLastMonth: number
  totalAP: number
  apNextDueDays: number
  netPosition: number
  daysInMonth: number
  daysElapsed: number
  openWorkOrders: OpenJob[]
  openWoCount: number
  needToSchedule: number
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
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(n)
}
function fmtShort(n: number) {
  if (n >= 1000000) return `$${(n / 1000000).toFixed(1)}M`
  if (n >= 1000) return `$${(n / 1000).toFixed(1)}k`
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(n)
}
function revPct(curr: number, prev: number) {
  if (prev === 0) return null
  const p = ((curr - prev) / prev) * 100
  return { pct: Math.abs(p).toFixed(0), up: p >= 0 }
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
function daysSinceStr(createdOn: string | null) {
  if (!createdOn) return 0
  return Math.floor((Date.now() - new Date(createdOn).getTime()) / 86400000)
}

// ── Stat Tile (Patriot-style with colored top border) ──────────────────────
function StatTile({ borderColor, label, value, sub, subColor }: {
  borderColor: string
  label: string
  value: string
  sub: string
  subColor?: string
}) {
  return (
    <div className={`rounded-box border border-base-content/10 bg-base-100 border-t-[3px] ${borderColor} p-4`}>
      <p className="text-[10px] font-semibold text-base-content/50 uppercase tracking-widest mb-1">{label}</p>
      <p className="text-2xl font-bold text-base-content leading-tight">{value}</p>
      <p className={`text-xs mt-1 ${subColor ?? 'text-base-content/40'}`}>{sub}</p>
    </div>
  )
}

// ── Main ───────────────────────────────────────────────────────────────────
export function DashboardPage() {
  const [data, setData] = useState<DashboardData | null>(null)
  const [loading, setLoading] = useState(true)
  const [syncing, setSyncing] = useState(false)
  const [error, setError] = useState('')
  const [schedTab, setSchedTab] = useState<'today' | 'tomorrow' | 'dayafter'>('today')
  const [apData, setApData] = useState<ApSummary | null>(null)

  const fetchDashboard = useCallback(async () => {
    const [dRes, apRes] = await Promise.all([
      fetch('/api/dashboard', { credentials: 'include' }),
      fetch('/api/ap/summary', { credentials: 'include' }),
    ])
    if (dRes.ok) setData(await dRes.json())
    if (apRes.ok) setApData(await apRes.json())
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
  const arTotal = d?.totalAR ?? 0
  const aging = d?.aRaging
  const change = d ? revPct(d.revenueThisMonth, d.revenueLastMonth) : null
  const prevMonthName = new Date(Date.now() - 30 * 86400000).toLocaleString('en-US', { month: 'short' })


  const activeSchedule: DaySchedule | null =
    schedTab === 'today'    ? (d?.scheduledToday    ?? null) :
    schedTab === 'tomorrow' ? (d?.scheduledTomorrow ?? null) :
                              (d?.scheduledDayAfter ?? null)

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

      {error && (
        <div className="alert alert-soft alert-error text-sm">
          <span className="icon-[tabler--alert-circle] size-4 shrink-0" />
          {error}
        </div>
      )}

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

      {/* ── Row 1: Financial KPIs (4 cards) ── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatTile
          borderColor="border-t-blue-500"
          label="Total AR"
          value={fmt(arTotal)}
          sub={arTotal > 0 ? `oldest ${d?.arOldestDays ?? 0}+ days` : 'all paid up'}
        />
        <StatTile
          borderColor="border-t-cyan-400"
          label="Total AP"
          value={fmt(d?.totalAP ?? 0)}
          sub={(d?.totalAP ?? 0) > 0 ? `next due ${d?.apNextDueDays ?? 0}d` : 'nothing owed'}
        />
        <StatTile
          borderColor="border-t-emerald-400"
          label="Net Position"
          value={fmt(d?.netPosition ?? 0)}
          sub={(d?.totalAP ?? 0) > 0 ? `AR covers AP ${((arTotal / (d?.totalAP ?? 1))).toFixed(1)}x` : '—'}
          subColor={(d?.netPosition ?? 0) >= 0 ? 'text-success' : 'text-error'}
        />
        <StatTile
          borderColor="border-t-green-500"
          label="Month Revenue"
          value={fmt(d?.revenueThisMonth ?? 0)}
          sub={change ? `${change.up ? '↑' : '↓'} ${change.pct}% vs ${prevMonthName}` : `Last: ${fmtShort(d?.revenueLastMonth ?? 0)}`}
          subColor={change ? (change.up ? 'text-success' : 'text-error') : undefined}
        />
      </div>

      {/* ── Row 2: Ops KPIs (3 cards) ── */}
      <div className="grid grid-cols-3 gap-3">
        <StatTile
          borderColor="border-t-red-500"
          label="Open WOs"
          value={(d?.openWoCount ?? 0).toString()}
          sub={d?.oldestWoDays ? `oldest ${d.oldestWoDays}d open` : 'none open'}
        />
        <StatTile
          borderColor="border-t-amber-400"
          label="Need to Schedule"
          value={(d?.needToSchedule ?? 0).toString()}
          sub="unbooked jobs"
        />
        <StatTile
          borderColor="border-t-fuchsia-500"
          label="Overdue PMs"
          value={(d?.overduePmCount ?? 0).toString()}
          sub={d?.overduePms?.length ? `longest ${d.overduePms[0]?.daysSince ?? 0}d ago` : 'all current'}
        />
      </div>

      {/* ── Weather + Schedule ── */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-3">
        <WeatherCard />
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
            <div className="divide-y divide-base-200">
              <div className="flex items-center gap-3 px-4 py-2 text-[10px] uppercase tracking-wider text-base-content/40 font-semibold">
                <span className="w-36 shrink-0">Technician</span>
                <span className="w-14 shrink-0">Job</span>
                <span className="flex-1">Location</span>
                <span className="shrink-0">Time</span>
              </div>
              {(() => {
                const techMap = new Map<string, typeof activeSchedule.items>()
                activeSchedule.items.forEach(item => {
                  const techName = item.techs?.length > 0 ? item.techs.join(', ') : 'Unassigned'
                  if (!techMap.has(techName)) techMap.set(techName, [])
                  techMap.get(techName)!.push(item)
                })
                return Array.from(techMap.entries()).map(([tech, items]) => {
                  if (items.length === 1) {
                    const item = items[0]
                    return (
                      <div key={tech} className="flex items-center gap-3 px-4 py-2.5 hover:bg-base-200/30">
                        <span className={'text-sm font-medium w-36 truncate shrink-0 ' + (tech === 'Unassigned' ? 'text-base-content/30 italic' : '')}>{tech}</span>
                        <span className="font-mono text-primary text-xs shrink-0">#{item.jobNumber}</span>
                        <span className="text-sm truncate flex-1 text-base-content/70">{item.locationName || item.customerName}</span>
                        <span className="text-xs text-base-content/40 shrink-0">{fmtTime(item.start)}</span>
                      </div>
                    )
                  }
                  return (
                    <details key={tech} className="group">
                      <summary className="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-base-200/40 list-none">
                        <span className="text-sm font-medium w-36 truncate shrink-0">{tech}</span>
                        <span className="icon-[tabler--chevron-down] size-3.5 text-base-content/30 transition-transform group-open:rotate-180" />
                        <span className="badge badge-xs badge-primary">{items.length}</span>
                        <span className="flex-1" />
                      </summary>
                      <div className="pb-1 bg-base-200/20">
                        {items.map((item, i) => (
                          <div key={i} className="flex items-center gap-3 px-4 pl-[11.5rem] py-1.5 text-sm">
                            <span className="font-mono text-primary text-xs shrink-0">#{item.jobNumber}</span>
                            <span className="truncate flex-1 text-base-content/70">{item.locationName || item.customerName}</span>
                            <span className="text-xs text-base-content/40 shrink-0">{fmtTime(item.start)}</span>
                          </div>
                        ))}
                      </div>
                    </details>
                  )
                })
              })()}
            </div>
          ) : (
            <p className="text-sm text-base-content/40 px-4 py-4">No appointments scheduled</p>
          )}
        </div>
      </div>

      </div>

      {/* Bottom layout */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">

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
                      const age = daysSinceStr(job.createdOn)
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
              <span className="text-sm text-base-content/50">{fmt(arTotal)} outstanding</span>
            </div>

            {aging && (
              <div className="px-4 py-3 space-y-2 border-b border-base-200">
                {[
                  { label: '0–30d',  val: aging.bucket0_30,   color: 'bg-success' },
                  { label: '31–60d', val: aging.bucket31_60,  color: 'bg-warning' },
                  { label: '61–90d', val: aging.bucket61_90,  color: 'bg-orange-400' },
                  { label: '90d+',   val: aging.bucket90Plus, color: 'bg-error' },
                ].map(({ label, val, color }) => (
                  <div key={label} className="flex items-center gap-2">
                    <span className="text-xs text-base-content/50 w-12 shrink-0">{label}</span>
                    <div className="flex-1 h-2 bg-base-200 rounded-full overflow-hidden">
                      <div
                        className={`h-full ${color} rounded-full transition-all`}
                        style={{ width: `${agePct(val, arTotal)}%` }}
                      />
                    </div>
                    <span className="text-xs font-medium text-base-content/70 w-16 text-right shrink-0">{fmtShort(val)}</span>
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
                        <td className="text-sm font-semibold text-right">{fmt(ar.totalOwed)}</td>
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


        {/* Accounts Payable */}
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-0">
            <div className="flex items-center justify-between px-4 pt-4 pb-3 border-b border-base-200">
              <h3 className="font-semibold text-sm text-base-content">Accounts Payable</h3>
              <a href="/app/ap" className="text-xs text-primary hover:underline">View All →</a>
            </div>
            <div className="overflow-auto max-h-64">
              {apData?.byVendor && apData.byVendor.length > 0 ? (
                <table className="table table-sm">
                  <thead>
                    <tr className="text-xs text-base-content/40 uppercase">
                      <th>Vendor</th>
                      <th className="text-right">Total Owed</th>
                      <th className="text-right">Next Due</th>
                    </tr>
                  </thead>
                  <tbody>
                    {apData.byVendor.map((v, i) => (
                      <tr key={i} className="hover:bg-base-200/40">
                        <td className="text-sm font-medium text-base-content">{v.vendorName}</td>
                        <td className="text-sm font-semibold text-right">{fmtShort(v.totalOwed)}</td>
                        <td className="text-sm text-base-content/60 text-right">{new Date(v.nextDue).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p className="text-sm text-base-content/40 px-4 py-4">No outstanding AP</p>
              )}
            </div>
          </div>
        </div>

        {/* Overdue PMs */}
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

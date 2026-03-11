import { useState, useEffect, useCallback } from 'react'

interface DashboardSnapshot {
  synced: boolean
  revenueThisMonth: number
  revenueLastMonth: number
  accountsReceivable: number
  unpaidInvoiceCount: number
  openJobCount: number
  overduePmCount: number
  snapshotTakenAt: string | null
}

function fmt(n: number) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(n)
}

function revChange(current: number, previous: number) {
  if (previous === 0) return null
  const pct = ((current - previous) / previous) * 100
  return { pct: Math.abs(pct).toFixed(1), up: pct >= 0 }
}

interface KpiCardProps {
  title: string
  value: string
  sub?: string
  trend?: { pct: string; up: boolean } | null
  alert?: boolean
  icon: string
}

function KpiCard({ title, value, sub, trend, alert, icon }: KpiCardProps) {
  return (
    <div className={`card shadow-sm ${alert ? 'border border-error/30 bg-error/5' : 'bg-base-100'}`}>
      <div className="card-body gap-3 p-5">
        <div className="flex items-center justify-between">
          <span className="text-xs font-medium text-base-content/50 uppercase tracking-wider">{title}</span>
          <div className={`avatar avatar-placeholder`}>
            <div className={`rounded-field size-9 ${alert ? 'bg-error/15 text-error' : 'bg-primary/10 text-primary'}`}>
              <span className={`${icon} size-4.5`} />
            </div>
          </div>
        </div>
        <div className={`text-3xl font-bold ${alert ? 'text-error' : 'text-base-content'}`}>{value}</div>
        {trend && (
          <span className={`text-xs font-medium ${trend.up ? 'text-success' : 'text-error'}`}>
            <span className={`icon-[tabler--trending-${trend.up ? 'up' : 'down'}] size-3.5 inline me-0.5`} />
            {trend.pct}% vs last month
          </span>
        )}
        {sub && <span className="text-xs text-base-content/40">{sub}</span>}
      </div>
    </div>
  )
}

export function DashboardPage() {
  const [data, setData] = useState<DashboardSnapshot | null>(null)
  const [loading, setLoading] = useState(true)
  const [syncing, setSyncing] = useState(false)
  const [error, setError] = useState('')

  const fetchSnapshot = useCallback(async () => {
    const res = await fetch('/api/dashboard/snapshot', { credentials: 'include' })
    if (res.ok) setData(await res.json())
  }, [])

  useEffect(() => {
    fetchSnapshot().finally(() => setLoading(false))
  }, [fetchSnapshot])

  const handleSync = async () => {
    setSyncing(true)
    setError('')
    try {
      const res = await fetch('/api/dashboard/sync', {
        method: 'POST',
        credentials: 'include',
      })
      const json = await res.json()
      if (!res.ok) setError(json.error || 'Sync failed')
      else setData(json)
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

  const change = data ? revChange(data.revenueThisMonth, data.revenueLastMonth) : null

  return (
    <div className="max-w-6xl mx-auto space-y-6 py-2">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-base-content text-2xl font-semibold">Dashboard</h1>
          {data?.snapshotTakenAt && (
            <p className="text-xs text-base-content/40 mt-1">
              Last synced {new Date(data.snapshotTakenAt).toLocaleString()}
            </p>
          )}
        </div>
        <button
          onClick={handleSync}
          disabled={syncing}
          className="btn btn-primary btn-sm gap-2"
        >
          <span className={`icon-[tabler--refresh] size-4 ${syncing ? 'animate-spin' : ''}`} />
          {syncing ? 'Syncing...' : 'Sync Now'}
        </button>
      </div>

      {/* Error */}
      {error && (
        <div className="alert alert-soft alert-error text-sm">
          <span className="icon-[tabler--alert-circle] size-4 shrink-0" />
          {error}
        </div>
      )}

      {/* Not connected banner */}
      {!data?.synced && (
        <div className="alert bg-primary/10 border border-primary/20 text-primary-content">
          <span className="icon-[tabler--plug] size-5 text-primary shrink-0" />
          <div className="flex-1">
            <p className="font-medium text-base-content">Connect ServiceTitan to see live data</p>
            <p className="text-base-content/60 text-sm">
              Your dashboard will populate automatically after connecting.
            </p>
          </div>
          <a href="/app/servicetitan" className="btn btn-primary btn-sm whitespace-nowrap">
            Connect →
          </a>
        </div>
      )}

      {/* KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
        <KpiCard
          title="Revenue This Month"
          value={fmt(data?.revenueThisMonth ?? 0)}
          trend={change}
          sub={`Last month: ${fmt(data?.revenueLastMonth ?? 0)}`}
          icon="icon-[tabler--currency-dollar]"
        />
        <KpiCard
          title="Accounts Receivable"
          value={fmt(data?.accountsReceivable ?? 0)}
          sub={`${data?.unpaidInvoiceCount ?? 0} unpaid invoice${(data?.unpaidInvoiceCount ?? 0) !== 1 ? 's' : ''}`}
          alert={(data?.accountsReceivable ?? 0) > 0}
          icon="icon-[tabler--receipt]"
        />
        <KpiCard
          title="Open Work Orders"
          value={(data?.openJobCount ?? 0).toString()}
          sub="Jobs not yet completed or canceled"
          icon="icon-[tabler--clipboard-list]"
        />
        <KpiCard
          title="Overdue PMs"
          value={(data?.overduePmCount ?? 0).toString()}
          sub="Recurring services past due date"
          alert={(data?.overduePmCount ?? 0) > 0}
          icon="icon-[tabler--calendar-x]"
        />
      </div>
    </div>
  )
}

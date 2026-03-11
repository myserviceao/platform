import { useState, useEffect, useCallback } from 'react'

interface PmCustomer {
  stCustomerId: number
  customerName: string
  lastPmDate: string | null
  pmStatus: 'Overdue' | 'ComingDue' | 'Current'
  updatedAt: string
}

function daysAgo(dateStr: string | null): number | null {
  if (!dateStr) return null
  return Math.floor((Date.now() - new Date(dateStr).getTime()) / (1000 * 60 * 60 * 24))
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return 'Never'
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}

type TabKey = 'Overdue' | 'ComingDue' | 'Current' | 'All'

export function PmTrackerPage() {
  const [customers, setCustomers] = useState<PmCustomer[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [activeTab, setActiveTab] = useState<TabKey>('Overdue')
  const [search, setSearch] = useState('')

  const fetchData = useCallback(async () => {
    const res = await fetch('/api/dashboard/pm-tracker', { credentials: 'include' })
    if (res.ok) setCustomers(await res.json())
    else setError('Failed to load PM tracker data')
  }, [])

  useEffect(() => {
    fetchData().finally(() => setLoading(false))
  }, [fetchData])

  const overdue = customers.filter((c) => c.pmStatus === 'Overdue')
  const comingDue = customers.filter((c) => c.pmStatus === 'ComingDue')
  const current = customers.filter((c) => c.pmStatus === 'Current')

  const tabs: { key: TabKey; label: string; count: number }[] = [
    { key: 'Overdue', label: 'Overdue', count: overdue.length },
    { key: 'ComingDue', label: 'Coming Due', count: comingDue.length },
    { key: 'Current', label: 'Current', count: current.length },
    { key: 'All', label: 'All', count: customers.length },
  ]

  const tabData: Record<TabKey, PmCustomer[]> = {
    Overdue: overdue,
    ComingDue: comingDue,
    Current: current,
    All: customers,
  }

  const filtered = tabData[activeTab].filter((c) =>
    c.customerName.toLowerCase().includes(search.toLowerCase())
  )

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <span className="loading loading-spinner loading-md text-primary" />
      </div>
    )
  }

  return (
    <div className="max-w-5xl mx-auto space-y-6 py-2">
      <div>
        <h1 className="text-base-content text-2xl font-semibold">PM Tracker</h1>
        <p className="text-base-content/60 text-sm mt-1">
          Preventive maintenance status for all customers based on completed PM jobs in ServiceTitan.
        </p>
      </div>

      <div className="grid grid-cols-3 gap-4">
        {(
          [
            { key: 'Overdue' as TabKey, label: 'Overdue', count: overdue.length, desc: 'Last PM 6+ months ago', card: 'border-error/30 bg-error/5', icon: 'icon-[tabler--alert-circle]', iconColor: 'text-error' },
            { key: 'ComingDue' as TabKey, label: 'Coming Due', count: comingDue.length, desc: 'Last PM 4-6 months ago', card: 'border-warning/30 bg-warning/5', icon: 'icon-[tabler--clock]', iconColor: 'text-warning' },
            { key: 'Current' as TabKey, label: 'Current', count: current.length, desc: 'Last PM under 4 months', card: 'border-success/30 bg-success/5', icon: 'icon-[tabler--circle-check]', iconColor: 'text-success' },
          ]
        ).map((s) => (
          <button
            key={s.key}
            onClick={() => setActiveTab(s.key)}
            className={`rounded-box border-2 p-4 text-left transition-all ${
              activeTab === s.key
                ? s.card
                : 'border-base-content/10 bg-base-100 hover:border-base-content/20'
            }`}
          >
            <div className="flex items-center gap-2 mb-1">
              <span className={`${s.icon} size-4 ${s.iconColor}`} />
              <span className="text-xs text-base-content/60 font-medium">{s.label}</span>
            </div>
            <div className="text-2xl font-bold text-base-content">{s.count}</div>
            <div className="text-xs text-base-content/40 mt-0.5">{s.desc}</div>
          </button>
        ))}
      </div>

      <div className="flex items-center justify-between gap-4">
        <div className="tabs tabs-bordered flex-1">
          {tabs.map((tab) => (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={`tab ${activeTab === tab.key ? 'tab-active' : ''}`}
            >
              {tab.label}
              <span className="badge badge-soft badge-sm ms-2">{tab.count}</span>
            </button>
          ))}
        </div>
        <div className="input input-sm max-w-52">
          <span className="icon-[tabler--search] text-base-content/40 size-4 shrink-0" />
          <input
            type="search"
            placeholder="Search customers..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="grow"
          />
        </div>
      </div>

      {error ? (
        <div className="alert alert-soft alert-error text-sm">
          <span className="icon-[tabler--alert-circle] size-4 shrink-0" />
          {error}
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-16 text-base-content/40 text-sm">
          {search ? 'No customers match your search.' : 'No customers in this category.'}
        </div>
      ) : (
        <div className="rounded-box border border-base-content/10 overflow-hidden">
          <table className="table table-sm">
            <thead>
              <tr>
                <th>Customer</th>
                <th>Last PM</th>
                <th>Days Since</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((c) => {
                const days = daysAgo(c.lastPmDate)
                const badgeClass =
                  c.pmStatus === 'Overdue'
                    ? 'badge-error'
                    : c.pmStatus === 'ComingDue'
                    ? 'badge-warning'
                    : 'badge-success'
                return (
                  <tr key={c.stCustomerId} className="hover">
                    <td className="font-medium text-base-content">{c.customerName}</td>
                    <td className="text-base-content/70">{formatDate(c.lastPmDate)}</td>
                    <td className="text-base-content/60">
                      {days !== null ? `${days} days` : '—'}
                    </td>
                    <td>
                      <span className={`badge badge-soft badge-sm ${badgeClass}`}>
                        {c.pmStatus === 'ComingDue' ? 'Coming Due' : c.pmStatus}
                      </span>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {customers.length > 0 && (
        <p className="text-xs text-base-content/40 text-right">
          Data from last sync. Run Sync from the Dashboard to refresh.
        </p>
      )}

      {customers.length === 0 && !loading && (
        <div className="text-center py-16 text-base-content/40 space-y-2">
          <p className="text-lg font-medium">No PM data yet</p>
          <p className="text-sm">Run a sync from the Dashboard to populate PM customer history.</p>
        </div>
      )}
    </div>
  )
}

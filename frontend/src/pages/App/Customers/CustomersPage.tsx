import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'

interface Customer {
  id: string
  name: string
  serviceTitanCustomerId: number
  lastPmDate?: string
  pmStatus: string
  updatedAt: string
}

function daysAgo(date?: string | null) {
  if (!date) return null
  return Math.floor((Date.now() - new Date(date).getTime()) / (1000 * 60 * 60 * 24))
}

function fmtDate(date?: string | null) {
  if (!date) return '—'
  return new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function pmBadgeClass(status: string) {
  switch (status) {
    case 'Overdue': return 'badge-error'
    case 'ComingDue': return 'badge-warning'
    case 'NoPm': return 'badge-ghost'
    default: return 'badge-success'
  }
}

function pmIconClass(status: string) {
  switch (status) {
    case 'Overdue': return 'icon-[tabler--alert-circle] text-error'
    case 'ComingDue': return 'icon-[tabler--clock] text-warning'
    case 'NoPm': return 'icon-[tabler--minus] text-base-content/30'
    default: return 'icon-[tabler--circle-check] text-success'
  }
}

export function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState<'all' | 'overdue' | 'comingdue' | 'nopm'>('all')
  const navigate = useNavigate()

  const fetchCustomers = useCallback(async () => {
    const res = await fetch('/api/customers', { credentials: 'include' })
    if (res.ok) setCustomers(await res.json())
    else setError('Failed to load customers')
  }, [])

  useEffect(() => {
    fetchCustomers().finally(() => setLoading(false))
  }, [fetchCustomers])

  const filtered = customers
    .filter(c => {
      if (filter === 'overdue') return c.pmStatus === 'Overdue'
      if (filter === 'comingdue') return c.pmStatus === 'ComingDue'
      if (filter === 'nopm') return c.pmStatus === 'NoPm'
      return true
    })
    .filter(c => c.name.toLowerCase().includes(search.toLowerCase()))

  const overdueCount = customers.filter(c => c.pmStatus === 'Overdue').length
  const comingDueCount = customers.filter(c => c.pmStatus === 'ComingDue').length
  const noPmCount = customers.filter(c => c.pmStatus === 'NoPm').length

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <span className="loading loading-spinner loading-md text-primary" />
      </div>
    )
  }

  return (
    <div className="max-w-6xl mx-auto space-y-6 py-2">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-base-content">Customers</h1>
          <p className="text-sm text-base-content/60 mt-1">
            {customers.length} customers · {overdueCount} overdue PM
          </p>
        </div>
        <div className="flex gap-3">
          <div className="text-right">
            <div className="text-xs text-base-content/40 uppercase tracking-wide">Overdue PM</div>
            <div className={`text-2xl font-bold ${overdueCount > 0 ? 'text-error' : 'text-base-content'}`}>
              {overdueCount}
            </div>
          </div>
          <div className="text-right">
            <div className="text-xs text-base-content/40 uppercase tracking-wide">Coming Due</div>
            <div className={`text-2xl font-bold ${comingDueCount > 0 ? 'text-warning' : 'text-base-content'}`}>
              {comingDueCount}
            </div>
          </div>
        </div>
      </div>

      {error && (
        <div className="alert alert-soft alert-error text-sm">
          <span className="icon-[tabler--alert-circle] size-4 shrink-0" />
          {error}
        </div>
      )}

      <div className="flex items-center gap-3">
        <div className="input input-sm flex-1 max-w-sm">
          <span className="icon-[tabler--search] text-base-content/40 size-4 shrink-0" />
          <input
            type="search"
            placeholder="Search customers..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="grow"
          />
        </div>
        <div className="tabs tabs-bordered">
          <button onClick={() => setFilter('all')} className={`tab ${filter === 'all' ? 'tab-active' : ''}`}>
            All <span className="badge badge-soft badge-sm ms-2">{customers.length}</span>
          </button>
          <button onClick={() => setFilter('overdue')} className={`tab ${filter === 'overdue' ? 'tab-active' : ''}`}>
            Overdue <span className="badge badge-soft badge-error badge-sm ms-2">{overdueCount}</span>
          </button>
          <button onClick={() => setFilter('comingdue')} className={`tab ${filter === 'comingdue' ? 'tab-active' : ''}`}>
            Coming Due <span className="badge badge-soft badge-warning badge-sm ms-2">{comingDueCount}</span>
          </button>
          <button onClick={() => setFilter('nopm')} className={`tab ${filter === 'nopm' ? 'tab-active' : ''}`}>
            No PM <span className="badge badge-soft badge-sm ms-2">{noPmCount}</span>
          </button>
        </div>
      </div>

      {filtered.length === 0 ? (
        <div className="text-center py-20 text-base-content/40">
          {search ? 'No customers match your search.' : 'No customers found.'}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map(c => {
            const days = daysAgo(c.lastPmDate)
            return (
              <button key={c.id} onClick={() => navigate(`/app/customers/${c.id}`)}
                className="rounded-box border border-base-content/10 bg-base-100 p-4 text-left hover:border-primary/40 hover:bg-base-200 transition-all group"
              >
                <div className="flex items-start justify-between gap-2 mb-3">
                  <div className="flex items-center gap-3">
                    <div className="avatar avatar-placeholder">
                      <div className="bg-primary/20 text-primary rounded-full size-9 text-sm font-semibold">
                        {c.name.trim().charAt(0).toUpperCase()}
                      </div>
                    </div>
                    <div>
                      <div className="font-medium text-base-content text-sm leading-tight group-hover:text-primary transition-colors">{c.name}</div>
                      <div className="text-xs text-base-content/40 mt-0.5">ST #{c.serviceTitanCustomerId}</div>
                    </div>
                  </div>
                  <span className="icon-[tabler--chevron-right] size-4 text-base-content/30 group-hover:text-primary shrink-0 mt-1 transition-colors" />
                </div>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5 text-xs text-base-content/50">
                    <span className={`${pmIconClass(c.pmStatus)} size-3.5`} />
                    Last PM: {c.lastPmDate ? fmtDate(c.lastPmDate) : 'None on file'}
                  </div>
                  <span className={`badge badge-soft badge-xs ${pmBadgeClass(c.pmStatus)}`}>
                    {c.pmStatus === 'ComingDue' ? 'Coming Due' : c.pmStatus === 'NoPm' ? 'No PM' : c.pmStatus}
                  </span>
                </div>
                {days !== null && <div className="text-xs text-base-content/30 mt-1">{days} days ago</div>}
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}

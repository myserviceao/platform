import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'

interface Customer {
  id: string
  name: string
  serviceTitanCustomerId: number
  totalAR: number
  current: number
  days30: number
  days60: number
  days90: number
}

function formatCurrency(n: number) {
  return n === 0 ? '$0' : '$' + n.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })
}

export function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState<'all' | 'owed'>('all')
  const navigate = useNavigate()

  const fetchCustomers = useCallback(async () => {
    const res = await fetch('/customers', { credentials: 'include' })
    if (res.ok) setCustomers(await res.json())
    else setError('Failed to load customers')
  }, [])

  useEffect(() => {
    fetchCustomers().finally(() => setLoading(false))
  }, [fetchCustomers])

  const filtered = customers
    .filter(c => filter === 'all' || c.totalAR > 0)
    .filter(c => c.name.toLowerCase().includes(search.toLowerCase()))

  const totalAR = customers.reduce((sum, c) => sum + c.totalAR, 0)
  const withBalance = customers.filter(c => c.totalAR > 0).length

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
            {customers.length} customers · {withBalance} with open balances
          </p>
        </div>
        <div className="text-right">
          <div className="text-xs text-base-content/40 uppercase tracking-wide">Total AR</div>
          <div className={`text-2xl font-bold ${totalAR > 0 ? 'text-error' : 'text-base-content'}`}>
            {formatCurrency(totalAR)}
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
          <button
            onClick={() => setFilter('all')}
            className={`tab ${filter === 'all' ? 'tab-active' : ''}`}
          >
            All
            <span className="badge badge-soft badge-sm ms-2">{customers.length}</span>
          </button>
          <button
            onClick={() => setFilter('owed')}
            className={`tab ${filter === 'owed' ? 'tab-active' : ''}`}
          >
            With Balance
            <span className="badge badge-soft badge-error badge-sm ms-2">{withBalance}</span>
          </button>
        </div>
      </div>

      {filtered.length === 0 ? (
        <div className="text-center py-20 text-base-content/40">
          {search ? 'No customers match your search.' : 'No customers found.'}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map(c => (
            <button
              key={c.id}
              onClick={() => navigate(`/app/customers/${c.id}`)}
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
                    <div className="font-medium text-base-content text-sm leading-tight group-hover:text-primary transition-colors">
                      {c.name}
                    </div>
                    <div className="text-xs text-base-content/40 mt-0.5">
                      ST #{c.serviceTitanCustomerId}
                    </div>
                  </div>
                </div>
                <span className="icon-[tabler--chevron-right] size-4 text-base-content/30 group-hover:text-primary shrink-0 mt-1 transition-colors" />
              </div>

              {c.totalAR > 0 ? (
                <div className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <span className="text-xs text-base-content/50">Balance Due</span>
                    <span className="text-sm font-semibold text-error">{formatCurrency(c.totalAR)}</span>
                  </div>
                  <div className="grid grid-cols-3 gap-1 text-center">
                    {c.days30 > 0 && (
                      <div className="rounded bg-warning/10 px-1 py-0.5">
                        <div className="text-xs font-medium text-warning">{formatCurrency(c.days30)}</div>
                        <div className="text-[10px] text-base-content/40">30 days</div>
                      </div>
                    )}
                    {c.days60 > 0 && (
                      <div className="rounded bg-error/10 px-1 py-0.5">
                        <div className="text-xs font-medium text-error">{formatCurrency(c.days60)}</div>
                        <div className="text-[10px] text-base-content/40">60 days</div>
                      </div>
                    )}
                    {c.days90 > 0 && (
                      <div className="rounded bg-error/20 px-1 py-0.5">
                        <div className="text-xs font-bold text-error">{formatCurrency(c.days90)}</div>
                        <div className="text-[10px] text-base-content/40">90+ days</div>
                      </div>
                    )}
                  </div>
                </div>
              ) : (
                <div className="flex items-center gap-1.5 text-xs text-success">
                  <span className="icon-[tabler--circle-check] size-3.5" />
                  No open balance
                </div>
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

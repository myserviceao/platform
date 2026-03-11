import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'

interface Customer {
  id: string
  name: string
  serviceTitanCustomerId: number
  totalBalance: number
  openWoCount: number
  lastJobDate?: string
  updatedAt: string
}

function fmt(n: number) {
  return n.toLocaleString('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 0, maximumFractionDigits: 0 })
}

export function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState<'all' | 'withBalance'>('all')
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
      if (filter === 'withBalance') return c.totalBalance > 0
      return true
    })
    .filter(c => c.name.toLowerCase().includes(search.toLowerCase()))

  const withBalanceCount = customers.filter(c => c.totalBalance > 0).length
  const totalAR = customers.reduce((sum, c) => sum + c.totalBalance, 0)

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
            {customers.length} customers
          </p>
        </div>
        {totalAR > 0 && (
          <div className="text-right">
            <div className="text-xs text-base-content/40 uppercase tracking-wide">Total AR</div>
            <div className="text-2xl font-bold text-base-content">{fmt(totalAR)}</div>
          </div>
        )}
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
            onClick={() => setFilter('withBalance')}
            className={`tab ${filter === 'withBalance' ? 'tab-active' : ''}`}
          >
            With Balance
            <span className="badge badge-soft badge-warning badge-sm ms-2">{withBalanceCount}</span>
          </button>
        </div>
      </div>

      {filtered.length === 0 ? (
        <div className="text-center py-20 text-base-content/40">
          {search ? 'No customers match your search.' : 'No customers found.'}
        </div>
      ) : (
        <div className="rounded-box border border-base-content/10 overflow-hidden">
          <table className="table table-sm">
            <thead>
              <tr>
                <th>Customer</th>
                <th className="text-right">Balance</th>
                <th className="text-center">Open WOs</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(c => (
                <tr
                  key={c.id}
                  onClick={() => navigate(`/app/customers/${c.id}`)}
                  className="hover cursor-pointer"
                >
                  <td>
                    <div className="flex items-center gap-3">
                      <div className="avatar avatar-placeholder">
                        <div className="bg-primary/20 text-primary rounded-full size-8 text-xs font-semibold">
                          {c.name.trim().charAt(0).toUpperCase()}
                        </div>
                      </div>
                      <div>
                        <div className="font-medium text-base-content text-sm">{c.name}</div>
                        <div className="text-xs text-base-content/40">ST #{c.serviceTitanCustomerId}</div>
                      </div>
                    </div>
                  </td>
                  <td className="text-right">
                    {c.totalBalance > 0 ? (
                      <span className="font-medium text-warning">{fmt(c.totalBalance)}</span>
                    ) : (
                      <span className="text-base-content/30">$0</span>
                    )}
                  </td>
                  <td className="text-center">
                    {c.openWoCount > 0 ? (
                      <span className="badge badge-soft badge-primary badge-sm">{c.openWoCount}</span>
                    ) : (
                      <span className="text-base-content/30">0</span>
                    )}
                  </td>
                  <td className="text-right">
                    <span className="icon-[tabler--chevron-right] size-4 text-base-content/30" />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

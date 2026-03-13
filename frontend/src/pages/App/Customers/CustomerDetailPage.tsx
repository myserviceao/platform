import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'

interface JobRow {
  jobNumber: string
  jobTypeName: string | null
  technicianName: string | null
  status: string
  createdOn: string | null
  totalAmount: number
}

interface InvoiceRow {
  stInvoiceId: number
  invoiceDate: string
  totalAmount: number
  balanceRemaining: number
}

interface CustomerDetail {
  id: string
  name: string
  address: string | null
  phone: string | null
  email: string | null
  serviceTitanCustomerId: number
  totalBalance: number
  lifetimeSpend: number
  openWoCount: number
  jobCount: number
  lastPmDate: string | null
  pmStatus: string
  jobs: JobRow[]
  invoices: InvoiceRow[]
}

function fmt(n: number) {
  return n.toLocaleString('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 2 })
}

function fmtDate(date?: string | null) {
  if (!date) return '—'
  return new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function invoiceStatus(balance: number, total: number) {
  if (balance <= 0) return { label: 'Paid', cls: 'badge-success' }
  if (balance < total) return { label: 'Partial', cls: 'badge-warning' }
  return { label: 'Open', cls: 'badge-error' }
}

type Tab = 'workorders' | 'invoices' | 'equipment'

export function CustomerDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [customer, setCustomer] = useState<CustomerDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [tab, setTab] = useState<Tab>('workorders')

  const fetchCustomer = useCallback(async () => {
    const res = await fetch(`/api/customers/${id}`, { credentials: 'include' })
    if (res.ok) setCustomer(await res.json())
    else setError('Failed to load customer')
  }, [id])

  useEffect(() => {
    fetchCustomer().finally(() => setLoading(false))
  }, [fetchCustomer])

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <span className="loading loading-spinner loading-md text-primary" />
      </div>
    )
  }

  if (error || !customer) {
    return (
      <div className="max-w-5xl mx-auto py-8">
        <div className="alert alert-soft alert-error">{error || 'Customer not found'}</div>
        <button onClick={() => navigate('/app/customers')} className="btn btn-ghost btn-sm mt-4">
          <span className="icon-[tabler--arrow-left] size-4" /> Back to Customers
        </button>
      </div>
    )
  }

  const tabs: { key: Tab; label: string; count?: number }[] = [
    { key: 'workorders', label: 'Work Orders', count: customer.jobs.length },
    { key: 'invoices', label: 'Invoices', count: customer.invoices.length },
    { key: 'equipment', label: 'Equipment' },
  ]

  return (
    <div className="max-w-5xl mx-auto space-y-6 py-2">

      {/* Back + Header */}
      <div>
        <button
          onClick={() => navigate('/app/customers')}
          className="btn btn-ghost btn-xs text-base-content/50 hover:text-base-content mb-3 -ml-2"
        >
          <span className="icon-[tabler--arrow-left] size-3.5" />
          Customers
        </button>

        <div className="flex items-center gap-4">
          <div className="avatar avatar-placeholder">
            <div className="bg-primary/20 text-primary rounded-full size-14 text-xl font-bold">
              {customer.name.trim().charAt(0).toUpperCase()}
            </div>
          </div>
          <div>
            <h1 className="text-2xl font-semibold text-base-content">{customer.name}</h1>
            <div className="text-sm text-base-content/50 mt-0.5">
              ServiceTitan #{customer.serviceTitanCustomerId}
            </div>
            <div className="flex flex-wrap gap-x-4 gap-y-1 mt-2 text-sm text-base-content/60">
              {customer.address && (
                <span className="flex items-center gap-1.5">
                  <span className="icon-[tabler--map-pin] size-3.5 shrink-0" />
                  {customer.address}
                </span>
              )}
              {customer.phone && (
                <a href={`tel:${customer.phone}`} className="flex items-center gap-1.5 hover:text-primary transition-colors">
                  <span className="icon-[tabler--phone] size-3.5 shrink-0" />
                  {customer.phone}
                </a>
              )}
              {customer.email && (
                <a href={`mailto:${customer.email}`} className="flex items-center gap-1.5 hover:text-primary transition-colors">
                  <span className="icon-[tabler--mail] size-3.5 shrink-0" />
                  {customer.email}
                </a>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
        <div className="rounded-box border border-base-content/10 bg-base-100 p-4">
          <div className="text-xs text-base-content/50 mb-1">Lifetime Spend</div>
          <div className="text-xl font-bold text-base-content">
            {fmt(customer.lifetimeSpend)}
          </div>
        </div>
        <div className="rounded-box border border-base-content/10 bg-base-100 p-4">
          <div className="text-xs text-base-content/50 mb-1">Balance Owed</div>
          <div className={`text-xl font-bold ${customer.totalBalance > 0 ? 'text-warning' : 'text-base-content'}`}>
            {fmt(customer.totalBalance)}
          </div>
        </div>
        <div className="rounded-box border border-base-content/10 bg-base-100 p-4">
          <div className="text-xs text-base-content/50 mb-1">Work Orders</div>
          <div className="text-xl font-bold text-base-content">{customer.jobCount}</div>
        </div>
        <div className="rounded-box border border-base-content/10 bg-base-100 p-4">
          <div className="text-xs text-base-content/50 mb-1">Open WOs</div>
          <div className={`text-xl font-bold ${customer.openWoCount > 0 ? 'text-primary' : 'text-base-content'}`}>
            {customer.openWoCount}
          </div>
        </div>
        <div className="rounded-box border border-base-content/10 bg-base-100 p-4">
          <div className="text-xs text-base-content/50 mb-1">Last PM</div>
          <div className="text-lg font-semibold text-base-content">
            {customer.lastPmDate ? fmtDate(customer.lastPmDate) : '—'}
          </div>
          {customer.pmStatus && customer.pmStatus !== 'NoPm' && (
            <span className={`badge badge-soft badge-xs mt-1 ${
              customer.pmStatus === 'Overdue' ? 'badge-error' :
              customer.pmStatus === 'ComingDue' ? 'badge-warning' : 'badge-success'
            }`}>
              {customer.pmStatus === 'ComingDue' ? 'Coming Due' : customer.pmStatus}
            </span>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div className="tabs tabs-bordered">
        {tabs.map(t => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`tab ${tab === t.key ? 'tab-active' : ''}`}
          >
            {t.label}
            {t.count !== undefined && (
              <span className="badge badge-soft badge-sm ms-2">{t.count}</span>
            )}
          </button>
        ))}
      </div>

      {/* Tab Content */}
      {tab === 'workorders' && (
        customer.jobs.length === 0 ? (
          <div className="text-center py-16 text-base-content/40 text-sm">
            No work orders found for this customer.
          </div>
        ) : (
          <div className="rounded-box border border-base-content/10 overflow-hidden">
            <table className="table table-sm">
              <thead>
                <tr>
                  <th>Job #</th>
                  <th>Type</th>
                  <th>Technician</th>
                  <th>Status</th>
                  <th>Created</th>
                  <th className="text-right">Amount</th>
                </tr>
              </thead>
              <tbody>
                {customer.jobs.map((j, i) => (
                  <tr key={i} className="row-hover">
                    <td className="font-medium text-base-content">{j.jobNumber || '—'}</td>
                    <td className="text-base-content/70">{j.jobTypeName || '—'}</td>
                    <td className="text-base-content/70">{j.technicianName || '—'}</td>
                    <td>
                      <span className={`badge badge-soft badge-xs ${
                        j.status === 'Completed' ? 'badge-success' :
                        j.status === 'InProgress' ? 'badge-primary' :
                        j.status === 'Scheduled' ? 'badge-info' :
                        j.status === 'Hold' ? 'badge-warning' :
                        j.status === 'Canceled' ? 'badge-ghost' : 'badge-ghost'
                      }`}>
                        {j.status || 'Unknown'}
                      </span>
                    </td>
                    <td className="text-base-content/60">{fmtDate(j.createdOn)}</td>
                    <td className="text-right font-medium text-base-content">
                      {j.totalAmount > 0 ? fmt(j.totalAmount) : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      )}

      {tab === 'invoices' && (
        customer.invoices.length === 0 ? (
          <div className="text-center py-16 text-base-content/40 text-sm">
            No invoices found for this customer.
          </div>
        ) : (
          <div className="rounded-box border border-base-content/10 overflow-hidden">
            <table className="table table-sm">
              <thead>
                <tr>
                  <th>Invoice #</th>
                  <th>Issue Date</th>
                  <th className="text-right">Total</th>
                  <th className="text-right">Balance</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {customer.invoices.map((inv, i) => {
                  const s = invoiceStatus(inv.balanceRemaining, inv.totalAmount)
                  return (
                    <tr key={i} className="row-hover">
                      <td className="font-medium text-base-content">#{inv.stInvoiceId}</td>
                      <td className="text-base-content/60">{fmtDate(inv.invoiceDate)}</td>
                      <td className="text-right text-base-content">{fmt(inv.totalAmount)}</td>
                      <td className="text-right">
                        {inv.balanceRemaining > 0 ? (
                          <span className="font-medium text-warning">{fmt(inv.balanceRemaining)}</span>
                        ) : (
                          <span className="text-success">$0.00</span>
                        )}
                      </td>
                      <td>
                        <span className={`badge badge-soft badge-xs ${s.cls}`}>{s.label}</span>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )
      )}

      {tab === 'equipment' && (
        <div className="rounded-box border border-base-content/10 bg-base-100 p-8 text-center">
          <span className="icon-[tabler--tool] size-10 text-base-content/20 mb-3 mx-auto block" />
          <div className="text-sm text-base-content/40">
            Equipment tracking coming soon. This will sync installed equipment from ServiceTitan.
          </div>
        </div>
      )}
    </div>
  )
}

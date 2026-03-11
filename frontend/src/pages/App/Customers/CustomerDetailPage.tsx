import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'

interface Contact { type: string; value: string; memo?: string }
interface Location {
  name: string; street: string; unit?: string; city: string; state: string; zip: string
  contacts: Contact[]
}
interface Invoice {
  invoiceNumber: string; invoiceDate: string; dueDate: string
  totalAmount: number; balanceRemaining: number; status: string
}
interface WorkOrder {
  jobNumber: string; status: string; jobTypeName: string
  createdAt: string; completedAt?: string; totalAmount: number
}
interface Equipment {
  type: string; brand: string; modelNumber?: string; serialNumber?: string
  installDate?: string; warrantyExpiration?: string; warrantyRegistered: boolean; notes?: string
}
interface CustomerProfile {
  id: string
  name: string
  serviceTitanCustomerId: number
  contacts: Contact[]
  locations: Location[]
  invoices: Invoice[]
  workOrders: WorkOrder[]
  equipment: Equipment[]
  lastPm?: { jobNumber: string; jobTypeName: string; completedAt: string }
  balanceOwed: number
}

function fmt(date?: string | null) {
  if (!date) return '—'
  return new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}
function fmtCurrency(n: number) {
  return '$' + n.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })
}
function daysAgo(date?: string | null) {
  if (!date) return null
  return Math.floor((Date.now() - new Date(date).getTime()) / (1000 * 60 * 60 * 24))
}

export function CustomerDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [profile, setProfile] = useState<CustomerProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [activeTab, setActiveTab] = useState<'overview' | 'invoices' | 'work-orders' | 'equipment'>('overview')

  const fetchProfile = useCallback(async () => {
    const res = await fetch(`/api/customers/${id}`, { credentials: 'include' })
    if (res.ok) setProfile(await res.json())
    else setError('Failed to load customer profile')
  }, [id])

  useEffect(() => {
    fetchProfile().finally(() => setLoading(false))
  }, [fetchProfile])

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <span className="loading loading-spinner loading-md text-primary" />
      </div>
    )
  }

  if (error || !profile) {
    return (
      <div className="max-w-4xl mx-auto py-8">
        <div className="alert alert-soft alert-error">{error || 'Customer not found'}</div>
        <button onClick={() => navigate('/app/customers')} className="btn btn-ghost btn-sm mt-4">
          <span className="icon-[tabler--arrow-left] size-4" /> Back to Customers
        </button>
      </div>
    )
  }

  const phones = profile.contacts.filter(c => c.type?.toLowerCase().includes('phone') || c.type?.toLowerCase().includes('mobile'))
  const emails = profile.contacts.filter(c => c.type?.toLowerCase().includes('email'))
  const pmDays = daysAgo(profile.lastPm?.completedAt)
  const openInvoices = profile.invoices.filter(i => i.balanceRemaining > 0)

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

        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-4">
            <div className="avatar avatar-placeholder">
              <div className="bg-primary/20 text-primary rounded-full size-14 text-xl font-bold">
                {profile.name.trim().charAt(0).toUpperCase()}
              </div>
            </div>
            <div>
              <h1 className="text-2xl font-semibold text-base-content">{profile.name}</h1>
              <div className="text-sm text-base-content/50 mt-0.5">
                ServiceTitan #{profile.serviceTitanCustomerId}
              </div>
            </div>
          </div>
          <div className="text-right shrink-0">
            <div className="text-xs text-base-content/40 uppercase tracking-wide">Balance Due</div>
            <div className={`text-2xl font-bold ${profile.balanceOwed > 0 ? 'text-error' : 'text-success'}`}>
              {profile.balanceOwed > 0 ? fmtCurrency(profile.balanceOwed) : 'Paid Up'}
            </div>
          </div>
        </div>
      </div>

      {/* Quick Stats */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <div className="rounded-box border border-base-content/10 bg-base-100 p-3">
          <div className="text-xs text-base-content/50 mb-1">Open Invoices</div>
          <div className={`text-xl font-bold ${openInvoices.length > 0 ? 'text-error' : 'text-base-content'}`}>
            {openInvoices.length}
          </div>
        </div>
        <div className="rounded-box border border-base-content/10 bg-base-100 p-3">
          <div className="text-xs text-base-content/50 mb-1">Service Calls</div>
          <div className="text-xl font-bold text-base-content">{profile.workOrders.length}</div>
        </div>
        <div className="rounded-box border border-base-content/10 bg-base-100 p-3">
          <div className="text-xs text-base-content/50 mb-1">Equipment</div>
          <div className="text-xl font-bold text-base-content">{profile.equipment.length}</div>
        </div>
        <div className="rounded-box border border-base-content/10 bg-base-100 p-3">
          <div className="text-xs text-base-content/50 mb-1">Last PM</div>
          <div className={`text-sm font-semibold ${pmDays && pmDays > 180 ? 'text-error' : pmDays && pmDays > 120 ? 'text-warning' : 'text-success'}`}>
            {pmDays !== null ? `${pmDays}d ago` : 'None on file'}
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="tabs tabs-bordered">
        {(['overview', 'invoices', 'work-orders', 'equipment'] as const).map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`tab ${activeTab === tab ? 'tab-active' : ''}`}
          >
            {tab === 'work-orders' ? 'Work Orders' : tab.charAt(0).toUpperCase() + tab.slice(1)}
            {tab === 'invoices' && openInvoices.length > 0 && (
              <span className="badge badge-soft badge-error badge-sm ms-2">{openInvoices.length}</span>
            )}
          </button>
        ))}
      </div>

      {/* Overview Tab */}
      {activeTab === 'overview' && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">

          {/* Contact Info */}
          <div className="rounded-box border border-base-content/10 bg-base-100 p-4 space-y-3">
            <h3 className="text-sm font-semibold text-base-content/70 uppercase tracking-wide">Contact Info</h3>
            {phones.length === 0 && emails.length === 0 ? (
              <p className="text-sm text-base-content/40">No contacts on file</p>
            ) : (
              <div className="space-y-2">
                {phones.map((c, i) => (
                  <div key={i} className="flex items-center gap-2">
                    <span className="icon-[tabler--phone] size-4 text-base-content/40 shrink-0" />
                    <div>
                      <a href={`tel:${c.value}`} className="text-sm text-base-content hover:text-primary">{c.value}</a>
                      {c.memo && <div className="text-xs text-base-content/40">{c.memo}</div>}
                    </div>
                  </div>
                ))}
                {emails.map((c, i) => (
                  <div key={i} className="flex items-center gap-2">
                    <span className="icon-[tabler--mail] size-4 text-base-content/40 shrink-0" />
                    <div>
                      <a href={`mailto:${c.value}`} className="text-sm text-base-content hover:text-primary">{c.value}</a>
                      {c.memo && <div className="text-xs text-base-content/40">{c.memo}</div>}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Last PM */}
          <div className="rounded-box border border-base-content/10 bg-base-100 p-4 space-y-3">
            <h3 className="text-sm font-semibold text-base-content/70 uppercase tracking-wide">Last PM</h3>
            {profile.lastPm ? (
              <div className="space-y-1">
                <div className="text-sm font-medium text-base-content">{profile.lastPm.jobTypeName}</div>
                <div className="text-sm text-base-content/60">{fmt(profile.lastPm.completedAt)}</div>
                <div className="text-xs text-base-content/40">Job #{profile.lastPm.jobNumber}</div>
                {pmDays !== null && (
                  <div className={`badge badge-soft badge-sm mt-1 ${pmDays > 180 ? 'badge-error' : pmDays > 120 ? 'badge-warning' : 'badge-success'}`}>
                    {pmDays} days ago
                  </div>
                )}
              </div>
            ) : (
              <p className="text-sm text-base-content/40">No PM on record</p>
            )}
          </div>

          {/* Service Locations */}
          <div className="rounded-box border border-base-content/10 bg-base-100 p-4 space-y-3 md:col-span-2">
            <h3 className="text-sm font-semibold text-base-content/70 uppercase tracking-wide">
              Service Locations ({profile.locations.length})
            </h3>
            {profile.locations.length === 0 ? (
              <p className="text-sm text-base-content/40">No locations on file</p>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {profile.locations.map((loc, i) => (
                  <div key={i} className="rounded-lg border border-base-content/10 p-3 space-y-1">
                    <div className="text-sm font-medium text-base-content">{loc.name || 'Service Location'}</div>
                    <div className="flex items-start gap-1.5 text-sm text-base-content/60">
                      <span className="icon-[tabler--map-pin] size-3.5 shrink-0 mt-0.5" />
                      <span>{[loc.street, loc.unit, loc.city, loc.state, loc.zip].filter(Boolean).join(', ')}</span>
                    </div>
                    {loc.contacts.map((c, j) => (
                      <div key={j} className="flex items-center gap-1.5 text-xs text-base-content/50">
                        <span className={`${c.type?.toLowerCase().includes('email') ? 'icon-[tabler--mail]' : 'icon-[tabler--phone]'} size-3`} />
                        {c.value}
                      </div>
                    ))}
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Invoices Tab */}
      {activeTab === 'invoices' && (
        <div className="rounded-box border border-base-content/10 overflow-hidden">
          {profile.invoices.length === 0 ? (
            <div className="text-center py-12 text-base-content/40 text-sm">No invoices on file</div>
          ) : (
            <table className="table table-sm">
              <thead>
                <tr>
                  <th>Invoice #</th>
                  <th>Date</th>
                  <th>Due</th>
                  <th className="text-right">Total</th>
                  <th className="text-right">Balance</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {profile.invoices.map((inv, i) => (
                  <tr key={i} className="hover">
                    <td className="font-medium text-base-content">{inv.invoiceNumber}</td>
                    <td className="text-base-content/60">{fmt(inv.invoiceDate)}</td>
                    <td className="text-base-content/60">{fmt(inv.dueDate)}</td>
                    <td className="text-right text-base-content">{fmtCurrency(inv.totalAmount)}</td>
                    <td className={`text-right font-semibold ${inv.balanceRemaining > 0 ? 'text-error' : 'text-success'}`}>
                      {inv.balanceRemaining > 0 ? fmtCurrency(inv.balanceRemaining) : '—'}
                    </td>
                    <td>
                      <span className={`badge badge-soft badge-sm ${
                        inv.status?.toLowerCase() === 'paid' ? 'badge-success' :
                        inv.status?.toLowerCase() === 'posted' ? 'badge-warning' : 'badge-ghost'
                      }`}>
                        {inv.status || 'Unknown'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Work Orders Tab */}
      {activeTab === 'work-orders' && (
        <div className="rounded-box border border-base-content/10 overflow-hidden">
          {profile.workOrders.length === 0 ? (
            <div className="text-center py-12 text-base-content/40 text-sm">No work orders on file</div>
          ) : (
            <table className="table table-sm">
              <thead>
                <tr>
                  <th>Job #</th>
                  <th>Type</th>
                  <th>Date</th>
                  <th>Completed</th>
                  <th className="text-right">Amount</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {profile.workOrders.map((wo, i) => (
                  <tr key={i} className="hover">
                    <td className="font-medium text-base-content">{wo.jobNumber}</td>
                    <td className="text-base-content/70">{wo.jobTypeName || '—'}</td>
                    <td className="text-base-content/60">{fmt(wo.createdAt)}</td>
                    <td className="text-base-content/60">{fmt(wo.completedAt)}</td>
                    <td className="text-right text-base-content">{wo.totalAmount > 0 ? fmtCurrency(wo.totalAmount) : '—'}</td>
                    <td>
                      <span className={`badge badge-soft badge-sm ${
                        wo.status?.toLowerCase() === 'completed' ? 'badge-success' :
                        wo.status?.toLowerCase() === 'in progress' ? 'badge-warning' :
                        wo.status?.toLowerCase() === 'canceled' ? 'badge-ghost' : 'badge-info'
                      }`}>
                        {wo.status || 'Unknown'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Equipment Tab */}
      {activeTab === 'equipment' && (
        <div>
          {profile.equipment.length === 0 ? (
            <div className="text-center py-12 text-base-content/40 text-sm">No equipment on file</div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {profile.equipment.map((eq, i) => {
                const warrantyExpired = eq.warrantyExpiration && new Date(eq.warrantyExpiration) < new Date()
                return (
                  <div key={i} className="rounded-box border border-base-content/10 bg-base-100 p-4 space-y-2">
                    <div className="flex items-start justify-between">
                      <div>
                        <div className="text-sm font-semibold text-base-content">{eq.brand} {eq.type}</div>
                        {eq.modelNumber && <div className="text-xs text-base-content/50">Model: {eq.modelNumber}</div>}
                        {eq.serialNumber && <div className="text-xs text-base-content/50">S/N: {eq.serialNumber}</div>}
                      </div>
                      <span className={`icon-[tabler--tool] size-5 text-base-content/20`} />
                    </div>
                    <div className="flex flex-wrap gap-2 text-xs text-base-content/50">
                      {eq.installDate && <span>Installed {fmt(eq.installDate)}</span>}
                      {eq.warrantyExpiration && (
                        <span className={warrantyExpired ? 'text-error' : 'text-success'}>
                          Warranty {warrantyExpired ? 'expired' : 'until'} {fmt(eq.warrantyExpiration)}
                        </span>
                      )}
                    </div>
                    {eq.notes && <div className="text-xs text-base-content/40 border-t border-base-content/10 pt-2">{eq.notes}</div>}
                  </div>
                )
              })}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

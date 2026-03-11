import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'

interface CustomerDetail {
  id: string
  name: string
  serviceTitanCustomerId: number
  lastPmDate?: string
  pmStatus: string
  updatedAt: string
}

function fmt(date?: string | null) {
  if (!date) return '—'
  return new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function daysAgo(date?: string | null) {
  if (!date) return null
  return Math.floor((Date.now() - new Date(date).getTime()) / (1000 * 60 * 60 * 24))
}

function pmBadgeClass(status: string) {
  switch (status) {
    case 'Overdue': return 'badge-error'
    case 'ComingDue': return 'badge-warning'
    case 'NoPm': return 'badge-ghost'
    default: return 'badge-success'
  }
}

export function CustomerDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [customer, setCustomer] = useState<CustomerDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const fetchCustomer = useCallback(async () => {
    const res = await fetch(`/api/customers/${id}`, { credentials: 'include' })
    if (res.ok) setCustomer(await res.json())
    else setError('Failed to load customer')
  }, [id])

  useEffect(() => {
    fetchCustomer().finally(() => setLoading(false))
  }, [fetchCustomer])

  if (loading) return (<div className="flex items-center justify-center py-20"><span className="loading loading-spinner loading-md text-primary" /></div>)

  if (error || !customer) return (
    <div className="max-w-4xl mx-auto py-8">
      <div className="alert alert-soft alert-error">{error || 'Customer not found'}</div>
      <button onClick={() => navigate('/app/customers')} className="btn btn-ghost btn-sm mt-4">
        <span className="icon-[tabler--arrow-left] size-4" /> Back to Customers
      </button>
    </div>
  )

  const pmDays = daysAgo(customer.lastPmDate)

  return (
    <div className="max-w-4xl mx-auto space-y-6 py-2">
      <div>
        <button onClick={() => navigate('/app/customers')} className="btn btn-ghost btn-xs text-base-content/50 hover:text-base-content mb-3 -ml-2">
          <span className="icon-[tabler--arrow-left] size-3.5" /> Customers
        </button>
        <div className="flex items-center gap-4">
          <div className="avatar avatar-placeholder">
            <div className="bg-primary/20 text-primary rounded-full size-14 text-xl font-bold">
              {customer.name.trim().charAt(0).toUpperCase()}
            </div>
          </div>
          <div>
            <h1 className="text-2xl font-semibold text-base-content">{customer.name}</h1>
            <div className="text-sm text-base-content/50 mt-0.5">ServiceTitan #{customer.serviceTitanCustomerId}</div>
          </div>
        </div>
      </div>

      <div className="rounded-box border border-base-content/10 bg-base-100 p-5">
        <h3 className="text-sm font-semibold text-base-content/60 uppercase tracking-wide mb-4">PM Status</h3>
        <div className="flex items-center justify-between">
          <div className="space-y-1">
            <div className="text-sm text-base-content/60">Last Preventive Maintenance</div>
            <div className="text-xl font-semibold text-base-content">
              {customer.lastPmDate ? fmt(customer.lastPmDate) : 'None on file'}
            </div>
            {pmDays !== null && <div className="text-sm text-base-content/50">{pmDays} days ago</div>}
          </div>
          <span className={`badge badge-soft badge-lg ${pmBadgeClass(customer.pmStatus)}`}>
            {customer.pmStatus === 'ComingDue' ? 'Coming Due' : customer.pmStatus === 'NoPm' ? 'No PM on Record' : customer.pmStatus}
          </span>
        </div>
      </div>

      <div className="rounded-box border border-base-content/10 bg-base-100 p-8 text-center">
        <span className="icon-[tabler--building] size-10 text-base-content/20 mb-3 mx-auto block" />
        <div className="text-sm text-base-content/40">Invoices, work orders, and equipment will be available once AR sync is added.</div>
      </div>
    </div>
  )
}

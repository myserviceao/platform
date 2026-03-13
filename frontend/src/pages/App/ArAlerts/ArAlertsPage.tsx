import { useState, useEffect, useCallback } from 'react'

interface AgingSummary {
  total: number
  totalCount: number
  days15: { amount: number; count: number }
  days30: { amount: number; count: number }
  days60: { amount: number; count: number }
  days90: { amount: number; count: number }
  overdueCustomerCount: number
}

interface ArCustomer {
  customerId: number
  customerName: string
  customerPhone: string | null
  customerEmail: string | null
  totalOwed: number
  invoiceCount: number
  oldestDays: number
  status: string
  paymentPlanAmount: number | null
  paymentPlanNote: string | null
  lastContact: string | null
}

interface ArInvoice {
  stInvoiceId: number
  invoiceDate: string
  totalAmount: number
  balanceRemaining: number
  ageDays: number
}

interface ContactLog {
  id: number
  contactType: string
  outcome: string
  notes: string | null
  followUpDate: string | null
  createdAt: string
}

interface CustomerDetail {
  customerId: number
  customerName: string
  customerPhone: string | null
  customerEmail: string | null
  status: string
  paymentPlanAmount: number | null
  paymentPlanNote: string | null
  invoices: ArInvoice[]
  contactLogs: ContactLog[]
}

const fmt = (n: number) => n.toLocaleString('en-US', { style: 'currency', currency: 'USD' })

const STATUS_LABELS: Record<string, { label: string; color: string }> = {
  active: { label: 'Active', color: 'badge-primary' },
  payment_plan: { label: 'Payment Plan', color: 'badge-info' },
  sent_to_collections: { label: 'Collections', color: 'badge-error' },
  written_off: { label: 'Written Off', color: 'badge-neutral' },
}

const OUTCOME_LABELS: Record<string, string> = {
  left_voicemail: 'Left Voicemail',
  spoke_with: 'Spoke With',
  promised_to_pay: 'Promised to Pay',
  disputed: 'Disputed',
  no_answer: 'No Answer',
  wrong_number: 'Wrong Number',
}

export function ArAlertsPage() {
  const [summary, setSummary] = useState<AgingSummary | null>(null)
  const [customers, setCustomers] = useState<ArCustomer[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [bucket, setBucket] = useState<string>('')
  const [statusFilter, setStatusFilter] = useState('all')
  const [loading, setLoading] = useState(true)
  const [expandedId, setExpandedId] = useState<number | null>(null)
  const [detail, setDetail] = useState<CustomerDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [showContactModal, setShowContactModal] = useState(false)
  const [showStatusModal, setShowStatusModal] = useState(false)
  const [contactCustomerId, setContactCustomerId] = useState<number>(0)
  const [statusCustomerId, setStatusCustomerId] = useState<number>(0)
  const pageSize = 25

  const loadSummary = useCallback(async () => {
    try {
      const r = await fetch('/api/ar-alerts/summary', { credentials: 'include' })
      if (r.ok) setSummary(await r.json())
    } catch { /* ignore */ }
  }, [])

  const loadCustomers = useCallback(async () => {
    setLoading(true)
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
      if (search) params.set('search', search)
      if (bucket) params.set('bucket', bucket)
      if (statusFilter && statusFilter !== 'all') params.set('status', statusFilter)
      const r = await fetch(`/api/ar-alerts/customers?${params}`, { credentials: 'include' })
      if (r.ok) {
        const data = await r.json()
        setCustomers(data.items)
        setTotal(data.total)
      }
    } catch { /* ignore */ }
    finally { setLoading(false) }
  }, [page, search, bucket, statusFilter])

  useEffect(() => { loadSummary() }, [loadSummary])
  useEffect(() => { loadCustomers() }, [loadCustomers])

  const loadDetail = async (customerId: number) => {
    if (expandedId === customerId) {
      setExpandedId(null)
      setDetail(null)
      return
    }
    setExpandedId(customerId)
    setDetailLoading(true)
    try {
      const r = await fetch(`/api/ar-alerts/customers/${customerId}`, { credentials: 'include' })
      if (r.ok) setDetail(await r.json())
    } catch { /* ignore */ }
    finally { setDetailLoading(false) }
  }

  const openContactModal = (customerId: number) => {
    setContactCustomerId(customerId)
    setShowContactModal(true)
  }

  const openStatusModal = (customerId: number) => {
    setStatusCustomerId(customerId)
    setShowStatusModal(true)
  }

  const sendReminder = (c: ArCustomer, channel: 'email' | 'sms') => {
    if (channel === 'email') {
      if (!c.customerEmail) { alert('No email on file for this customer.'); return }
      const subject = encodeURIComponent(`Outstanding Balance - ${c.customerName}`)
      const body = encodeURIComponent(
        `Hi ${c.customerName},\n\nThis is a reminder about your outstanding balance of ${fmt(c.totalOwed)} with ${c.invoiceCount} open invoice(s).\n\nPlease contact us to arrange payment.\n\nThank you`
      )
      window.location.href = `mailto:${c.customerEmail}?subject=${subject}&body=${body}`
    } else {
      if (!c.customerPhone) { alert('No phone number on file for this customer.'); return }
      const body = encodeURIComponent(
        `Hi ${c.customerName}, you have an outstanding balance of ${fmt(c.totalOwed)}. Please contact us to arrange payment.`
      )
      window.location.href = `sms:${c.customerPhone}?body=${body}`
    }
  }

  const ageBadge = (days: number) => {
    if (days >= 90) return 'badge-error'
    if (days >= 60) return 'badge-warning'
    if (days >= 30) return 'badge-info'
    if (days >= 15) return 'badge-primary'
    return 'badge-neutral'
  }

  const totalPages = Math.ceil(total / pageSize)

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-base-content">AR Alerts</h1>
          <p className="text-sm text-base-content/60">Collections workflow &amp; accounts receivable tracking</p>
        </div>
        {summary && summary.overdueCustomerCount > 0 && (
          <div className="badge badge-error badge-soft gap-1">
            <span className="icon-[tabler--alert-circle] size-3.5" />
            {summary.overdueCustomerCount} customer{summary.overdueCustomerCount !== 1 ? 's' : ''} overdue
          </div>
        )}
      </div>

      {/* Aging Summary Cards */}
      {summary && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
          <button
            onClick={() => { setBucket(''); setPage(1) }}
            className={`card bg-base-100 border border-base-content/10 p-4 text-left hover:border-primary/30 transition-colors ${bucket === '' ? 'ring-2 ring-primary' : ''}`}
          >
            <div className="text-xs text-base-content/50 uppercase tracking-wide">Total Open</div>
            <div className="text-xl font-bold text-base-content mt-1">{fmt(summary.total)}</div>
            <div className="text-xs text-base-content/40 mt-0.5">{summary.totalCount} invoices</div>
          </button>
          {[
            { key: '15', label: '15+ Days', data: summary.days15 },
            { key: '30', label: '30+ Days', data: summary.days30 },
            { key: '60', label: '60+ Days', data: summary.days60 },
            { key: '90', label: '90+ Days', data: summary.days90 },
          ].map(b => (
            <button
              key={b.key}
              onClick={() => { setBucket(bucket === b.key ? '' : b.key); setPage(1) }}
              className={`card bg-base-100 border border-base-content/10 p-4 text-left hover:border-primary/30 transition-colors ${bucket === b.key ? 'ring-2 ring-primary' : ''}`}
            >
              <div className="text-xs text-base-content/50 uppercase tracking-wide">{b.label}</div>
              <div className="text-xl font-bold text-base-content mt-1">{fmt(b.data.amount)}</div>
              <div className="text-xs text-base-content/40 mt-0.5">{b.data.count} invoices</div>
            </button>
          ))}
        </div>
      )}

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-center">
        <div className="input input-sm bg-base-100 border-base-content/10 w-64">
          <span className="icon-[tabler--search] text-base-content/40 size-4" />
          <input
            type="text"
            placeholder="Search customers..."
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1) }}
            className="grow"
          />
        </div>
        <select
          className="select select-sm bg-base-100 border-base-content/10"
          value={statusFilter}
          onChange={e => { setStatusFilter(e.target.value); setPage(1) }}
        >
          <option value="all">All Statuses</option>
          <option value="active">Active</option>
          <option value="payment_plan">Payment Plan</option>
          <option value="sent_to_collections">Collections</option>
          <option value="written_off">Written Off</option>
        </select>
      </div>

      {/* Customer Table */}
      <div className="card bg-base-100 border border-base-content/10 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="table table-sm">
            <thead>
              <tr className="border-b border-base-content/10">
                <th className="text-base-content/60 font-medium text-xs uppercase">Customer</th>
                <th className="text-base-content/60 font-medium text-xs uppercase">Total Owed</th>
                <th className="text-base-content/60 font-medium text-xs uppercase">Invoices</th>
                <th className="text-base-content/60 font-medium text-xs uppercase">Oldest</th>
                <th className="text-base-content/60 font-medium text-xs uppercase">Status</th>
                <th className="text-base-content/60 font-medium text-xs uppercase">Last Contact</th>
                <th className="text-base-content/60 font-medium text-xs uppercase">Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={7} className="text-center py-12">
                  <span className="loading loading-spinner loading-md text-primary" />
                </td></tr>
              ) : customers.length === 0 ? (
                <tr><td colSpan={7} className="text-center py-12 text-base-content/40 text-sm">
                  No customers found
                </td></tr>
              ) : customers.map(c => (
                <>
                  <tr
                    key={c.customerId}
                    className={`border-b border-base-content/5 hover:bg-base-200/30 cursor-pointer transition-colors ${expandedId === c.customerId ? 'bg-base-200/40' : ''}`}
                    onClick={() => loadDetail(c.customerId)}
                  >
                    <td>
                      <div className="font-medium text-base-content">{c.customerName}</div>
                      <div className="text-xs text-base-content/40">
                        {c.customerPhone && <span>{c.customerPhone}</span>}
                        {c.customerPhone && c.customerEmail && <span> &middot; </span>}
                        {c.customerEmail && <span>{c.customerEmail}</span>}
                      </div>
                    </td>
                    <td className="font-semibold text-base-content">{fmt(c.totalOwed)}</td>
                    <td>{c.invoiceCount}</td>
                    <td>
                      <span className={`badge badge-sm badge-soft ${ageBadge(c.oldestDays)}`}>
                        {c.oldestDays}d
                      </span>
                    </td>
                    <td>
                      <span className={`badge badge-sm badge-soft ${STATUS_LABELS[c.status]?.color ?? 'badge-neutral'}`}>
                        {STATUS_LABELS[c.status]?.label ?? c.status}
                      </span>
                    </td>
                    <td className="text-xs text-base-content/50">
                      {c.lastContact ? new Date(c.lastContact).toLocaleDateString() : 'Never'}
                    </td>
                    <td onClick={e => e.stopPropagation()}>
                      <div className="flex gap-1">
                        <button
                          className="btn btn-ghost btn-xs tooltip tooltip-top"
                          data-tip="Call"
                          onClick={() => { if (c.customerPhone) window.location.href = `tel:${c.customerPhone}`; else alert('No phone on file') }}
                        >
                          <span className="icon-[tabler--phone] size-3.5" />
                        </button>
                        <button
                          className="btn btn-ghost btn-xs tooltip tooltip-top"
                          data-tip="Email"
                          onClick={() => sendReminder(c, 'email')}
                        >
                          <span className="icon-[tabler--mail] size-3.5" />
                        </button>
                        <button
                          className="btn btn-ghost btn-xs tooltip tooltip-top"
                          data-tip="SMS"
                          onClick={() => sendReminder(c, 'sms')}
                        >
                          <span className="icon-[tabler--message] size-3.5" />
                        </button>
                        <button
                          className="btn btn-ghost btn-xs tooltip tooltip-top"
                          data-tip="Log Contact"
                          onClick={() => openContactModal(c.customerId)}
                        >
                          <span className="icon-[tabler--notebook] size-3.5" />
                        </button>
                        <button
                          className="btn btn-ghost btn-xs tooltip tooltip-top"
                          data-tip="Change Status"
                          onClick={() => openStatusModal(c.customerId)}
                        >
                          <span className="icon-[tabler--settings] size-3.5" />
                        </button>
                      </div>
                    </td>
                  </tr>
                  {/* Expanded Detail Row */}
                  {expandedId === c.customerId && (
                    <tr key={`detail-${c.customerId}`} className="bg-base-200/20">
                      <td colSpan={7} className="p-4">
                        {detailLoading ? (
                          <div className="flex justify-center py-6">
                            <span className="loading loading-spinner loading-sm text-primary" />
                          </div>
                        ) : detail ? (
                          <div className="grid md:grid-cols-2 gap-6">
                            {/* Invoices */}
                            <div>
                              <h4 className="font-semibold text-sm text-base-content mb-2 flex items-center gap-1.5">
                                <span className="icon-[tabler--file-invoice] size-4" />
                                Open Invoices ({detail.invoices.length})
                              </h4>
                              <div className="space-y-1.5">
                                {detail.invoices.map(inv => (
                                  <div key={inv.stInvoiceId} className="flex items-center justify-between bg-base-100 rounded-box px-3 py-2 border border-base-content/5">
                                    <div>
                                      <span className="text-xs text-base-content/50">#{inv.stInvoiceId}</span>
                                      <span className="text-xs text-base-content/40 ml-2">{new Date(inv.invoiceDate).toLocaleDateString()}</span>
                                    </div>
                                    <div className="flex items-center gap-2">
                                      <span className="font-medium text-sm">{fmt(inv.balanceRemaining)}</span>
                                      <span className={`badge badge-xs badge-soft ${ageBadge(inv.ageDays)}`}>{inv.ageDays}d</span>
                                    </div>
                                  </div>
                                ))}
                                {detail.invoices.length === 0 && (
                                  <div className="text-xs text-base-content/40 text-center py-3">No open invoices</div>
                                )}
                              </div>
                            </div>

                            {/* Contact History */}
                            <div>
                              <h4 className="font-semibold text-sm text-base-content mb-2 flex items-center gap-1.5">
                                <span className="icon-[tabler--history] size-4" />
                                Contact History ({detail.contactLogs.length})
                              </h4>
                              <div className="space-y-1.5 max-h-48 overflow-y-auto">
                                {detail.contactLogs.map(log => (
                                  <div key={log.id} className="bg-base-100 rounded-box px-3 py-2 border border-base-content/5">
                                    <div className="flex items-center justify-between">
                                      <div className="flex items-center gap-1.5">
                                        <span className={`icon-[tabler--${log.contactType === 'call' ? 'phone' : log.contactType === 'email' ? 'mail' : 'message'}] size-3.5 text-base-content/50`} />
                                        <span className="text-xs font-medium capitalize">{log.contactType}</span>
                                        <span className="text-xs text-base-content/40">&middot;</span>
                                        <span className="text-xs text-base-content/50">{OUTCOME_LABELS[log.outcome] || log.outcome}</span>
                                      </div>
                                      <span className="text-xs text-base-content/40">{new Date(log.createdAt).toLocaleDateString()}</span>
                                    </div>
                                    {log.notes && <div className="text-xs text-base-content/60 mt-1">{log.notes}</div>}
                                    {log.followUpDate && (
                                      <div className="text-xs text-warning mt-1">Follow up: {new Date(log.followUpDate).toLocaleDateString()}</div>
                                    )}
                                  </div>
                                ))}
                                {detail.contactLogs.length === 0 && (
                                  <div className="text-xs text-base-content/40 text-center py-3">No contact history</div>
                                )}
                              </div>
                            </div>
                          </div>
                        ) : null}
                      </td>
                    </tr>
                  )}
                </>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-base-content/10">
            <div className="text-xs text-base-content/40">
              Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)} of {total}
            </div>
            <div className="flex gap-1">
              <button className="btn btn-ghost btn-xs" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>
                <span className="icon-[tabler--chevron-left] size-4" />
              </button>
              <button className="btn btn-ghost btn-xs" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>
                <span className="icon-[tabler--chevron-right] size-4" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Log Contact Modal */}
      {showContactModal && (
        <LogContactModal
          customerId={contactCustomerId}
          onClose={() => setShowContactModal(false)}
          onSaved={() => { setShowContactModal(false); loadCustomers(); if (expandedId === contactCustomerId) loadDetail(contactCustomerId).then(() => loadDetail(contactCustomerId)) }}
        />
      )}

      {/* Update Status Modal */}
      {showStatusModal && (
        <UpdateStatusModal
          customerId={statusCustomerId}
          currentCustomer={customers.find(c => c.customerId === statusCustomerId)}
          onClose={() => setShowStatusModal(false)}
          onSaved={() => { setShowStatusModal(false); loadCustomers(); loadSummary() }}
        />
      )}
    </div>
  )
}

function LogContactModal({ customerId, onClose, onSaved }: { customerId: number; onClose: () => void; onSaved: () => void }) {
  const [contactType, setContactType] = useState('call')
  const [outcome, setOutcome] = useState('spoke_with')
  const [notes, setNotes] = useState('')
  const [followUpDate, setFollowUpDate] = useState('')
  const [saving, setSaving] = useState(false)

  const handleSave = async () => {
    setSaving(true)
    try {
      const r = await fetch(`/api/ar-alerts/customers/${customerId}/contact`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({
          contactType,
          outcome,
          notes: notes || null,
          followUpDate: followUpDate || null,
        }),
      })
      if (r.ok) onSaved()
    } catch { /* ignore */ }
    finally { setSaving(false) }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={onClose}>
      <div className="card bg-base-100 w-full max-w-md shadow-xl" onClick={e => e.stopPropagation()}>
        <div className="card-body">
          <h3 className="card-title text-base">Log Contact</h3>

          <div className="form-control">
            <label className="label"><span className="label-text text-xs">Contact Type</span></label>
            <div className="flex gap-2">
              {['call', 'email', 'text'].map(t => (
                <button
                  key={t}
                  className={`btn btn-sm flex-1 ${contactType === t ? 'btn-primary' : 'btn-ghost'}`}
                  onClick={() => setContactType(t)}
                >
                  <span className={`icon-[tabler--${t === 'call' ? 'phone' : t === 'email' ? 'mail' : 'message'}] size-3.5`} />
                  <span className="capitalize">{t}</span>
                </button>
              ))}
            </div>
          </div>

          <div className="form-control">
            <label className="label"><span className="label-text text-xs">Outcome</span></label>
            <select className="select select-sm bg-base-200/60" value={outcome} onChange={e => setOutcome(e.target.value)}>
              <option value="spoke_with">Spoke With</option>
              <option value="left_voicemail">Left Voicemail</option>
              <option value="promised_to_pay">Promised to Pay</option>
              <option value="disputed">Disputed</option>
              <option value="no_answer">No Answer</option>
              <option value="wrong_number">Wrong Number</option>
            </select>
          </div>

          <div className="form-control">
            <label className="label"><span className="label-text text-xs">Notes</span></label>
            <textarea
              className="textarea textarea-sm bg-base-200/60 h-20"
              placeholder="Optional notes..."
              value={notes}
              onChange={e => setNotes(e.target.value)}
            />
          </div>

          <div className="form-control">
            <label className="label"><span className="label-text text-xs">Follow-Up Date</span></label>
            <input
              type="date"
              className="input input-sm bg-base-200/60"
              value={followUpDate}
              onChange={e => setFollowUpDate(e.target.value)}
            />
          </div>

          <div className="card-actions justify-end mt-2">
            <button className="btn btn-ghost btn-sm" onClick={onClose}>Cancel</button>
            <button className="btn btn-primary btn-sm" onClick={handleSave} disabled={saving}>
              {saving ? <span className="loading loading-spinner loading-xs" /> : 'Save'}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

function UpdateStatusModal({ customerId, currentCustomer, onClose, onSaved }: {
  customerId: number
  currentCustomer?: ArCustomer
  onClose: () => void
  onSaved: () => void
}) {
  const [status, setStatus] = useState(currentCustomer?.status ?? 'active')
  const [paymentPlanAmount, setPaymentPlanAmount] = useState(currentCustomer?.paymentPlanAmount?.toString() ?? '')
  const [paymentPlanNote, setPaymentPlanNote] = useState(currentCustomer?.paymentPlanNote ?? '')
  const [saving, setSaving] = useState(false)

  const handleSave = async () => {
    setSaving(true)
    try {
      const r = await fetch(`/api/ar-alerts/customers/${customerId}/status`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({
          status,
          paymentPlanAmount: paymentPlanAmount ? parseFloat(paymentPlanAmount) : null,
          paymentPlanNote: paymentPlanNote || null,
        }),
      })
      if (r.ok) onSaved()
    } catch { /* ignore */ }
    finally { setSaving(false) }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={onClose}>
      <div className="card bg-base-100 w-full max-w-md shadow-xl" onClick={e => e.stopPropagation()}>
        <div className="card-body">
          <h3 className="card-title text-base">Update Status</h3>
          <p className="text-sm text-base-content/50">{currentCustomer?.customerName}</p>

          <div className="form-control">
            <label className="label"><span className="label-text text-xs">Status</span></label>
            <select className="select select-sm bg-base-200/60" value={status} onChange={e => setStatus(e.target.value)}>
              <option value="active">Active</option>
              <option value="payment_plan">Payment Plan</option>
              <option value="sent_to_collections">Sent to Collections</option>
              <option value="written_off">Written Off</option>
            </select>
          </div>

          {status === 'payment_plan' && (
            <>
              <div className="form-control">
                <label className="label"><span className="label-text text-xs">Monthly Payment Amount</span></label>
                <input
                  type="number"
                  className="input input-sm bg-base-200/60"
                  placeholder="e.g. 150.00"
                  value={paymentPlanAmount}
                  onChange={e => setPaymentPlanAmount(e.target.value)}
                />
              </div>
              <div className="form-control">
                <label className="label"><span className="label-text text-xs">Payment Plan Notes</span></label>
                <textarea
                  className="textarea textarea-sm bg-base-200/60 h-16"
                  placeholder="e.g. Agreed to $150/mo starting April 1"
                  value={paymentPlanNote}
                  onChange={e => setPaymentPlanNote(e.target.value)}
                />
              </div>
            </>
          )}

          <div className="card-actions justify-end mt-2">
            <button className="btn btn-ghost btn-sm" onClick={onClose}>Cancel</button>
            <button className="btn btn-primary btn-sm" onClick={handleSave} disabled={saving}>
              {saving ? <span className="loading loading-spinner loading-xs" /> : 'Save'}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

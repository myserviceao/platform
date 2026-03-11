import { useState, useEffect, useCallback } from 'react'

interface OutreachCustomer {
  stCustomerId: number
  customerName: string
  phone: string | null
  email: string | null
  pmStatus: string
  lastPmDate: string | null
  daysSince: number
  message: string
  subject: string
}

interface OutreachData {
  companyName: string
  overdueCount: number
  comingDueCount: number
  noPmCount: number
  customers: OutreachCustomer[]
}


function statusBadge(status: string) {
  switch (status) {
    case 'Overdue': return 'badge-error'
    case 'ComingDue': return 'badge-warning'
    case 'NoPm': return 'badge-ghost'
    default: return 'badge-ghost'
  }
}

function statusLabel(status: string) {
  switch (status) {
    case 'Overdue': return 'Overdue'
    case 'ComingDue': return 'Coming Due'
    case 'NoPm': return 'No PM'
    default: return status
  }
}

export function PmOutreachPage() {
  const [data, setData] = useState<OutreachData | null>(null)
  const [loading, setLoading] = useState(true)
  const [filter, setFilter] = useState<'all' | 'Overdue' | 'ComingDue' | 'NoPm'>('all')
  const [search, setSearch] = useState('')
  const [expandedId, setExpandedId] = useState<number | null>(null)
  const [copied, setCopied] = useState<number | null>(null)

  const fetchData = useCallback(async () => {
    const res = await fetch('/api/pm-outreach', { credentials: 'include' })
    if (res.ok) setData(await res.json())
  }, [])

  useEffect(() => { fetchData().finally(() => setLoading(false)) }, [fetchData])

  const copyMessage = (c: OutreachCustomer) => {
    navigator.clipboard.writeText(c.message)
    setCopied(c.stCustomerId)
    setTimeout(() => setCopied(null), 2000)
  }

  if (loading) {
    return <div className="flex items-center justify-center py-20"><span className="loading loading-spinner loading-md text-primary" /></div>
  }

  if (!data) {
    return <div className="text-center py-20 text-base-content/40">Failed to load outreach data. Try syncing from the Dashboard first.</div>
  }

  const filtered = data.customers
    .filter(c => filter === 'all' || c.pmStatus === filter)
    .filter(c => c.customerName.toLowerCase().includes(search.toLowerCase()))

  const withPhone = filtered.filter(c => c.phone)
  const withEmail = filtered.filter(c => c.email)

  return (
    <div className="max-w-6xl mx-auto space-y-6 py-2">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-base-content">PM Outreach</h1>
          <p className="text-sm text-base-content/60 mt-1">
            Send maintenance reminders to customers. {data.customers.length} customers need outreach.
          </p>
        </div>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-3 gap-3">
        <button
          onClick={() => setFilter(filter === 'Overdue' ? 'all' : 'Overdue')}
          className={`rounded-box border-2 p-4 text-left transition-all ${
            filter === 'Overdue' ? 'border-error/30 bg-error/5' : 'border-base-content/10 bg-base-100 hover:border-base-content/20'
          }`}
        >
          <div className="flex items-center gap-2 mb-1">
            <span className="icon-[tabler--alert-circle] size-4 text-error" />
            <span className="text-xs text-base-content/60 font-medium">Overdue</span>
          </div>
          <div className="text-2xl font-bold text-base-content">{data.overdueCount}</div>
          <div className="text-xs text-base-content/40">6+ months since last PM</div>
        </button>
        <button
          onClick={() => setFilter(filter === 'ComingDue' ? 'all' : 'ComingDue')}
          className={`rounded-box border-2 p-4 text-left transition-all ${
            filter === 'ComingDue' ? 'border-warning/30 bg-warning/5' : 'border-base-content/10 bg-base-100 hover:border-base-content/20'
          }`}
        >
          <div className="flex items-center gap-2 mb-1">
            <span className="icon-[tabler--clock] size-4 text-warning" />
            <span className="text-xs text-base-content/60 font-medium">Coming Due</span>
          </div>
          <div className="text-2xl font-bold text-base-content">{data.comingDueCount}</div>
          <div className="text-xs text-base-content/40">4-6 months since last PM</div>
        </button>
        <button
          onClick={() => setFilter(filter === 'NoPm' ? 'all' : 'NoPm')}
          className={`rounded-box border-2 p-4 text-left transition-all ${
            filter === 'NoPm' ? 'border-base-content/20 bg-base-200/50' : 'border-base-content/10 bg-base-100 hover:border-base-content/20'
          }`}
        >
          <div className="flex items-center gap-2 mb-1">
            <span className="icon-[tabler--minus] size-4 text-base-content/40" />
            <span className="text-xs text-base-content/60 font-medium">No PM on Record</span>
          </div>
          <div className="text-2xl font-bold text-base-content">{data.noPmCount}</div>
          <div className="text-xs text-base-content/40">Never had maintenance with us</div>
        </button>
      </div>

      {/* Search + stats */}
      <div className="flex items-center justify-between gap-3">
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
        <div className="text-xs text-base-content/40">
          {withPhone.length} with phone, {withEmail.length} with email
        </div>
      </div>

      {/* Customer list */}
      {filtered.length === 0 ? (
        <div className="text-center py-16 text-base-content/40 text-sm">
          {search ? 'No customers match your search.' : 'No customers need outreach.'}
        </div>
      ) : (
        <div className="space-y-2">
          {filtered.map(c => {
            const expanded = expandedId === c.stCustomerId
            return (
              <div key={c.stCustomerId} className="rounded-box border border-base-content/10 bg-base-100 overflow-hidden">
                {/* Row header */}
                <button
                  onClick={() => setExpandedId(expanded ? null : c.stCustomerId)}
                  className="w-full flex items-center gap-3 px-4 py-3 text-left hover:bg-base-200/40 transition-colors"
                >
                  <span className={`badge badge-soft badge-xs ${statusBadge(c.pmStatus)}`}>
                    {statusLabel(c.pmStatus)}
                  </span>
                  <span className="font-medium text-base-content text-sm flex-1">{c.customerName}</span>
                  {c.pmStatus !== 'NoPm' && (
                    <span className="text-xs text-base-content/40">{c.daysSince}d since last PM</span>
                  )}
                  <div className="flex items-center gap-1.5">
                    {c.phone && <span className="icon-[tabler--phone] size-3.5 text-success" title={c.phone} />}
                    {c.email && <span className="icon-[tabler--mail] size-3.5 text-primary" title={c.email} />}
                    {!c.phone && !c.email && <span className="text-xs text-base-content/30">No contact</span>}
                  </div>
                  <span className={`icon-[tabler--chevron-down] size-4 text-base-content/30 transition-transform ${expanded ? 'rotate-180' : ''}`} />
                </button>

                {/* Expanded message */}
                {expanded && (
                  <div className="border-t border-base-content/10 px-4 py-4 space-y-3">
                    <div className="text-xs text-base-content/50 font-medium uppercase tracking-wide">Generated Message</div>
                    <pre className="text-sm text-base-content/80 whitespace-pre-wrap font-sans bg-base-200/30 rounded-lg p-3 max-h-48 overflow-y-auto">
                      {c.message}
                    </pre>

                    {/* Contact info */}
                    <div className="flex items-center gap-2 text-xs text-base-content/50">
                      {c.phone && <span>Phone: {c.phone}</span>}
                      {c.phone && c.email && <span>|</span>}
                      {c.email && <span>Email: {c.email}</span>}
                    </div>

                    {/* Action buttons */}
                    <div className="flex flex-wrap gap-2">
                      <button
                        onClick={() => copyMessage(c)}
                        className="btn btn-sm btn-ghost gap-1"
                      >
                        <span className={`size-4 ${copied === c.stCustomerId ? 'icon-[tabler--check] text-success' : 'icon-[tabler--copy]'}`} />
                        {copied === c.stCustomerId ? 'Copied!' : 'Copy Message'}
                      </button>
                      {c.phone && (
                        <a
                          href={`sms:${c.phone}?body=${encodeURIComponent(c.message)}`}
                          className="btn btn-sm btn-success gap-1"
                        >
                          <span className="icon-[tabler--message] size-4" />
                          Text
                        </a>
                      )}
                      {c.email && (
                        <a
                          href={`mailto:${c.email}?subject=${encodeURIComponent(c.subject)}&body=${encodeURIComponent(c.message)}`}
                          className="btn btn-sm btn-primary gap-1"
                        >
                          <span className="icon-[tabler--mail] size-4" />
                          Email
                        </a>
                      )}
                    </div>
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}

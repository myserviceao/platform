import { useState, useEffect, useCallback } from 'react'
import { MessageEditorModal } from './MessageEditorModal'
import { TemplateManager } from './TemplateManager'
import { OutreachSettings } from './OutreachSettings'
import { CampaignModal } from './CampaignModal'

interface OutreachItem {
  id: number
  customerId: number
  jobId: number | null
  type: string
  channel: string
  status: string
  failureReason: string | null
  subject: string | null
  body: string
  scheduledFor: string | null
  sentAt: string | null
  dismissedAt: string | null
  createdAt: string
  updatedAt: string
  customerName: string | null
  customerPhone: string | null
  customerEmail: string | null
}

interface Stats {
  byTypeAndStatus: { type: string; status: string; count: number }[]
  sentToday: number
  sentThisWeek: number
  totalPending: number
}

type Tab = 'pm_reminder' | 'post_service' | 'win_back' | 'seasonal' | 'sent' | 'templates' | 'settings'

function fmtDate(d?: string | null) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

export function OutreachPage() {
  const [tab, setTab] = useState<Tab>('pm_reminder')
  const [items, setItems] = useState<OutreachItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [stats, setStats] = useState<Stats | null>(null)
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [editItem, setEditItem] = useState<OutreachItem | null>(null)
  const [showCampaign, setShowCampaign] = useState(false)
  const [sending, setSending] = useState<number | null>(null)

  const isQueueTab = !['sent', 'templates', 'settings'].includes(tab)

  const fetchStats = useCallback(async () => {
    const res = await fetch('/api/outreach/stats', { credentials: 'include' })
    if (res.ok) setStats(await res.json())
  }, [])

  const fetchItems = useCallback(async () => {
    setLoading(true)
    const params = new URLSearchParams({ page: String(page), pageSize: '50' })
    if (tab === 'sent') {
      params.set('status', 'sent')
    } else if (isQueueTab) {
      params.set('type', tab)
      params.set('status', 'pending')
    }
    const res = await fetch(`/api/outreach?${params}`, { credentials: 'include' })
    if (res.ok) {
      const data = await res.json()
      setItems(data.items)
      setTotal(data.total)
    }
    setLoading(false)
  }, [tab, page, isQueueTab])

  useEffect(() => { fetchStats() }, [fetchStats])
  useEffect(() => {
    if (tab !== 'templates' && tab !== 'settings') fetchItems()
  }, [fetchItems, tab])

  const pendingCount = (type: string) =>
    stats?.byTypeAndStatus.filter(s => s.type === type && s.status === 'pending').reduce((a, b) => a + b.count, 0) ?? 0

  const openNativeClient = (item: OutreachItem): boolean => {
    if (item.channel === 'email') {
      if (!item.customerEmail) {
        alert('This customer has no email address on file.')
        return false
      }
      const subject = encodeURIComponent(item.subject ?? '')
      const body = encodeURIComponent(item.body)
      window.location.href = `mailto:${item.customerEmail}?subject=${subject}&body=${body}`
      return true
    } else if (item.channel === 'sms') {
      if (!item.customerPhone) {
        alert('This customer has no phone number on file.')
        return false
      }
      const body = encodeURIComponent(item.body)
      window.location.href = `sms:${item.customerPhone}?body=${body}`
      return true
    }
    return false
  }

  const handleSend = async (item: OutreachItem) => {
    if (!openNativeClient(item)) return
    setSending(item.id)
    await fetch(`/api/outreach/${item.id}/mark-sent`, { method: 'POST', credentials: 'include' })
    setSending(null)
    fetchItems()
    fetchStats()
  }

  const handleDismiss = async (id: number) => {
    await fetch(`/api/outreach/${id}/dismiss`, { method: 'POST', credentials: 'include' })
    fetchItems()
    fetchStats()
  }

  const handleRetry = async (item: OutreachItem) => {
    setSending(item.id)
    openNativeClient(item)
    await fetch(`/api/outreach/${item.id}/mark-sent`, { method: 'POST', credentials: 'include' })
    setSending(null)
    fetchItems()
    fetchStats()
  }

  const handleBulkSend = async () => {
    if (selected.size === 0) return
    // Open native client for each selected item
    const selectedItems = allItems.filter(i => selected.has(i.id))
    for (const item of selectedItems) {
      openNativeClient(item)
    }
    // Mark all as sent
    for (const id of selected) {
      await fetch(`/api/outreach/${id}/mark-sent`, { method: 'POST', credentials: 'include' })
    }
    setSelected(new Set())
    fetchItems()
    fetchStats()
  }

  const handleBulkDismiss = async () => {
    if (selected.size === 0) return
    await fetch('/api/outreach/bulk-dismiss', {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ids: [...selected] }),
    })
    setSelected(new Set())
    fetchItems()
    fetchStats()
  }

  const toggleSelect = (id: number) => {
    setSelected(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const toggleAll = () => {
    if (selected.size === items.length) setSelected(new Set())
    else setSelected(new Set(items.map(i => i.id)))
  }

  const tabs: { key: Tab; label: string; count?: number }[] = [
    { key: 'pm_reminder', label: 'PM Reminders', count: pendingCount('pm_reminder') },
    { key: 'post_service', label: 'Post-Service', count: pendingCount('post_service') },
    { key: 'win_back', label: 'Win-Back', count: pendingCount('win_back') },
    { key: 'seasonal', label: 'Seasonal', count: pendingCount('seasonal') },
    { key: 'sent', label: 'Sent History' },
    { key: 'templates', label: 'Templates' },
    { key: 'settings', label: 'Settings' },
  ]

  // Also load failed items for queue tabs
  const [failedItems, setFailedItems] = useState<OutreachItem[]>([])
  useEffect(() => {
    if (!isQueueTab) { setFailedItems([]); return }
    const fetchFailed = async () => {
      const params = new URLSearchParams({ type: tab, status: 'failed', pageSize: '50' })
      const res = await fetch(`/api/outreach?${params}`, { credentials: 'include' })
      if (res.ok) {
        const data = await res.json()
        setFailedItems(data.items)
      }
    }
    fetchFailed()
  }, [tab, isQueueTab])

  const allItems = [...failedItems, ...items]

  return (
    <div className="max-w-6xl mx-auto space-y-5 py-2">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-base-content">Outreach</h1>
          <p className="text-sm text-base-content/50 mt-0.5">Review and send customer communications</p>
        </div>
        <button onClick={() => setShowCampaign(true)} className="btn btn-primary btn-sm gap-1.5">
          <span className="icon-[tabler--speakerphone] size-4" />
          New Campaign
        </button>
      </div>

      {/* Stats */}
      {stats && (
        <div className="grid grid-cols-3 gap-4">
          <div className="rounded-box border border-base-content/10 bg-base-100 p-4">
            <div className="text-xs text-base-content/50 mb-1">Total Pending</div>
            <div className="text-xl font-bold text-primary">{stats.totalPending}</div>
          </div>
          <div className="rounded-box border border-base-content/10 bg-base-100 p-4">
            <div className="text-xs text-base-content/50 mb-1">Sent Today</div>
            <div className="text-xl font-bold text-success">{stats.sentToday}</div>
          </div>
          <div className="rounded-box border border-base-content/10 bg-base-100 p-4">
            <div className="text-xs text-base-content/50 mb-1">Sent This Week</div>
            <div className="text-xl font-bold text-base-content">{stats.sentThisWeek}</div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div className="tabs tabs-bordered overflow-x-auto">
        {tabs.map(t => (
          <button
            key={t.key}
            onClick={() => { setTab(t.key); setPage(1); setSelected(new Set()) }}
            className={`tab whitespace-nowrap ${tab === t.key ? 'tab-active' : ''}`}
          >
            {t.label}
            {t.count !== undefined && t.count > 0 && (
              <span className="badge badge-primary badge-soft badge-xs ms-1.5">{t.count}</span>
            )}
          </button>
        ))}
      </div>

      {/* Templates tab */}
      {tab === 'templates' && <TemplateManager />}

      {/* Settings tab */}
      {tab === 'settings' && <OutreachSettings />}

      {/* Queue / Sent tabs */}
      {tab !== 'templates' && tab !== 'settings' && (
        <>
          {/* Bulk actions */}
          {isQueueTab && selected.size > 0 && (
            <div className="flex items-center gap-2 p-3 rounded-box bg-primary/10 border border-primary/20">
              <span className="text-sm font-medium text-primary">{selected.size} selected</span>
              <button onClick={handleBulkSend} className="btn btn-primary btn-xs gap-1">
                <span className="icon-[tabler--send] size-3" /> Send
              </button>
              <button onClick={handleBulkDismiss} className="btn btn-ghost btn-xs gap-1">
                <span className="icon-[tabler--x] size-3" /> Dismiss
              </button>
            </div>
          )}

          {loading ? (
            <div className="flex items-center justify-center py-16">
              <span className="loading loading-spinner loading-md text-primary" />
            </div>
          ) : allItems.length === 0 ? (
            <div className="text-center py-16 text-base-content/40 text-sm">
              {tab === 'sent' ? 'No sent messages yet.' : 'No pending items. Outreach items are generated during sync.'}
            </div>
          ) : (
            <div className="rounded-box border border-base-content/10 overflow-hidden">
              <table className="table table-sm">
                <thead>
                  <tr>
                    {isQueueTab && (
                      <th className="w-8">
                        <input type="checkbox" className="checkbox checkbox-xs" checked={selected.size === allItems.length && allItems.length > 0} onChange={toggleAll} />
                      </th>
                    )}
                    <th>Customer</th>
                    <th>Contact</th>
                    {tab !== 'sent' && <th>Channel</th>}
                    <th>Draft Preview</th>
                    {tab === 'win_back' && <th className="text-right">Spend</th>}
                    {tab === 'sent' && <th>Channel</th>}
                    {tab === 'sent' && <th>Sent</th>}
                    {isQueueTab && <th className="text-right">Actions</th>}
                  </tr>
                </thead>
                <tbody>
                  {allItems.map(item => (
                    <tr key={item.id} className={`row-hover ${item.status === 'failed' ? 'bg-error/5' : ''}`}>
                      {isQueueTab && (
                        <td>
                          <input type="checkbox" className="checkbox checkbox-xs" checked={selected.has(item.id)} onChange={() => toggleSelect(item.id)} />
                        </td>
                      )}
                      <td>
                        <div className="flex items-center gap-2">
                          <div className="avatar avatar-placeholder">
                            <div className="bg-primary/20 text-primary rounded-full size-8 text-xs font-bold">
                              {(item.customerName ?? '?')[0].toUpperCase()}
                            </div>
                          </div>
                          <div>
                            <div className="font-medium text-base-content text-sm">{item.customerName ?? 'Unknown'}</div>
                            {item.status === 'failed' && (
                              <span className="badge badge-error badge-soft badge-xs gap-0.5">
                                <span className="icon-[tabler--alert-triangle] size-2.5" /> Failed
                              </span>
                            )}
                          </div>
                        </div>
                      </td>
                      <td className="text-xs text-base-content/60">
                        {item.channel === 'email' ? item.customerEmail || '—' : item.customerPhone || '—'}
                      </td>
                      {tab !== 'sent' && (
                        <td>
                          <span className={`badge badge-soft badge-xs ${item.channel === 'email' ? 'badge-info' : 'badge-warning'}`}>
                            {item.channel}
                          </span>
                        </td>
                      )}
                      <td className="max-w-xs">
                        <div className="text-sm text-base-content/70 truncate">{item.subject || item.body.slice(0, 80)}</div>
                        {item.failureReason && (
                          <div className="text-xs text-error mt-0.5 truncate">{item.failureReason}</div>
                        )}
                      </td>
                      {tab === 'win_back' && <td className="text-right font-medium text-base-content">—</td>}
                      {tab === 'sent' && (
                        <td>
                          <span className={`badge badge-soft badge-xs ${item.channel === 'email' ? 'badge-info' : 'badge-warning'}`}>
                            {item.channel}
                          </span>
                        </td>
                      )}
                      {tab === 'sent' && <td className="text-xs text-base-content/60">{fmtDate(item.sentAt)}</td>}
                      {isQueueTab && (
                        <td className="text-right">
                          <div className="flex items-center gap-1 justify-end">
                            <button onClick={() => setEditItem(item)} className="btn btn-ghost btn-xs" title="Edit">
                              <span className="icon-[tabler--edit] size-3.5" />
                            </button>
                            {item.status === 'failed' ? (
                              <button onClick={() => handleRetry(item)} disabled={sending === item.id} className="btn btn-warning btn-xs gap-0.5" title="Retry">
                                {sending === item.id ? <span className="loading loading-spinner loading-xs" /> : <span className="icon-[tabler--refresh] size-3" />}
                                Retry
                              </button>
                            ) : (
                              <button onClick={() => handleSend(item)} disabled={sending === item.id} className="btn btn-primary btn-xs gap-0.5" title="Send">
                                {sending === item.id ? <span className="loading loading-spinner loading-xs" /> : <span className="icon-[tabler--send] size-3" />}
                                Send
                              </button>
                            )}
                            <button onClick={() => handleDismiss(item.id)} className="btn btn-ghost btn-xs text-base-content/40" title="Dismiss">
                              <span className="icon-[tabler--x] size-3.5" />
                            </button>
                          </div>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Pagination */}
          {total > 50 && (
            <div className="flex items-center justify-center gap-2 pt-2">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page <= 1} className="btn btn-ghost btn-xs">
                <span className="icon-[tabler--chevron-left] size-4" />
              </button>
              <span className="text-sm text-base-content/50">Page {page} of {Math.ceil(total / 50)}</span>
              <button onClick={() => setPage(p => p + 1)} disabled={page >= Math.ceil(total / 50)} className="btn btn-ghost btn-xs">
                <span className="icon-[tabler--chevron-right] size-4" />
              </button>
            </div>
          )}
        </>
      )}

      {/* Modals */}
      {editItem && (
        <MessageEditorModal
          item={editItem}
          onClose={() => setEditItem(null)}
          onSaved={() => { setEditItem(null); fetchItems() }}
          onSent={() => { setEditItem(null); fetchItems(); fetchStats() }}
        />
      )}
      {showCampaign && (
        <CampaignModal
          onClose={() => setShowCampaign(false)}
          onCreated={() => { setShowCampaign(false); setTab('seasonal'); fetchItems(); fetchStats() }}
        />
      )}
    </div>
  )
}

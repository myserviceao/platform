import { useState, useEffect } from 'react'

interface POItem { skuName: string; skuCode: string; description: string | null; quantity: number; quantityReceived: number; cost: number; total: number; status: string }
interface PO { id: number; number: string; status: string; vendorName: string; jobNumber: string | null; total: number; tax: number; shipping: number; summary: string | null; date: string; requiredOn: string | null; sentOn: string | null; receivedOn: string | null; itemCount: number; items: POItem[] }
interface Bill { id: number; invoiceNumber: string; amount: number; dueDate: string; isPaid: boolean; paidDate: string | null; stPurchaseOrderId: number | null; status: string | null; source: string | null; referenceNumber: string | null; summary: string | null; billDate: string | null; vendorName: string }
interface ApSummary { totalUnpaid: number; billCount: number; overdueCount: number; overdueAmount: number; dueThisWeek: number; dueThisMonth: number; poCount: number; openPoCount: number; openPoTotal: number; byVendor: { vendor: string; total: number; count: number }[] }

const fmt = (n: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 2 }).format(n)
const fmtDate = (d: string | null) => d ? new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '—'
const daysDiff = (d: string) => Math.floor((new Date(d).getTime() - Date.now()) / 86400000)

const STATUS_COLORS: Record<string, string> = {
  Pending: 'badge-warning', Open: 'badge-info', Sent: 'badge-info',
  PartiallyReceived: 'badge-primary', FullyReceived: 'badge-success', Closed: 'badge-ghost', Canceled: 'badge-error',
  Unreconciled: 'badge-warning', Reconciled: 'badge-success', Discrepancy: 'badge-error',
}

export function ApPage() {
  const [tab, setTab] = useState<'summary' | 'pos' | 'bills'>('pos')
  const [summary, setSummary] = useState<ApSummary | null>(null)
  const [pos, setPos] = useState<PO[]>([])
  const [bills, setBills] = useState<Bill[]>([])
  const [loading, setLoading] = useState(true)
  const [expandedPo, setExpandedPo] = useState<number | null>(null)

  useEffect(() => {
    const loadData = async () => {
      try {
        const [poR, billR] = await Promise.all([
          fetch('/api/ap/purchase-orders', { credentials: 'include' }),
          fetch('/api/ap/bills-enhanced', { credentials: 'include' }),
        ])
        const poData = await poR.json()
        const billData = await billR.json()
        if (Array.isArray(poData)) setPos(poData)
        if (Array.isArray(billData)) setBills(billData)
      } catch {}
      try {
        const sumR = await fetch('/api/ap/summary', { credentials: 'include' })
        const sumData = await sumR.json()
        if (!sumData.error) setSummary(sumData)
      } catch {}
      setLoading(false)
    }
    loadData()
  }, [])

  if (loading) return <div className="flex items-center justify-center py-20"><span className="loading loading-spinner loading-lg text-primary" /></div>

  const unpaidBills = bills.filter(b => !b.isPaid)

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold text-base-content">Accounts Payable & Purchasing</h1>
        <div className="text-right">
          <div className="text-xs text-base-content/40">Total Unpaid</div>
          <div className="text-2xl font-bold text-error">{fmt(summary?.totalUnpaid ?? 0)}</div>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-5 gap-3">
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Unpaid Bills</div>
          <div className="text-lg font-bold text-error">{fmt(summary?.totalUnpaid ?? 0)}</div>
          <div className="text-xs text-base-content/50">{summary?.billCount ?? 0} bills</div>
        </div></div>
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Overdue</div>
          <div className="text-lg font-bold text-error">{fmt(summary?.overdueAmount ?? 0)}</div>
          <div className="text-xs text-base-content/50">{summary?.overdueCount ?? 0} past due</div>
        </div></div>
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Due This Week</div>
          <div className="text-lg font-bold text-warning">{fmt(summary?.dueThisWeek ?? 0)}</div>
        </div></div>
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Open POs</div>
          <div className="text-lg font-bold text-info">{summary?.openPoCount ?? 0}</div>
          <div className="text-xs text-base-content/50">{fmt(summary?.openPoTotal ?? 0)}</div>
        </div></div>
        <div className="card bg-base-100 shadow-sm"><div className="card-body p-3">
          <div className="text-[10px] text-base-content/40 uppercase">Due This Month</div>
          <div className="text-lg font-bold text-warning">{fmt(summary?.dueThisMonth ?? 0)}</div>
        </div></div>
      </div>

      {/* Tabs */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body p-0">
          <div className="flex border-b border-base-200 px-4 pt-3 gap-1">
            {([['summary', 'Summary'], ['pos', `Purchase Orders (${pos.length})`], ['bills', `Bills (${unpaidBills.length})`]] as const).map(([key, label]) => (
              <button key={key} onClick={() => setTab(key as typeof tab)}
                className={`px-3 py-1.5 text-sm font-medium rounded-t border-b-2 transition-colors ${tab === key ? 'border-b-primary text-primary bg-primary/5' : 'border-b-transparent text-base-content/50 hover:text-base-content'}`}>
                {label}
              </button>
            ))}
          </div>

          {/* Summary Tab */}
          {tab === 'summary' && summary && (
            <div className="p-4">
              <h3 className="font-semibold text-sm text-base-content mb-3">Payables by Vendor</h3>
              {summary.byVendor.length > 0 ? (
                <div className="space-y-2">
                  {summary.byVendor.map((v, i) => (
                    <div key={i} className="flex items-center gap-3">
                      <span className="text-sm font-medium flex-1 truncate">{v.vendor}</span>
                      <span className="text-xs text-base-content/40">{v.count} bills</span>
                      <span className="text-sm font-bold text-error w-24 text-right">{fmt(v.total)}</span>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-base-content/40">No outstanding payables</p>
              )}
            </div>
          )}

          {/* POs Tab */}
          {tab === 'pos' && (
            <div className="divide-y divide-base-200">
              {pos.length > 0 ? pos.map(po => (
                <div key={po.id}>
                  <div className="flex items-center gap-3 px-4 py-3 hover:bg-base-200/30 cursor-pointer"
                    onClick={() => setExpandedPo(expandedPo === po.id ? null : po.id)}>
                    <span className="icon-[tabler--chevron-right] size-4 text-base-content/30 transition-transform" style={{ transform: expandedPo === po.id ? 'rotate(90deg)' : '' }} />
                    <span className="font-mono text-primary text-sm font-medium">PO-{po.number}</span>
                    <span className={`badge badge-xs ${STATUS_COLORS[po.status] || 'badge-ghost'}`}>{po.status}</span>
                    <span className="text-sm text-base-content/70 truncate flex-1">{po.vendorName}</span>
                    {po.jobNumber && <span className="text-xs text-base-content/40">Job #{po.jobNumber}</span>}
                    <span className="text-xs text-base-content/40">{po.itemCount} items</span>
                    <span className="text-sm font-bold">{fmt(po.total)}</span>
                    <span className="text-xs text-base-content/40">{fmtDate(po.date)}</span>
                  </div>
                  {expandedPo === po.id && po.items.length > 0 && (
                    <div className="bg-base-200/20 px-4 pb-3">
                      {po.summary && <p className="text-xs text-base-content/50 mb-2 pl-8">{po.summary}</p>}
                      <table className="table table-xs ml-8">
                        <thead>
                          <tr className="text-[10px] text-base-content/40 uppercase">
                            <th>Item</th><th>Code</th><th className="text-right">Qty</th><th className="text-right">Received</th><th className="text-right">Cost</th><th className="text-right">Total</th><th>Status</th>
                          </tr>
                        </thead>
                        <tbody>
                          {po.items.map((item, i) => (
                            <tr key={i}>
                              <td className="text-sm">{item.skuName}</td>
                              <td className="text-xs text-base-content/50 font-mono">{item.skuCode}</td>
                              <td className="text-sm text-right">{item.quantity}</td>
                              <td className={`text-sm text-right ${item.quantityReceived < item.quantity ? 'text-warning' : 'text-success'}`}>{item.quantityReceived}</td>
                              <td className="text-sm text-right">{fmt(item.cost)}</td>
                              <td className="text-sm text-right font-medium">{fmt(item.total)}</td>
                              <td><span className={`badge badge-xs ${STATUS_COLORS[item.status] || 'badge-ghost'}`}>{item.status}</span></td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              )) : <p className="text-sm text-base-content/40 px-4 py-4">No purchase orders found. Sync to pull POs from ServiceTitan.</p>}
            </div>
          )}

          {/* Bills Tab */}
          {tab === 'bills' && (
            <div className="overflow-x-auto">
              {unpaidBills.length > 0 ? (
                <table className="table table-sm">
                  <thead>
                    <tr className="text-xs text-base-content/40 uppercase">
                      <th>Vendor</th><th>Reference</th><th>Source</th><th className="text-right">Amount</th><th>Due Date</th><th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {unpaidBills.map(b => {
                      const dd = daysDiff(b.dueDate)
                      return (
                        <tr key={b.id} className="hover:bg-base-200/40">
                          <td className="text-sm font-medium">{b.vendorName || '—'}</td>
                          <td className="text-sm font-mono text-base-content/70">{b.referenceNumber || b.invoiceNumber || '—'}</td>
                          <td><span className="badge badge-xs badge-ghost">{b.source || '—'}</span></td>
                          <td className="text-sm text-right font-bold">{fmt(b.amount)}</td>
                          <td className={`text-sm ${dd < 0 ? 'text-error font-semibold' : dd < 7 ? 'text-warning' : 'text-base-content/60'}`}>
                            {fmtDate(b.dueDate)}
                            {dd < 0 && <span className="text-[10px] ml-1">({Math.abs(dd)}d late)</span>}
                          </td>
                          <td><span className={`badge badge-xs ${STATUS_COLORS[b.status || ''] || 'badge-ghost'}`}>{b.status || '—'}</span></td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              ) : <p className="text-sm text-base-content/40 px-4 py-4">No unpaid bills</p>}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

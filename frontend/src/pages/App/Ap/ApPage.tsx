import { useState, useEffect, useCallback } from 'react'

interface Vendor { id: number; name: string }
interface Bill {
  id: number; vendorId: number; vendorName: string
  invoiceNumber: string; amount: number; dueDate: string
  isPaid: boolean; paidDate: string | null
}

function fmt(n: number) {
  return n.toLocaleString('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 2 })
}
function fmtDate(d: string) {
  return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}
function daysUntil(d: string) {
  return Math.ceil((new Date(d).getTime() - Date.now()) / 86400000)
}

export function ApPage() {
  const [bills, setBills] = useState<Bill[]>([])
  const [vendors, setVendors] = useState<Vendor[]>([])
  const [loading, setLoading] = useState(true)
  const [showPaid, setShowPaid] = useState(false)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState({ vendorId: 0, invoiceNumber: '', amount: '', dueDate: '' })
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const fetchData = useCallback(async () => {
    const [bRes, vRes] = await Promise.all([
      fetch('/api/ap/bills', { credentials: 'include' }),
      fetch('/api/ap/vendors', { credentials: 'include' }),
    ])
    if (bRes.ok) setBills(await bRes.json())
    if (vRes.ok) setVendors(await vRes.json())
  }, [])

  useEffect(() => { fetchData().finally(() => setLoading(false)) }, [fetchData])

  const handleSubmit = async () => {
    if (!form.vendorId || !form.amount || !form.dueDate) { setError('All fields required'); return }
    setSaving(true); setError('')
    const res = await fetch('/api/ap/bills', {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        vendorId: form.vendorId,
        invoiceNumber: form.invoiceNumber,
        amount: parseFloat(form.amount),
        dueDate: form.dueDate,
      })
    })
    if (res.ok) {
      const bill = await res.json()
      setBills(prev => [...prev, bill])
      setForm({ vendorId: 0, invoiceNumber: '', amount: '', dueDate: '' })
      setShowForm(false)
    } else {
      const d = await res.json(); setError(d.error || 'Failed to add bill')
    }
    setSaving(false)
  }

  const togglePaid = async (bill: Bill) => {
    const endpoint = bill.isPaid ? 'unpay' : 'pay'
    const res = await fetch(`/api/ap/bills/${bill.id}/${endpoint}`, { method: 'PUT', credentials: 'include' })
    if (res.ok) {
      setBills(prev => prev.map(b => b.id === bill.id ? { ...b, isPaid: !b.isPaid, paidDate: !b.isPaid ? new Date().toISOString() : null } : b))
    }
  }

  const deleteBill = async (id: number) => {
    if (!confirm('Delete this bill?')) return
    const res = await fetch(`/api/ap/bills/${id}`, { method: 'DELETE', credentials: 'include' })
    if (res.ok) setBills(prev => prev.filter(b => b.id !== id))
  }

  const unpaid = bills.filter(b => !b.isPaid)
  const displayed = showPaid ? bills : unpaid
  const totalOwed = unpaid.reduce((s, b) => s + b.amount, 0)

  // Group by vendor for summary
  const byVendor = Object.values(
    unpaid.reduce((acc, b) => {
      if (!acc[b.vendorId]) acc[b.vendorId] = { vendorName: b.vendorName, total: 0, count: 0, nextDue: b.dueDate }
      acc[b.vendorId].total += b.amount
      acc[b.vendorId].count++
      if (b.dueDate < acc[b.vendorId].nextDue) acc[b.vendorId].nextDue = b.dueDate
      return acc
    }, {} as Record<number, { vendorName: string; total: number; count: number; nextDue: string }>)
  ).sort((a, b) => b.total - a.total)

  if (loading) {
    return <div className="flex items-center justify-center py-20"><span className="loading loading-spinner loading-md text-primary" /></div>
  }

  return (
    <div className="max-w-6xl mx-auto space-y-6 py-2">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-base-content">Accounts Payable</h1>
          <p className="text-sm text-base-content/60 mt-1">{unpaid.length} unpaid bills · {fmt(totalOwed)} owed</p>
        </div>
        <button onClick={() => setShowForm(true)} className="btn btn-primary btn-sm gap-1.5">
          <span className="icon-[tabler--plus] size-4" /> Add Bill
        </button>
      </div>

      {/* Vendors Owed Summary */}
      {byVendor.length > 0 && (
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body p-0">
            <div className="px-4 pt-4 pb-3 border-b border-base-200">
              <h3 className="font-semibold text-sm text-base-content">Vendors Owed</h3>
            </div>
            <table className="table table-sm">
              <thead>
                <tr className="text-xs text-base-content/40 uppercase">
                  <th>Vendor</th>
                  <th className="text-center">Invoices</th>
                  <th className="text-right">Amount Due</th>
                  <th className="text-right">Next Due</th>
                </tr>
              </thead>
              <tbody>
                {byVendor.map((v, i) => {
                  const days = daysUntil(v.nextDue)
                  return (
                    <tr key={i} className="hover:bg-base-200/40">
                      <td className="font-medium text-base-content">{v.vendorName}</td>
                      <td className="text-center"><span className="badge badge-ghost badge-sm">{v.count}</span></td>
                      <td className="text-right font-semibold">{fmt(v.total)}</td>
                      <td className="text-right">
                        <span className={`text-sm ${days < 0 ? 'text-error font-medium' : days <= 7 ? 'text-warning' : 'text-base-content/60'}`}>
                          {fmtDate(v.nextDue)}
                        </span>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Add Bill Modal */}
      {showForm && (
        <div className="card bg-base-100 shadow-sm border border-primary/20">
          <div className="card-body p-4 space-y-3">
            <h3 className="font-semibold text-base-content">Add Bill</h3>
            {error && <div className="alert alert-soft alert-error text-sm py-2">{error}</div>}
            {vendors.length === 0 && (
              <div className="alert alert-soft alert-warning text-sm py-2">
                No vendors yet. <a href="/app/settings" className="text-primary underline">Add vendors in Settings</a> first.
              </div>
            )}
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
              <select
                className="select select-sm"
                value={form.vendorId}
                onChange={e => setForm(f => ({ ...f, vendorId: parseInt(e.target.value) }))}
              >
                <option value={0}>Select Vendor</option>
                {vendors.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
              </select>
              <input
                className="input input-sm"
                placeholder="Invoice #"
                value={form.invoiceNumber}
                onChange={e => setForm(f => ({ ...f, invoiceNumber: e.target.value }))}
              />
              <input
                className="input input-sm"
                type="number"
                step="0.01"
                placeholder="Amount"
                value={form.amount}
                onChange={e => setForm(f => ({ ...f, amount: e.target.value }))}
              />
              <input
                className="input input-sm"
                type="date"
                value={form.dueDate}
                onChange={e => setForm(f => ({ ...f, dueDate: e.target.value }))}
              />
            </div>
            <div className="flex gap-2 justify-end">
              <button className="btn btn-ghost btn-sm" onClick={() => { setShowForm(false); setError('') }}>Cancel</button>
              <button className="btn btn-primary btn-sm" onClick={handleSubmit} disabled={saving}>
                {saving ? 'Adding...' : 'Add Bill'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Filter tabs */}
      <div className="tabs tabs-bordered">
        <button onClick={() => setShowPaid(false)} className={`tab ${!showPaid ? 'tab-active' : ''}`}>
          Unpaid <span className="badge badge-soft badge-sm ms-2">{unpaid.length}</span>
        </button>
        <button onClick={() => setShowPaid(true)} className={`tab ${showPaid ? 'tab-active' : ''}`}>
          All <span className="badge badge-soft badge-sm ms-2">{bills.length}</span>
        </button>
      </div>

      {/* Bills Table */}
      {displayed.length === 0 ? (
        <div className="text-center py-16 text-base-content/40 text-sm">
          {showPaid ? 'No bills yet.' : 'No unpaid bills. You\'re all caught up!'}
        </div>
      ) : (
        <div className="rounded-box border border-base-content/10 overflow-hidden">
          <table className="table table-sm">
            <thead>
              <tr>
                <th>Vendor</th>
                <th>Invoice #</th>
                <th className="text-right">Amount</th>
                <th>Due Date</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {displayed.map(b => {
                const days = daysUntil(b.dueDate)
                return (
                  <tr key={b.id} className="hover">
                    <td className="font-medium text-base-content">{b.vendorName}</td>
                    <td className="text-base-content/60">{b.invoiceNumber || '—'}</td>
                    <td className="text-right font-medium">{fmt(b.amount)}</td>
                    <td>
                      <span className={`text-sm ${!b.isPaid && days < 0 ? 'text-error font-medium' : !b.isPaid && days <= 7 ? 'text-warning' : 'text-base-content/60'}`}>
                        {fmtDate(b.dueDate)}
                        {!b.isPaid && days < 0 && <span className="text-xs ml-1">({Math.abs(days)}d overdue)</span>}
                      </span>
                    </td>
                    <td>
                      <button onClick={() => togglePaid(b)} className={`badge badge-soft badge-sm cursor-pointer ${b.isPaid ? 'badge-success' : 'badge-warning'}`}>
                        {b.isPaid ? 'Paid' : 'Unpaid'}
                      </button>
                    </td>
                    <td>
                      <button onClick={() => deleteBill(b.id)} className="btn btn-ghost btn-xs text-base-content/30 hover:text-error">
                        <span className="icon-[tabler--trash] size-3.5" />
                      </button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

import { useState, useEffect, useCallback } from 'react'
import { useTheme, type Theme } from '@/hooks/useTheme'

const THEMES: { value: Theme; label: string; description: string; icon: string }[] = [
  { value: 'dark', label: 'Dark', description: 'Easy on the eyes, great for night work.', icon: 'icon-[tabler--moon]' },
  { value: 'light', label: 'Light', description: 'Clean and bright for daytime use.', icon: 'icon-[tabler--sun]' },
  { value: 'corporate', label: 'Corporate', description: 'Professional blue tones for presentations.', icon: 'icon-[tabler--building]' },
]

interface Vendor { id: number; name: string; contactName?: string; phone?: string; email?: string }

export function SettingsPage() {
  const { theme, setTheme } = useTheme()
  const [vendors, setVendors] = useState<Vendor[]>([])
  const [vendorForm, setVendorForm] = useState({ name: '', contactName: '', phone: '', email: '' })
  const [showVendorForm, setShowVendorForm] = useState(false)
  const [vendorSaving, setVendorSaving] = useState(false)
  const [vendorError, setVendorError] = useState('')

  const fetchVendors = useCallback(async () => {
    const res = await fetch('/api/ap/vendors', { credentials: 'include' })
    if (res.ok) setVendors(await res.json())
  }, [])

  useEffect(() => { fetchVendors() }, [fetchVendors])

  const addVendor = async () => {
    if (!vendorForm.name.trim()) { setVendorError('Name is required'); return }
    setVendorSaving(true); setVendorError('')
    const res = await fetch('/api/ap/vendors', {
      method: 'POST', credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(vendorForm)
    })
    if (res.ok) {
      const v = await res.json()
      setVendors(prev => [...prev, v].sort((a, b) => a.name.localeCompare(b.name)))
      setVendorForm({ name: '', contactName: '', phone: '', email: '' })
      setShowVendorForm(false)
    } else {
      const d = await res.json(); setVendorError(d.error || 'Failed')
    }
    setVendorSaving(false)
  }

  const deleteVendor = async (id: number) => {
    if (!confirm('Delete this vendor?')) return
    const res = await fetch(`/api/ap/vendors/${id}`, { method: 'DELETE', credentials: 'include' })
    if (res.ok) setVendors(prev => prev.filter(v => v.id !== id))
    else { const d = await res.json(); alert(d.error || 'Failed to delete') }
  }

  return (
    <div className="max-w-2xl mx-auto space-y-8 py-2">
      <div>
        <h1 className="text-base-content text-2xl font-semibold">Settings</h1>
        <p className="text-base-content/60 text-sm mt-1">Manage your account preferences.</p>
      </div>

      {/* Theme */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-5">
          <div>
            <h2 className="text-base-content font-semibold text-base">Appearance</h2>
            <p className="text-base-content/60 text-sm mt-0.5">Choose how MyServiceAO looks for you.</p>
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            {THEMES.map((t) => {
              const active = theme === t.value
              return (
                <button
                  key={t.value}
                  onClick={() => setTheme(t.value)}
                  className={`rounded-box border-2 p-4 text-left transition-all ${
                    active ? 'border-primary bg-primary/5' : 'border-base-content/10 hover:border-base-content/30 bg-base-200/50'
                  }`}
                >
                  <div className="flex items-center gap-2 mb-2">
                    <span className={`${t.icon} size-5 ${active ? 'text-primary' : 'text-base-content/60'}`} />
                    <span className={`font-medium text-sm ${active ? 'text-primary' : 'text-base-content'}`}>{t.label}</span>
                    {active && <span className="icon-[tabler--check] size-4 text-primary ms-auto" />}
                  </div>
                  <p className="text-xs text-base-content/50">{t.description}</p>
                </button>
              )
            })}
          </div>
        </div>
      </div>

      {/* Vendors */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-base-content font-semibold text-base">Vendors</h2>
              <p className="text-base-content/60 text-sm mt-0.5">Manage vendors for Accounts Payable.</p>
            </div>
            <button onClick={() => setShowVendorForm(true)} className="btn btn-primary btn-sm gap-1">
              <span className="icon-[tabler--plus] size-4" /> Add Vendor
            </button>
          </div>

          {showVendorForm && (
            <div className="rounded-box border border-primary/20 bg-base-200/30 p-4 space-y-3">
              {vendorError && <div className="alert alert-soft alert-error text-sm py-2">{vendorError}</div>}
              <div className="grid grid-cols-2 gap-3">
                <input className="input input-sm" placeholder="Company Name *" value={vendorForm.name} onChange={e => setVendorForm(f => ({ ...f, name: e.target.value }))} />
                <input className="input input-sm" placeholder="Contact Name" value={vendorForm.contactName} onChange={e => setVendorForm(f => ({ ...f, contactName: e.target.value }))} />
                <input className="input input-sm" placeholder="Phone" value={vendorForm.phone} onChange={e => setVendorForm(f => ({ ...f, phone: e.target.value }))} />
                <input className="input input-sm" placeholder="Email" value={vendorForm.email} onChange={e => setVendorForm(f => ({ ...f, email: e.target.value }))} />
              </div>
              <div className="flex gap-2 justify-end">
                <button className="btn btn-ghost btn-sm" onClick={() => { setShowVendorForm(false); setVendorError('') }}>Cancel</button>
                <button className="btn btn-primary btn-sm" onClick={addVendor} disabled={vendorSaving}>{vendorSaving ? 'Adding...' : 'Save Vendor'}</button>
              </div>
            </div>
          )}

          {vendors.length === 0 ? (
            <p className="text-sm text-base-content/40">No vendors yet. Add one to start tracking AP.</p>
          ) : (
            <div className="rounded-box border border-base-content/10 overflow-hidden">
              <table className="table table-sm">
                <thead>
                  <tr><th>Name</th><th>Contact</th><th>Phone</th><th>Email</th><th></th></tr>
                </thead>
                <tbody>
                  {vendors.map(v => (
                    <tr key={v.id} className="hover">
                      <td className="font-medium text-base-content">{v.name}</td>
                      <td className="text-base-content/60">{v.contactName || '—'}</td>
                      <td className="text-base-content/60">{v.phone || '—'}</td>
                      <td className="text-base-content/60">{v.email || '—'}</td>
                      <td>
                        <button onClick={() => deleteVendor(v.id)} className="btn btn-ghost btn-xs text-base-content/30 hover:text-error">
                          <span className="icon-[tabler--trash] size-3.5" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Account placeholder */}
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-2">
          <h2 className="text-base-content font-semibold text-base">Account</h2>
          <p className="text-base-content/40 text-sm">Profile and password settings coming soon.</p>
        </div>
      </div>

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-2">
          <h2 className="text-base-content font-semibold text-base">Integrations</h2>
          <p className="text-base-content/40 text-sm">Manage your connected services.</p>
          <a href="/app/servicetitan" className="link link-primary text-sm w-fit">ServiceTitan settings →</a>
        </div>
      </div>
    </div>
  )
}

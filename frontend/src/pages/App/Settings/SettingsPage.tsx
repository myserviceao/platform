import { useState, useEffect, useCallback, useRef } from 'react'
import { useTheme, type Theme } from '@/hooks/useTheme'
import { useAuth } from '@/hooks/useAuth'

const THEMES: { value: Theme; label: string; description: string; colors: [string, string, string] }[] = [
  { value: 'dark', label: 'Dark', description: 'Default dark with purple accents', colors: ['#1d232a', '#661ae6', '#2a323c'] },
  { value: 'midnight', label: 'Midnight', description: 'Deep navy with sky blue accents', colors: ['#0f172a', '#38bdf8', '#1e293b'] },
  { value: 'emerald', label: 'Emerald', description: 'Dark with rich green accents', colors: ['#0f1a17', '#10b981', '#1c2826'] },
  { value: 'crimson', label: 'Crimson', description: 'Dark with bold red-pink accents', colors: ['#1a1015', '#f43f5e', '#2a2025'] },
  { value: 'ocean', label: 'Ocean', description: 'Deep indigo with violet accents', colors: ['#0c0a3e', '#6366f1', '#1e1b4b'] },
  { value: 'amber', label: 'Amber', description: 'Warm dark with gold accents', colors: ['#1c1917', '#f59e0b', '#292524'] },
  { value: 'slate', label: 'Slate', description: 'Clean minimal dark with muted tones', colors: ['#0f172a', '#94a3b8', '#1e293b'] },
  { value: 'luxury', label: 'Luxury', description: 'Elegant black with gold', colors: ['#09090b', '#dca54c', '#171717'] },
  { value: 'gourmet', label: 'Gourmet', description: 'Warm cream with earthy tones', colors: ['#faf5ef', '#c8702a', '#f0e8dc'] },
  { value: 'light', label: 'Light', description: 'Clean and bright', colors: ['#ffffff', '#661ae6', '#f2f2f2'] },
  { value: 'corporate', label: 'Corporate', description: 'Professional with blue tones', colors: ['#ffffff', '#4b6bfb', '#f0f0f0'] },
  { value: 'soft', label: 'Soft', description: 'Gentle pastels, easy reading', colors: ['#f9fafb', '#6366f1', '#f3f4f6'] },
]

interface Vendor { id: number; name: string; contactName?: string; phone?: string; email?: string }


function ProfileSettings() {
  const { user } = useAuth()
  const [title, setTitle] = useState(user?.title || '')
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [logoPreview, setLogoPreview] = useState(user?.tenant?.logoUrl || '')
  const [msg, setMsg] = useState('')
  const fileRef = useRef<HTMLInputElement>(null)

  const saveTitle = async () => {
    setSaving(true); setMsg('')
    const res = await fetch('/api/profile/title', {
      method: 'PUT', credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title })
    })
    if (res.ok) setMsg('Title saved! Reload to see changes in sidebar.')
    else setMsg('Failed to save title')
    setSaving(false)
  }

  const uploadLogo = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setUploading(true); setMsg('')
    const form = new FormData()
    form.append('file', file)
    const res = await fetch('/api/profile/logo', {
      method: 'POST', credentials: 'include', body: form
    })
    if (res.ok) {
      const d = await res.json()
      setLogoPreview(d.logoUrl)
      setMsg('Logo uploaded! Reload to see changes in sidebar.')
    } else {
      const d = await res.json()
      setMsg(d.error || 'Upload failed')
    }
    setUploading(false)
  }

  const removeLogo = async () => {
    const res = await fetch('/api/profile/logo', { method: 'DELETE', credentials: 'include' })
    if (res.ok) {
      setLogoPreview('')
      setMsg('Logo removed.')
    }
  }

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-5">
        <div>
          <h2 className="text-base-content font-semibold text-base">Profile & Branding</h2>
          <p className="text-base-content/60 text-sm mt-0.5">Your logo and display title shown in the sidebar.</p>
        </div>

        {msg && (
          <div className="alert alert-soft alert-info text-sm py-2">{msg}</div>
        )}

        {/* Logo upload */}
        <div className="flex items-center gap-4">
          {logoPreview ? (
            <img src={logoPreview} alt="Logo" className="max-h-16 max-w-[10rem] object-contain" />
          ) : (
            <div className="w-16 h-16 rounded-full bg-base-200 flex items-center justify-center">
              <span className="icon-[tabler--photo] size-6 text-base-content/30" />
            </div>
          )}
          <div className="space-y-1">
            <input ref={fileRef} type="file" accept="image/*" onChange={uploadLogo} className="hidden" />
            <button onClick={() => fileRef.current?.click()} className="btn btn-sm btn-primary gap-1" disabled={uploading}>
              <span className="icon-[tabler--upload] size-4" />
              {uploading ? 'Uploading...' : 'Upload Logo'}
            </button>
            {logoPreview && (
              <button onClick={removeLogo} className="btn btn-sm btn-ghost text-error gap-1">
                <span className="icon-[tabler--trash] size-3.5" /> Remove
              </button>
            )}
            <p className="text-xs text-base-content/40">PNG, JPG, or SVG. Max 2MB.</p>
          </div>
        </div>

        {/* Title */}
        <div className="space-y-2">
          <label className="text-sm font-medium text-base-content">Display Title</label>
          <div className="flex gap-2">
            <input
              className="input input-sm flex-1"
              placeholder="e.g. CEO, Operations Manager, Owner"
              value={title}
              onChange={e => setTitle(e.target.value)}
            />
            <button onClick={saveTitle} className="btn btn-sm btn-primary" disabled={saving}>
              {saving ? 'Saving...' : 'Save'}
            </button>
          </div>
          <p className="text-xs text-base-content/40">Shown under your name in the sidebar. Leave blank to show your role.</p>
        </div>
      </div>
    </div>
  )
}

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
          <div className="grid gap-3 grid-cols-3 sm:grid-cols-4 lg:grid-cols-6">
            {THEMES.map((t) => {
              const active = theme === t.value
              return (
                <button
                  key={t.value}
                  onClick={() => setTheme(t.value)}
                  className={`rounded-box border-2 p-3 text-center transition-all ${
                    active ? 'border-primary ring-2 ring-primary/30' : 'border-base-content/10 hover:border-base-content/30'
                  }`}
                >
                  <div className="flex justify-center gap-1 mb-2">
                    {t.colors.map((c, i) => (
                      <div
                        key={i}
                        className={`rounded-full ${i === 1 ? 'size-6' : 'size-4'}`}
                        style={{ backgroundColor: c }}
                      />
                    ))}
                  </div>
                  <div className={`text-xs font-medium ${active ? 'text-primary' : 'text-base-content/70'}`}>{t.label}</div>
                  {active && <span className="icon-[tabler--check] size-3.5 text-primary mx-auto mt-1 block" />}
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

      {/* Profile & Logo */}
      <ProfileSettings />

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
